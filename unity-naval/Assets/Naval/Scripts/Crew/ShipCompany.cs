using System.Collections.Generic;
using UnityEngine;
using Role = Galleon.CrewPlayer.Role;

namespace Galleon
{
    public sealed class ShipCompany : MonoBehaviour
    {
        const int AimBandCount = 3;
        const int QuarterdeckShotInterval = 12;
        enum GunWork { Unavailable, Standby, Reload, Ready }

        readonly List<CrewMate> mates = new();
        readonly List<(int dutyOrder, float distance, CrewMate mate, int gun)> gunCandidates = new();
        NavalShip ship, enemy;
        CrewDeckPaths paths;
        CrewMate deputy, aiAtWheel;
        CrewPlayer playerAtPost;
        Role playerRole;
        int playerIndex;
        int assignments, side = 1;
        float nextPlan, retryCaptainAt;
        bool hasSailed;
        int[] aimBandByGun, shotsByGun;
        Vector3[] gunFeet;
        GunWork[] gunWork;
        bool[] reservedGuns;

        public int Capacity => 1 + GunnerCount + DamageControlCount;
        public int GunnerCount { get; private set; }
        public int DamageControlCount { get; private set; }
        public CrewDamageTarget Captain { get; private set; }
        public CrewDamageTarget ActingCaptain { get { RefreshCaptain(); return deputy; } }
        public IReadOnlyList<CrewMate> Mates => mates;
        public int ShotsFired { get; private set; }
        public int LastShotSide { get; private set; }
        internal int ShotsFiredByGun(int index) => shotsByGun[index];

        internal static int GetGunnerCount(ShipType type)
        {
            int port = 0, starboard = 0;
            foreach (var gun in ShipDefinition.For(type).Data.guns)
            {
                if (gun.forward.x > .5f) starboard++;
                else if (gun.forward.x < -.5f) port++;
            }
            return Mathf.Max(port, starboard);
        }
        internal static int GetDamageControlCount(ShipType type) => type == ShipType.Sloop ? 1 : 2;

        internal void Initialize(NavalShip owner, NavalShip opponent, CrewPlayer playerCaptain)
        {
            ship = owner;
            enemy = opponent;
            ship.Company = this;
            GunnerCount = GetGunnerCount(ship.Definition.Type);
            DamageControlCount = GetDamageControlCount(ship.Definition.Type);
            int gunCount = ship.Rig.Guns.Length;
            aimBandByGun = new int[gunCount];
            shotsByGun = new int[gunCount];
            gunFeet = new Vector3[gunCount];
            gunWork = new GunWork[gunCount];
            reservedGuns = new bool[gunCount];
            // Give both batteries the same spread independently of the rig's marker order.
            int port = 0, starboard = 0;
            for (int i = 0; i < aimBandByGun.Length; i++)
            {
                float across = ship.Definition.Data.guns[i].forward.x;
                if (across < -.5f) aimBandByGun[i] = port++ % AimBandCount;
                if (across > .5f) aimBandByGun[i] = starboard++ % AimBandCount;
                var gun = ship.Rig.Guns[i];
                if (CrewStationFit.TryGetPose(ship, Role.Gun, i, gun, ship.transform.position, out var foot, out _))
                    gunFeet[i] = ship.transform.InverseTransformPoint(foot);
            }
            Captain = playerCaptain;
            paths = new CrewDeckPaths(ship);
            // The navy roster continues after the pirate roster, regardless of creation order.
            int rosterStart = !ship.IsPirate && enemy && enemy.IsPirate
                ? 1 + GetGunnerCount(enemy.Definition.Type) + GetDamageControlCount(enemy.Definition.Type)
                : 0;
            for (int slot = playerCaptain ? 1 : 0; slot < Capacity; slot++)
            {
                CrewDuty duty = slot == 0 ? CrewDuty.Captain : slot <= GunnerCount ? CrewDuty.Gunner : CrewDuty.DamageControl;
                string title = duty == CrewDuty.Captain ? "船长" : duty == CrewDuty.Gunner ? $"炮手 {slot}" : $"损管员 {slot - GunnerCount}";
                var mate = new GameObject(title).AddComponent<CrewMate>();
                mate.transform.SetParent(ship.transform, false);
                mate.Initialize(ship, paths, slot, duty, paths.Spawn(slot, duty), rosterStart + slot);
                mates.Add(mate);
                if (slot == 0) Captain = mate;
            }
        }

