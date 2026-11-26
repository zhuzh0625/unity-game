using UnityEngine;
using UnityEngine.Pool;

namespace Galleon
{
    [DefaultExecutionOrder(-150)]
    public sealed partial class NavalShip : MonoBehaviour
    {
        internal const float MaxGunYaw = 30f;
        internal const float MinGunPitch = -15f;
        internal const float MaxGunPitch = 35f;
        internal const float MaxShotRange = 500f;
        internal const float ShotSpeed = 70.7f;
        internal const float ShotDrop = 8f;
        internal const float ShotSpawnOffset = 1.4f;

        public ShipDefinition Definition { get; private set; }

        public GalleonRig Rig { get; private set; }
        public ShipCompany Company { get; internal set; }
        public HullFlooding Flooding { get; private set; }
        public int SailStep { get; private set; }
        public bool Anchored => SailStep == 0;
        public float Speed { get; private set; }
        public float Rudder { get; private set; }
        public float Course { get; private set; }
        public bool IsPirate { get; private set; }
        public int HitsLanded { get; private set; }
        public float Draft
        {
            get
            {
                if (!Flooding) return Definition.DryDraft;
                // The bilge starts below the sea. By the first gun deck, inside water and
                // outside mean sea level must meet at the ports, including as the ship refloats.
                float height = Flooding.WaterHeight;
                float bilgeFill = Mathf.InverseLerp(Flooding.DryHeight, Definition.EqualizationHeight, height);
                return height + (Definition.DryDraft - Flooding.DryHeight) * (1f - bilgeFill);
            }
        }

        bool[] loaded;
        Vector2 centre; // World X/Z of the amidships pivot.
        float requestedHelm;
        float sailDisplay;
        float wavePhase;

        public void Initialize(bool pirate, Vector3 centreAtSea, float course, ShipType type = ShipType.Sloop)
        {
            Definition = ShipDefinition.For(type);
            IsPirate = pirate;
            wavePhase = pirate ? 0f : 2.2f;
            PlaceAtSea(centreAtSea, course, 0f);
            Rig = GalleonRig.Create(transform, pirate, Definition);
            footprint = HullFootprint.For(Rig);
            loaded = new bool[Rig.Guns.Length];
            Flooding = gameObject.AddComponent<HullFlooding>();
            Flooding.Initialize(Rig);
            Flooding.Sunk += SettleOnSinking;
            Rig.SetSail(sailDisplay);
        }

        public void SetHelm(float input)
        {
            if (!Flooding.IsSunk) requestedHelm = Mathf.Clamp(input, -1f, 1f);
        }

        public void ChangeSail(int direction)
        {
            SetSailStep(SailStep + System.Math.Sign(direction));
        }

        internal void SetSailStep(int step)
        {
            if (!Flooding.IsSunk) SailStep = Mathf.Clamp(step, 0, 2);
        }

        bool HasGun(int index) => loaded != null && index >= 0 && index < loaded.Length && Rig.Guns[index];
        public bool IsLoaded(int index) => HasGun(index) && loaded[index];

        public bool IsGunSubmerged(int index)
        {
            // R13 gun markers are measured on the barrel axis in the moving ship.
            return HasGun(index) && Flooding.WaterHeight >= Rig.transform.InverseTransformPoint(Rig.Guns[index].position).y;
        }

        public void Load(int index)
        {
            if (HasGun(index) && !Flooding.IsSunk && !IsGunSubmerged(index))
                loaded[index] = true;
        }

        public static Vector3 ShotDirection(Transform gun, float yaw, float pitch)
        {
            Vector3 lateral = Quaternion.AngleAxis(Mathf.Clamp(yaw, -MaxGunYaw, MaxGunYaw), gun.up) * gun.forward;
            return Quaternion.AngleAxis(-Mathf.Clamp(pitch, MinGunPitch, MaxGunPitch), Vector3.Cross(gun.up, lateral).normalized) * lateral;
        }

        internal Vector3 GunSightPosition(Transform gun, Vector3 direction)
        {
            // Gun markers sit at different depths behind the ports on each model. Follow the
            // sight line to the hull skin, including on the bow and stern chase guns.
            Transform hull = Rig.transform;
            Vector3 origin = hull.InverseTransformPoint(gun.position);
            Vector3 heading = hull.InverseTransformDirection(direction);
            float inside = 0f;
            float outside = Definition.Length + Definition.Beam;
            for (int i = 0; i < 12; i++)
            {
                float distance = (inside + outside) * .5f;
                Vector3 point = origin + heading * distance;
                if (Mathf.Abs(point.x) < Definition.HalfBreadth(point.z, point.y)) inside = distance;
                else outside = distance;
            }
            // Keep the near plane past the static barrel lip and the gunport lining.
            const float portClearance = .5f;
            return gun.position + direction * (outside + portClearance);
        }

