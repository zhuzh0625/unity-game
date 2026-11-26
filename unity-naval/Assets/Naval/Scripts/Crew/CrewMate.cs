using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    internal enum CrewDuty { Captain, Gunner, DamageControl }

    public sealed class CrewMate : CrewDamageTarget
    {
        internal const float WalkSpeed = 2.6f;
        readonly CrewHealth life = new();
        readonly List<Vector3> route = new();
        NavalShip ship;
        CrewDeckPaths paths;
        SailorModel sailor;
        Vector3 workFoot, workFacing;
        int step;
        float phase;
        double progress, respawnAt;
        string workClip;

        internal CrewDuty Duty { get; private set; }
        internal int Slot { get; private set; }
        internal CrewPlayer.Role? Job { get; private set; }
        internal int JobIndex { get; private set; } = -1;
        internal override NavalShip AllegianceShip => ship;
        internal bool AtPost => Health > 0 && Job.HasValue && step >= route.Count &&
            (transform.localPosition - workFoot).sqrMagnitude < .025f * .025f;
        public override int Health => life.Current;

        internal void Initialize(NavalShip ship, CrewDeckPaths paths, int slot, CrewDuty duty, Vector3 localSpawn, int rosterIndex)
        {
            this.ship = ship;
            this.paths = paths;
            Slot = slot;
            Duty = duty;
            AssignVoice(rosterIndex);
            transform.SetParent(ship.transform, false);
            transform.localPosition = localSpawn;
            transform.localRotation = Quaternion.identity;
            gameObject.layer = 2; // The combat queries include this layer, camera and station queries exclude it.
            sailor = new SailorModel(transform);
            phase = Mathf.Repeat(slot * .37f, 1f);
        }

        internal bool Assign(CrewPlayer.Role role, int index = -1)
        {
            if (!ship || ship.Flooding.IsSunk || Health <= 0) return false;
            if (role == Job && index == JobIndex) return true;
            var rig = ship.Rig;
            var target = role switch
            {
                CrewPlayer.Role.Helm => rig.Helm,
                CrewPlayer.Role.Pump => At(rig.Pumps, index),
                CrewPlayer.Role.Gun => At(rig.Guns, index),
                CrewPlayer.Role.Leak => At(rig.LeakInside, index),
                _ => null
            };
            if (!target) return false;
            Physics.SyncTransforms();
            if (!CrewStationFit.TryGetPose(ship, role, index, target, transform.position,
                out var worldFoot, out var worldFacing)) return false;
            var foot = ship.transform.InverseTransformPoint(worldFoot);
            if (!paths.TryRoute(transform.localPosition, foot, route)) return false;
            ReleaseWork();
            Job = role;
            JobIndex = index;
            workFoot = foot;
            workFacing = ship.transform.InverseTransformDirection(worldFacing);
            bool small = ship.Definition.Type == ShipType.Sloop;
            workClip = role switch
            {
                CrewPlayer.Role.Helm => small ? "HelmSmall" : "HelmMedium",
                CrewPlayer.Role.Leak => "Repair",
                CrewPlayer.Role.Pump => small ? "PumpSmall" : "PumpMedium",
                CrewPlayer.Role.Gun when small => "GunSmall",
                CrewPlayer.Role.Gun when foot.y > ship.Definition.Data.lowerDeck + 1f => "GunMediumUpper",
                _ => "GunMediumLower"
            };
            progress = phase = 0f;
            step = (transform.localPosition - foot).sqrMagnitude < .0001f ? route.Count : 0;
            return true;
        }

        static Transform At(Transform[] targets, int index) =>
            index >= 0 && index < targets.Length ? targets[index] : null;

        internal float HelmWalkDistance()
        {
            if (!ship || ship.Flooding.IsSunk || Health <= 0) return float.PositiveInfinity;
            Physics.SyncTransforms();
            if (!CrewStationFit.TryGetPose(ship, CrewPlayer.Role.Helm, -1, ship.Rig.Helm, transform.position,
                out var foot, out _)) return float.PositiveInfinity;
            return paths.WalkingDistance(transform.localPosition, ship.transform.InverseTransformPoint(foot));
        }

        void ReleaseWork()
        {
            SetRepairAudio(false);
            if (Job == CrewPlayer.Role.Pump) ship.Rig.SetPumpStroke(JobIndex, 0f);
        }

        void DropJob()
        {
            ReleaseWork();
            Job = null;
            JobIndex = -1;
            route.Clear();
            step = 0;
            progress = 0f;
        }

        internal void ClearJob()
        {
            if (!Job.HasValue) return;
            DropJob();
            if (Health > 0 && !ship.Flooding.IsSunk)
                paths.TryRoute(transform.localPosition, paths.Berth(Slot), route);
        }

        public override void TakeDamage(int amount)
        {
            if (!ship || ship.Flooding.IsSunk || !isActiveAndEnabled || Time.timeScale <= 0f) return;
            if (!life.Damage(amount) || Health != 0) return;
            DropJob();
            Physics.SyncTransforms();
            Vector3 up = ship.transform.up;
            Vector3 forward = sailor.Forward;
            if (Physics.Raycast(sailor.Foot + up * .24f, -up, out var support, .65f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                support.transform.IsChildOf(ship.transform) && Vector3.Dot(support.normal, up) > .35f)
            {
                CrewPlayer.FitOnDeck(ship, ref support, ref forward);
                transform.position = support.point + support.normal * .008f;
                up = support.normal;
            }
            sailor.LieDown(forward, up);
            respawnAt = Time.timeAsDouble + 10.0;
            NavalAudio.Voice(this, SailorLine.Death);
        }

        void Update()
        {
            if (!ship || ship.Flooding.IsSunk)
            {
                SetRepairAudio(false);
                return;
            }
            if (Time.deltaTime <= 0f) return;
            if (Health == 0)
            {
                if (Time.timeAsDouble >= respawnAt && CrewRespawnArea.TryPickLocalFoot(ship, out var foot))
                {
                    transform.localPosition = foot;
                    life.Restore();
                    sailor.Face(0f, true, 0f);
                    sailor.Pose("Idle", 0f, 1f);
                    paths.TryRoute(foot, paths.Berth(Slot), route);
                }
                return;
            }
            if (ship.Company && !ship.Company.enabled) return;
            float dt = Time.deltaTime;
            if (step < route.Count)
            {
                SetRepairAudio(false);
                Walk(dt);
                return;
            }
            if (!AtPost)
            {
                SetRepairAudio(false);
                sailor.Pose("Idle", phase, dt);
                return;
            }
            sailor.Face(Heading(workFacing), true, dt);
            bool working = true;
            switch (Job.Value)
            {
                case CrewPlayer.Role.Gun:
                    if (ship.IsGunSubmerged(JobIndex)) { ClearJob(); return; }
                    working = !ship.IsLoaded(JobIndex);
                    if (working && (progress += dt) >= CrewPlayer.CannonLoadSeconds)
                    {
                        ship.Load(JobIndex);
                        progress = 0f;
                    }
                    if (ship.IsLoaded(JobIndex)) ship.Company.FireReadyGun(this);
                    break;
                case CrewPlayer.Role.Leak:
                    if (!ship.Flooding.IsLeaking(JobIndex)) { ClearJob(); return; }
                    SetRepairAudio(true);
                    if ((progress += dt) >= CrewPlayer.HullRepairSeconds)
                    {
                        ship.Flooding.Repair(JobIndex);
                        ClearJob();
                        return;
                    }
                    break;
                case CrewPlayer.Role.Pump:
                    if (ship.Flooding.ActiveLeaks == 0 && ship.Flooding.WaterLevel01 <= 0f) { ClearJob(); return; }
                    ship.Flooding.Pump(dt);
                    break;
                case CrewPlayer.Role.Helm:
                    working = Mathf.Abs(ship.Rudder) > .02f;
                    break;
            }
            if (working) phase = Mathf.Repeat(phase + dt * 1.35f, 1f);
            sailor.Pose(workClip, phase, dt);
            if (Job == CrewPlayer.Role.Pump) ship.Rig.SetPumpStroke(JobIndex, phase);
        }

        static float Heading(Vector3 local) => Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        void Walk(float dt)
        {
            float speed = Job == CrewPlayer.Role.Helm ? CrewPlayer.RunSpeed : WalkSpeed;
            float remaining = speed * dt;
            while (remaining > 0f && step < route.Count)
            {
                Vector3 current = transform.localPosition;
                Vector3 delta = route[step] - current;
                float distance = delta.magnitude;
                if (distance > .005f) sailor.Face(Heading(delta), false, dt);
                if (distance > remaining)
                {
                    transform.localPosition = current + delta * (remaining / distance);
                    remaining = 0f;
                }
                else
                {
                    transform.localPosition = route[step++];
                    remaining -= distance;
                }
            }
            phase = Mathf.Repeat(phase + speed * dt / 1.6f, 1f);
            sailor.Pose("Run", phase, dt);
        }
    }
}