        void RefreshCaptain()
        {
            if (!ship) return;
            if (aiAtWheel && aiAtWheel.Health <= 0) RelinquishWheel();
            CrewMate next = null;
            if (Captain && Captain.Health <= 0)
            {
                if (deputy && deputy.Health > 0) return;
                if (!deputy && Time.time < retryCaptainAt) return;
                float nearest = float.PositiveInfinity;
                foreach (var mate in mates)
                {
                    if (mate.Health <= 0 || mate == Captain) continue;
                    float distance = mate.HelmWalkDistance();
                    if (distance < nearest)
                    {
                        nearest = distance;
                        next = mate;
                    }
                }
                retryCaptainAt = next ? 0f : Time.time + .32f;
            }
            else retryCaptainAt = 0f;
            if (next == deputy) return;
            if (deputy && deputy.Job == Role.Helm) deputy.ClearJob();
            deputy = next;
            RelinquishWheel();
            nextPlan = 0f;
        }

        internal bool CanSteer(CrewDamageTarget who)
        {
            RefreshCaptain();
            return who && who.Health > 0 && (who == Captain || who == deputy);
        }

        internal bool TryTakePlayerPost(CrewPlayer who, Role role, int index)
        {
            if (!who || who.Health <= 0 || (role == Role.Helm && !CanSteer(who))) return false;
            if (playerAtPost && playerAtPost != who) return false;
            playerAtPost = who;
            playerRole = role;
            playerIndex = index;
            foreach (var mate in mates)
                if (mate.Job == role && mate.JobIndex == index) mate.ClearJob();
            if (role == Role.Helm) RelinquishWheel();
            nextPlan = 0f;
            return true;
        }

        internal void ReleasePlayerPost(CrewPlayer who)
        {
            if (playerAtPost != who) return;
            playerAtPost = null;
            nextPlan = 0f;
        }

        bool PlayerUses(Role role, int index) => playerAtPost && playerAtPost.Health > 0 &&
            playerRole == role && playerIndex == index;

        void Update()
        {
            if (!ship || ship.Flooding.IsSunk || Time.deltaTime <= 0f) return;
            RefreshCaptain();
            var commander = Captain.Health > 0 ? Captain as CrewMate : deputy;
            bool opposing = enemy && !enemy.Flooding.IsSunk;
            Vector3 delta = opposing ? Centre(enemy) - Centre(ship) : Vector3.zero;
            delta.y = 0f;
            float across = Vector3.Dot(delta, ship.transform.right);
            if (Mathf.Abs(across) > 2f && side != (across > 0f ? 1 : -1))
            {
                side = -side;
                nextPlan = 0f;
            }
            // A captain may leave a secure anchorage to help the crew. A new target
            // bearing or an opponent getting underway calls the captain back to the wheel.
            bool hold = !opposing || (hasSailed && ShotsFired > 0 && enemy.Anchored &&
                delta.magnitude < 65f && Mathf.Abs(across) > .95f * delta.magnitude);
            bool needsWheel = (deputy && Captain is CrewPlayer) || !hold || !ship.Anchored;
            if (Time.time >= nextPlan)
            {
                nextPlan = Time.time + .32f;
                Plan(commander, needsWheel, opposing);
            }
            if (commander && commander.Job == Role.Helm && commander.AtPost)
            {
                aiAtWheel = commander;
                Navigate(delta, hold);
            }
            else RelinquishWheel();
        }

        // CrewPlayer restores life in its own Update, after the company's orders.
        // Revoke a deputy's helm input before NavalShip integrates another frame.
        void LateUpdate()
        {
            if (deputy || aiAtWheel) RefreshCaptain();
        }

        static Vector3 Centre(NavalShip vessel) => vessel.transform.TransformPoint(0f,
            vessel.Definition.DryDraft, vessel.Definition.PivotZ);

