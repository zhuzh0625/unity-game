using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    public sealed partial class CrewPlayer
    {
        public const int PistolDamage = 50, CutlassDamage = 25;
        const float ReloadSeconds = 5f, PistolRange = 60f, CutlassReach = 1.8f;
        const int MedicineHealing = 50;
        const float MedicineSeconds = 1f;
        // Frames 14 through 22 of the authored clip hold the raised gun. The shot is at frame 22.
        const float PistolAimPhase = 14f / 36f, PistolFirePhase = 22f / 36f;
        const float PistolRecoilSeconds = .20f;
        // Both v3 clips sweep through the front between frames 6 and 35 of the 45-frame interval.
        const float SlashContactPhase = 20.5f / 45f;
        readonly HashSet<CrewDamageTarget> slashVictims = new();
        string attackClip;
        float attackTime, attackLength, pistolCooldown;
        float pistolRaisePhase, pistolRecoil;
        double medicineElapsed = -1d;
        bool appliedStrike, queuedSlash, nextSlashRight;

        public CrewWeapon Weapon { get; private set; }
        public bool IsPistolAiming { get; private set; }
        public bool IsAttacking => attackClip != null || pistolRecoil > 0f;
        public bool IsDrinkingMedicine => medicineElapsed >= 0d;
        public float PistolCooldownRemaining => pistolCooldown;

        void SetWeapon(CrewWeapon weapon)
        {
            if (Weapon == weapon) return;
            Weapon = weapon;
            IsPistolAiming = false;
            pistolRaisePhase = pistolRecoil = 0f;
            StopMedicine();
            attackClip = null;
            queuedSlash = nextSlashRight = false;
            sailor.ShowWeapon(weapon);
        }

        void BeginSlash()
        {
            attackClip = nextSlashRight ? "CutlassSlashRight" : "CutlassSlash";
            nextSlashRight = !nextSlashRight;
            attackTime = 0f;
            attackLength = sailor.ClipSeconds(attackClip);
            queuedSlash = appliedStrike = false;
            NavalAudio.Voice(this, SailorLine.Attack);
        }

        bool TickCombat(CrewCommand command, float elapsed, float dt)
        {
            if (command.WeaponKey != CrewWeapon.None)
                SetWeapon(Weapon == command.WeaponKey ? CrewWeapon.None : command.WeaponKey);
            if (Weapon == CrewWeapon.Pistol) return TickPistol(command, elapsed, dt);
            if (Weapon == CrewWeapon.Cutlass)
            {
                sailor.Face(yaw, false, dt);
                TickCutlass(command, elapsed);
            }
            else if (Weapon == CrewWeapon.Medicine) TickMedicine(command.AttackPressed, elapsed);
            return false;
        }

        void TickMedicine(bool pressed, float elapsed)
        {
            if (!IsDrinkingMedicine)
            {
                if (pressed)
                {
                    medicineElapsed = 0d;
                    NavalAudio.SetAction(this, NavalSound.Medicine, true);
                }
                return;
            }

            medicineElapsed += elapsed;
            // Fixed frame steps supplied as floats can sum to just under one second.
            if (medicineElapsed < MedicineSeconds && !Mathf.Approximately((float)medicineElapsed, MedicineSeconds)) return;
            StopMedicine();
            life.Heal(MedicineHealing);
        }

        void StopMedicine()
        {
            if (!IsDrinkingMedicine) return;
            medicineElapsed = -1d;
            NavalAudio.SetAction(this, NavalSound.Medicine, false);
        }

        bool TickPistol(CrewCommand command, float elapsed, float dt)
        {
            pistolCooldown = Mathf.Max(0f, pistolCooldown - elapsed);
            pistolRecoil = Mathf.Max(0f, pistolRecoil - elapsed);
            if (command.AimPressed)
            {
                IsPistolAiming = !IsPistolAiming;
                pistolRecoil = 0f;
            }
            pistolRaisePhase = Mathf.MoveTowards(pistolRaisePhase, IsPistolAiming ? PistolAimPhase : 0f,
                elapsed / sailor.PistolClipSeconds);
            if (!IsPistolAiming) return false;

            // A toggle and a trigger in the same frame only toggle. Holding the trigger never queues a shot.
            bool fire = !command.AimPressed && command.AttackPressed && pistolCooldown <= 0f;
            sailor.Face(yaw, fire, dt);
            if (!fire) return false;
            pistolRaisePhase = PistolAimPhase;
            pistolRecoil = PistolRecoilSeconds;
            pistolCooldown = ReloadSeconds;
            return true;
        }

        void PosePistol(bool moving, float dt)
        {
            float phase = pistolRecoil > 0f ? PistolFirePhase : pistolRaisePhase;
            float kick = 5f * Mathf.Sin(Mathf.PI * pistolRecoil / PistolRecoilSeconds);
            sailor.PosePistol(phase, moving, stride, pistolRecoil == PistolRecoilSeconds ? 1f : dt, pitch - kick);
        }

        void TickCutlass(CrewCommand command, float elapsed)
        {
            if (attackClip == null)
            {
                if (!command.AttackPressed && !command.AttackHeld) return;
                BeginSlash();
            }
            else if (command.AttackPressed) queuedSlash = true;

            // Sample the strike frame itself, even if this rendered frame crosses the event.
            while (elapsed > 0f)
            {
                float step = Mathf.Min(elapsed, attackLength - attackTime);
                float strikeAt = SlashContactPhase * attackLength;
                if (!appliedStrike && attackTime + step >= strikeAt)
                {
                    appliedStrike = true;
                    sailor.Pose(attackClip, SlashContactPhase, 1f);
                    Physics.SyncTransforms();
                    StrikeCutlass();
                }
                attackTime += step;
                elapsed -= step;
                if (attackTime < attackLength) return;
                attackClip = null;
                if (!(command.AttackHeld || queuedSlash)) return;
                BeginSlash();
            }
        }

        void FirePistol()
        {
            UpdateCamera(0f);
            Ray sight = view.ViewportPointToRay(new Vector3(.5f, .5f));
            float sightReach = PistolRange + Vector3.Distance(sight.origin, sailor.Foot);
            Vector3 aim = TraceShot(sight, sightReach, out var sightHit, out _) ? sightHit.point : sight.GetPoint(sightReach);
            Vector3 muzzle = sailor.PistolMuzzle;
            Vector3 toAim = aim - muzzle;
            Vector3 direction = toAim.normalized;
            NavalAudio.Effect(NavalSound.Pistol, muzzle, AllegianceShip);
            NavalAudio.Voice(this, SailorLine.Attack);
            PistolSmoke.Emit(transform, muzzle, Vector3.Dot(direction, sight.direction) > 0f ? direction : sight.direction);

            // A camera looking around cover cannot fire through it, even with the muzzle pressed against it.
            Vector3 chest = sailor.Foot + transform.up * 1.25f;
            Vector3 toMuzzle = muzzle - chest;
            if (TraceShot(new Ray(chest, toMuzzle), toMuzzle.magnitude, out _, out var target))
            {
                if (target) ReportWeaponImpact(ResolveWeaponImpact(target, PistolDamage, CrewKillWeapon.Pistol));
                return;
            }
            if (Vector3.Dot(toAim, sight.direction) <= 0f) return;
            if (TraceShot(new Ray(muzzle, direction), Mathf.Min(PistolRange, toAim.magnitude + .04f), out _, out target) && target)
                ReportWeaponImpact(ResolveWeaponImpact(target, PistolDamage, CrewKillWeapon.Pistol));
        }

        SailorLine? ResolveWeaponImpact(CrewDamageTarget target, int amount, CrewKillWeapon weapon)
        {
            var allegiance = target.AllegianceShip;
            bool opposing = !allegiance || allegiance.IsPirate != AllegianceShip.IsPirate;
            int previousHealth = target.Health;
            target.TakeWeaponDamage(amount, this, weapon);
            if (!opposing || target.Health >= previousHealth) return null;
            return target.Health == 0 ? SailorLine.Kill : SailorLine.Hit;
        }

        void ReportWeaponImpact(SailorLine? outcome)
        {
            if (outcome.HasValue) NavalAudio.Voice(this, outcome.Value);
        }

        bool TraceShot(Ray ray, float distance, out RaycastHit contact, out CrewDamageTarget victim)
        {
            contact = default;
            victim = null;
            float closest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray, distance, Physics.AllLayers, QueryTriggerInteraction.Collide))
            {
                if (hit.distance >= closest) continue;
                var target = hit.collider.GetComponentInParent<CrewDamageTarget>();
                // The trigger follows the visible body. The upright movement capsule can be elsewhere at a station.
                if (target)
                {
                    if (target == this || target.Health <= 0 || !hit.collider.isTrigger) continue;
                }
                else if (hit.collider.isTrigger) continue;
                closest = hit.distance;
                contact = hit;
                victim = target;
            }
            return contact.collider;
        }

        void StrikeCutlass()
        {
            Vector3 up = transform.up;
            Vector3 origin = sailor.Foot + up * 1.05f;
            Vector3 forward = Vector3.ProjectOnPlane(sailor.Forward, up).normalized;
            NavalAudio.Effect(NavalSound.Cutlass, origin, AllegianceShip);
            slashVictims.Clear();
            SailorLine? outcome = null;
            foreach (var body in Physics.OverlapSphere(origin, CutlassReach, Physics.AllLayers, QueryTriggerInteraction.Collide))
            {
                if (!body.isTrigger) continue;
                var target = body.GetComponentInParent<CrewDamageTarget>();
                if (!target || target == this || target.Health <= 0 || slashVictims.Contains(target)) continue;
                Vector3 toBody = body.bounds.center - origin;
                Vector3 horizontal = Vector3.ProjectOnPlane(toBody, up);
                if (Mathf.Abs(Vector3.Dot(toBody, up)) > 1.15f || Vector3.Dot(forward, horizontal) < .4f * horizontal.magnitude) continue;
                if (Physics.Linecast(origin, body.ClosestPoint(origin), WorldMask, QueryTriggerInteraction.Ignore)) continue;
                slashVictims.Add(target);
                var impact = ResolveWeaponImpact(target, CutlassDamage, CrewKillWeapon.Cutlass);
                if (impact == SailorLine.Kill || !outcome.HasValue) outcome = impact;
            }
            ReportWeaponImpact(outcome);
        }
    }
}