        public bool Fire(int index, float yawDegrees, float pitchDegrees, CrewDamageTarget gunner = null)
        {
            if (!IsLoaded(index) || Flooding.IsSunk || IsGunSubmerged(index)) return false;
            Transform gun = Rig.Guns[index];
            Vector3 direction = ShotDirection(gun, yawDegrees, pitchDegrees);
            loaded[index] = false;
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Round shot";
            ball.transform.position = gun.position + direction * ShotSpawnOffset;
            ball.transform.localScale = Vector3.one * .32f;
            ball.GetComponent<Collider>().enabled = false;
            ball.GetComponent<MeshRenderer>().sharedMaterial = NavalWorld.ShotMaterial;
            ball.AddComponent<RoundShot>().Launch(this, direction * ShotSpeed + transform.forward * Speed, gunner, gun.position);
            NavalAudio.Effect(NavalSound.Cannon, gun.position, this);
            NavalAudio.Voice(gunner, SailorLine.Attack);
            return true;
        }

        internal void RegisterHit() => HitsLanded++;

        void FinishMotion(float dt)
        {
            ApplyPose(Time.time);
            Rig.SetWheel(Rudder);
            float sailTarget = SailStep * .5f;
            if (!Mathf.Approximately(sailDisplay, sailTarget))
            {
                sailDisplay = Mathf.MoveTowards(sailDisplay, sailTarget, dt * .7f);
                Rig.SetSail(sailDisplay);
            }
        }

        void ApplyPose(float time)
        {
            float t = time + wavePhase;
            float draft = Draft;
            // As the remaining freeboard disappears, waves no longer lift the deck clear.
            float waveResponse = Mathf.Clamp01((Definition.SinkingHeight - draft) / .65f);
            float pitch = Mathf.Sin(t * .65f + centre.y * .05f) * .48f * waveResponse;
            float roll = (Mathf.Sin(t * .50f + centre.x * .07f) * 1.05f + Rudder * Mathf.Min(Speed, 5.8f) * .12f) * waveResponse;
            Quaternion rotation = Quaternion.Euler(pitch, Course, roll);
            float heave = Mathf.Sin(t * .63f + centre.y * .06f) * .095f * waveResponse;
            Vector3 amidships = new Vector3(centre.x, -draft + heave, centre.y);
            transform.SetPositionAndRotation(amidships - rotation * new Vector3(0, 0, Definition.PivotZ), rotation);
        }

        void SettleOnSinking()
        {
            Speed = driveSpeed = Rudder = requestedHelm = 0f;
            ApplyPose(Time.time);
            Rig.SetWheel(0f);
        }

        void OnDestroy()
        {
            if (Flooding) Flooding.Sunk -= SettleOnSinking;
        }
    }

    public sealed class RoundShot : MonoBehaviour
    {
        internal const float InnerRadius = 1.8f;
        internal const float OuterRadius = 3f;
        // Both playable hulls remain above this depth until the voyage ends.
        const float MaxUnderwaterDepth = 6f;
        const float MaxFlightSeconds = 15f;
        // Allow for floating point error at tangency when comparing world-space metres.
        const float BoundaryTolerance = .00001f;
        NavalShip source;
        CrewDamageTarget gunner;
        NavalWorld world;
        Vector3 velocity;
        Vector2 rangeOrigin;
        float age;
        bool firingPirate, splashed;

        internal void Launch(NavalShip ship, Vector3 initialVelocity, CrewDamageTarget gunner = null, Vector3? gunPosition = null)
        {
            source = ship;
            this.gunner = gunner;
            world = ship.GetComponentInParent<NavalWorld>();
            NavalShip allegiance = gunner ? gunner.AllegianceShip : null;
            firingPirate = allegiance ? allegiance.IsPirate : ship.IsPirate;
            velocity = initialVelocity;
            Vector3 origin = gunPosition ?? transform.position;
            rangeOrigin = new Vector2(origin.x, origin.z);
        }