        void Plan(CrewMate commander, bool needsWheel, bool opposing)
        {
            assignments = 0;
            GunWork bestGun = PrepareGuns(opposing);
            var water = ship.Flooding;
            bool soloGunner = bestGun >= GunWork.Reload && HasSingleAvailableMate() &&
                (bestGun == GunWork.Ready || (water.ActiveLeaks == 0 && water.WaterLevel01 == 0f));
            if (commander && needsWheel && !soloGunner && !PlayerUses(Role.Helm, -1))
            {
                assignments |= 1 << commander.Slot;
                if (!commander.Assign(Role.Helm)) commander.ClearJob();
            }

            if (!soloGunner && (water.ActiveLeaks > 0 || water.WaterLevel01 > 0f))
            {
                int firstRepair = -1;
                for (int i = 0; i < ship.Rig.LeakInside.Length; i++)
                {
                    if (!water.IsLeaking(i) || !Staff(Role.Leak, i, CrewDuty.DamageControl, ship.Rig.LeakInside[i])) continue;
                    firstRepair = i;
                    break;
                }
                bool needsPump = water.ActiveLeaks == 0 || water.WaterLevel01 >= .025f || PumpIsStaffed();
                if (needsPump)
                {
                    int pumps = water.WaterLevel01 > .35f ? ship.Rig.Pumps.Length : 1;
                    for (int i = 0; i < pumps; i++)
                        Staff(Role.Pump, i, CrewDuty.DamageControl, ship.Rig.Pumps[i]);
                }
                for (int i = 0; i < ship.Rig.LeakInside.Length; i++)
                    if (i != firstRepair && water.IsLeaking(i))
                        Staff(Role.Leak, i, CrewDuty.DamageControl, ship.Rig.LeakInside[i]);
            }
            // Walking routes use physics queries against the moving deck.
            if (bestGun != GunWork.Unavailable) Physics.SyncTransforms();
            if (bestGun == GunWork.Ready) StaffGuns(GunWork.Ready);
            if (bestGun >= GunWork.Reload) StaffGuns(GunWork.Reload);
            if (bestGun >= GunWork.Standby) StaffGuns(GunWork.Standby);
            if (commander && (assignments & 1 << commander.Slot) == 0 && !PlayerUses(Role.Helm, -1))
            {
                commander.Assign(Role.Helm);
                assignments |= 1 << commander.Slot;
            }
            foreach (var mate in mates)
                if (mate.Job.HasValue && (assignments & 1 << mate.Slot) == 0) mate.ClearJob();
        }

        bool HasSingleAvailableMate()
        {
            int count = 0;
            foreach (var mate in mates)
            {
                if (!mate.isActiveAndEnabled || mate.Health <= 0) continue;
                if (++count == 2) return false;
            }
            return count == 1;
        }

        GunWork PrepareGuns(bool opposing)
        {
            GunWork best = GunWork.Unavailable;
            for (int i = 0; i < gunWork.Length; i++)
            {
                reservedGuns[i] = false;
                var work = GunWork.Unavailable;
                if (opposing && ship.Rig.Guns[i] && !PlayerUses(Role.Gun, i) && !ship.IsGunSubmerged(i))
                {
                    if (TryAim(i, out _)) work = ship.IsLoaded(i) ? GunWork.Ready : GunWork.Reload;
                    else if (GunSide(i) == side) work = GunWork.Standby;
                }
                gunWork[i] = work;
                if (work > best) best = work;
            }
            return best;
        }

        int GunSide(int index) => ship.Definition.Data.guns[index].forward.x > 0f ? 1 : -1;

        bool CanAssign(CrewMate mate) => mate.isActiveAndEnabled && mate.Health > 0 && (assignments & 1 << mate.Slot) == 0;

        void StaffGuns(GunWork work)
        {
            // Honour an operator already at, or on the way to, a gun of this priority.
            foreach (var mate in mates)
            {
                if (!CanAssign(mate) || mate.Job != Role.Gun ||
                    gunWork[mate.JobIndex] != work || reservedGuns[mate.JobIndex]) continue;
                reservedGuns[mate.JobIndex] = true;
                assignments |= 1 << mate.Slot;
            }

            // Only vacancies need a route search. A gun directly overhead can be a long walk via the stairs.
            gunCandidates.Clear();
            for (int gun = 0; gun < gunWork.Length; gun++)
            {
                if (gunWork[gun] != work || reservedGuns[gun]) continue;
                foreach (var mate in mates)
                {
                    if (!CanAssign(mate)) continue;
                    float distance = paths.WalkingDistance(mate.transform.localPosition, gunFeet[gun]);
                    if (float.IsPositiveInfinity(distance)) continue;
                    // The nearest hand takes a live shot; routine loading and watch go to gunners first.
                    int dutyOrder = work == GunWork.Ready || mate.Duty == CrewDuty.Gunner ? 0 : 1;
                    gunCandidates.Add((dutyOrder, distance, mate, gun));
                }
            }
            gunCandidates.Sort((a, b) => a.dutyOrder != b.dutyOrder
                ? a.dutyOrder.CompareTo(b.dutyOrder) : a.distance.CompareTo(b.distance));
            foreach (var candidate in gunCandidates)
            {
                if (reservedGuns[candidate.gun] || !CanAssign(candidate.mate)) continue;
                if (!candidate.mate.Assign(Role.Gun, candidate.gun)) continue;
                reservedGuns[candidate.gun] = true;
                assignments |= 1 << candidate.mate.Slot;
            }
        }

        bool PumpIsStaffed()
        {
            foreach (var mate in mates)
                if (mate.Job == Role.Pump && mate.Health > 0) return true;
            return false;
        }

        bool Staff(Role role, int index, CrewDuty preferred, Transform station)
        {
            if (!station || PlayerUses(role, index)) return false;
            int tried = assignments;
            while (true)
            {
                CrewMate best = null;
                float bestScore = float.PositiveInfinity;
                foreach (var mate in mates)
                {
                    if ((tried & 1 << mate.Slot) != 0 || mate.Health <= 0) continue;
                    float score = Vector3.Distance(mate.transform.position, station.position);
                    if (mate.Duty == preferred) score -= 100f;
                    if (mate.Job == role && mate.JobIndex == index) score -= 60f;
                    if (score >= bestScore) continue;
                    best = mate;
                    bestScore = score;
                }
                if (!best) return false;
                int bit = 1 << best.Slot;
                if (best.Assign(role, index))
                {
                    assignments |= bit;
                    return true;
                }
                tried |= bit;
            }
        }

        void Navigate(Vector3 delta, bool hold)
        {
            float distance = delta.magnitude;
            int sail = hold ? 0 : distance > 64f ? 2 : 1;
            if (ship.SailStep != sail) ship.ChangeSail(sail - ship.SailStep);
            if (!ship.Anchored) hasSailed = true;
            float bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float offset = Mathf.Clamp(90f + (41f - distance) * 1.5f, 15f, 145f);
            if (hold) ship.SetHelm(0f);
            else SetCourse(bearing - side * offset);
        }

        void SetCourse(float heading)
        {
            float turn = Mathf.DeltaAngle(ship.Course, heading);
            ship.SetHelm(Mathf.Clamp(turn / 15f, -1f, 1f));
        }

        void RelinquishWheel()
        {
            if (!aiAtWheel) return;
            ship.SetHelm(0f);
            aiAtWheel = null;
        }

        internal void FireReadyGun(CrewMate gunner)
        {
            int index = gunner.JobIndex;
            if (PlayerUses(Role.Gun, index) || !TryAim(index, out var angles)) return;
            if (!ship.Fire(index, angles.x, angles.y, gunner)) return;
            shotsByGun[index]++;
            ShotsFired++;
            LastShotSide = GunSide(index);
            nextPlan = 0f;
        }