        void Update()
        {
            // Returning to the fleet retires both the ships and their airborne rounds.
            if (!source || !source.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 start = transform.position;
            Vector2 fromGun = new Vector2(start.x, start.z) - rangeOrigin;
            Vector2 next = fromGun + new Vector2(velocity.x, velocity.z) * dt;
            bool endOfRange = next.sqrMagnitude >= NavalShip.MaxShotRange * NavalShip.MaxShotRange;
            // Clip the last frame before its collision query, including a shot from a moving ship.
            if (endOfRange)
                dt *= Mathf.InverseLerp(fromGun.magnitude, next.magnitude, NavalShip.MaxShotRange);
            Vector3 down = Vector3.down * NavalShip.ShotDrop;
            Vector3 displacement = velocity * dt + down * (.5f * dt * dt);
            velocity += down * dt;
            float distance = displacement.magnitude;
            if (distance > 0f)
            {
                var hits = Physics.RaycastAll(start, displacement / distance, distance, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    NavalShip struckShip = null;
                    var crew = hit.collider.GetComponentInParent<CrewDamageTarget>();
                    if (crew)
                    {
                        // Only the hitbox follows the visible sailor at a station or on a ladder.
                        if (!hit.collider.isTrigger || crew.Health <= 0) continue;
                    }
                    else
                    {
                        if (hit.collider.isTrigger) continue;
                        struckShip = hit.collider.GetComponentInParent<NavalShip>();
                        if (!struckShip || struckShip == source) continue;
                    }

                    PlayWaterEntry(start, hit.point);
                    SailorLine result = DamageCrewAt(hit.point, gunner, firingPirate);
                    if (struckShip)
                    {
                        NavalAudio.Effect(NavalSound.HullHit, hit.point, struckShip);
                        // A real hull strike can land even with every breach site occupied.
                        if (struckShip.Flooding.Hit(hit.point, hit.normal)) source.RegisterHit();
                        if (result == SailorLine.Miss && struckShip.IsPirate != firingPirate) result = SailorLine.Hit;
                    }
                    Finish(result);
                    return;
                }
            }
            transform.position = start + displacement;
            PlayWaterEntry(start, transform.position);
            age += dt;
            if (endOfRange || transform.position.y < -MaxUnderwaterDepth || age >= MaxFlightSeconds)
                Finish(SailorLine.Miss);
        }

        void PlayWaterEntry(Vector3 start, Vector3 end)
        {
            if (splashed) return;
            float aboveStart = start.y - SurfaceHeight(start);
            float aboveEnd = end.y - SurfaceHeight(end);
            if (aboveStart <= 0f || aboveEnd > 0f) return;
            Vector3 entry = Vector3.Lerp(start, end, aboveStart / (aboveStart - aboveEnd));
            entry.y = SurfaceHeight(entry);
            splashed = true;
            NavalAudio.Effect(NavalSound.Splash, entry, source);
        }

        float SurfaceHeight(Vector3 point) => world ? world.SurfaceHeight(point) : 0f;

        void Finish(SailorLine result)
        {
            if (gunner && gunner.isActiveAndEnabled && gunner.Health > 0)
                NavalAudio.Voice(gunner, result);
            Destroy(gameObject);
        }

        internal static void DamageCrewAt(Vector3 impactPoint) => DamageCrewAt(impactPoint, null, null);

        static SailorLine DamageCrewAt(Vector3 impactPoint, CrewDamageTarget gunner, bool? firingPirate)
        {
            using var lease = DictionaryPool<CrewDamageTarget, int>.Get(out var victims);
            var bodies = Physics.OverlapSphere(impactPoint, OuterRadius + BoundaryTolerance,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            foreach (var body in bodies)
            {
                if (!body.isTrigger) continue;
                var target = body.GetComponentInParent<CrewDamageTarget>();
                if (!target || !target.isActiveAndEnabled || target.Health <= 0) continue;

                float distance = Vector3.Distance(impactPoint, body.ClosestPoint(impactPoint));
                if (distance > OuterRadius + BoundaryTolerance) continue;
                int amount = distance <= InnerRadius + BoundaryTolerance ? 100 : 50;
                if (!victims.TryGetValue(target, out int previous) || amount > previous)
                    victims[target] = amount;
            }
            // Resolve the whole impact before any sailor falls, releases a station or moves a hurtbox.
            SailorLine result = SailorLine.Miss;
            foreach (var victim in victims)
            {
                CrewDamageTarget target = victim.Key;
                int previous = target.Health;
                target.TakeWeaponDamage(victim.Value, gunner, CrewKillWeapon.Cannon);
                NavalShip allegiance = target.AllegianceShip;
                if (!firingPirate.HasValue || (allegiance && allegiance.IsPirate == firingPirate.Value) ||
                    target.Health >= previous) continue;
                if (target.Health == 0) result = SailorLine.Kill;
                else if (result == SailorLine.Miss) result = SailorLine.Hit;
            }
            return result;
        }
    }
}