        internal bool TryAim(int gunIndex, out Vector2 angles)
        {
            angles = default;
            if (!enemy || enemy.Flooding.IsSunk || aimBandByGun == null ||
                gunIndex < 0 || gunIndex >= aimBandByGun.Length) return false;
            Transform gun = ship.Rig.Guns[gunIndex];
            if (!gun) return false;

            Vector3 targetLocal = enemy.transform.InverseTransformPoint(gun.position);
            ShipDefinition hull = enemy.Definition;
            Vector3 bore = enemy.transform.InverseTransformDirection(gun.forward);
            float crossing = Mathf.Abs(bore.x) > .1f ? -targetLocal.x / bore.x : 0f;
            float projectedZ = targetLocal.z + bore.z * Mathf.Max(0f, crossing);
            float z = Mathf.Clamp(projectedZ, .18f * hull.Length, .82f * hull.Length);
            float targetSide = Mathf.Sign(targetLocal.x);
            bool hasRaisedQuarterdeck = hull.Type == ShipType.Brigantine;
            // Open with two ordinary hull shots. Count launches so ready guns never wait for one another.
            bool quarterdeckFirst = hasRaisedQuarterdeck && ShotsFired % QuarterdeckShotInterval == 2;
            if (quarterdeckFirst && TryAimAtQuarterdeck(gun, targetSide, projectedZ, out angles)) return true;

            // The top band follows the planking alongside the deck, even as the waterline rises.
            float high = Mathf.Max(hull.Data.mainDeck - .05f, hull.Data.deckCrown - .20f);
            float low = Mathf.Min(high - .60f, Mathf.Max(hull.DryDraft + .65f, enemy.Draft - .50f));
            int preferredBand = (aimBandByGun[gunIndex] + shotsByGun[gunIndex]) % AimBandCount;
            int direction = preferredBand == AimBandCount - 1 ? -1 : 1;
            for (int attempt = 0; attempt < AimBandCount; attempt++)
            {
                // If the preferred height is outside the traverse, try the remaining heights.
                int band = (preferredBand + direction * attempt + AimBandCount) % AimBandCount;
                float height = Mathf.Lerp(low, high, (float)band / (AimBandCount - 1));
                if (TryAimAtHullSection(gun, targetSide, z, height, out angles)) return true;
            }
            return hasRaisedQuarterdeck && !quarterdeckFirst && TryAimAtQuarterdeck(gun, targetSide, projectedZ, out angles);
        }

        bool TryAimAtQuarterdeck(Transform gun, float sideSign, float projectedZ, out Vector2 angles)
        {
            // On the brigantine R06 the upper planking below the quarterdeck spans this aft section.
            var hull = enemy.Definition;
            float z = Mathf.Clamp(projectedZ, .08f * hull.Length, .12f * hull.Length);
            return TryAimAtHullSection(gun, sideSign, z, hull.Data.maxHullHeight - .20f, out angles);
        }

        bool TryAimAtHullSection(Transform gun, float sideSign, float z, float height, out Vector2 angles)
        {
            float skin = enemy.Definition.HalfBreadth(z, height);
            if (skin > 0f)
                return TryAimAtTarget(gun, enemy.transform.TransformPoint(sideSign * skin, height, z), out angles);
            angles = default;
            return false;
        }

        bool TryAimAtTarget(Transform gun, Vector3 target, out Vector2 angles)
        {
            angles = default;
            Vector3 offset = target - gun.position;
            float rangeSquared = NavalShip.MaxShotRange * NavalShip.MaxShotRange;
            if (new Vector2(offset.x, offset.z).sqrMagnitude > rangeSquared) return false;
            Vector3 targetMotion = enemy.transform.forward * enemy.Speed;
            Vector3 relativeMotion = targetMotion - ship.transform.forward * ship.Speed;
            float travel = 0f;
            Vector3 sightLine = offset;
            // Refine the travel time against the same motion used by the in-game projectile.
            // A level-distance estimate alone puts long shots short of the selected strip of hull.
            for (int i = 0; i < 8; i++)
            {
                float next = Mathf.Max(0f, sightLine.magnitude - NavalShip.ShotSpawnOffset) / NavalShip.ShotSpeed;
                float difference = Mathf.Abs(next - travel);
                travel = next;
                sightLine = offset + relativeMotion * travel;
                sightLine.y += .5f * NavalShip.ShotDrop * travel * travel;
                if (difference < .0005f) break;
            }
            Vector3 arrival = offset + targetMotion * travel;
            if (new Vector2(arrival.x, arrival.z).sqrMagnitude > rangeSquared) return false;
            Vector3 local = gun.InverseTransformDirection(sightLine);
            angles = new Vector2(Mathf.Atan2(local.x, local.z), Mathf.Atan2(local.y,
                Mathf.Sqrt(local.x * local.x + local.z * local.z))) * Mathf.Rad2Deg;
            return Mathf.Abs(angles.x) <= NavalShip.MaxGunYaw &&
                angles.y >= NavalShip.MinGunPitch && angles.y <= NavalShip.MaxGunPitch;
        }

        void OnDisable()
        {
            RelinquishWheel();
            foreach (var mate in mates)
                if (mate && mate.Job.HasValue) mate.ClearJob();
            nextPlan = 0f;
        }
    }
}
