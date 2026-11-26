using UnityEngine;

namespace Galleon
{
    internal enum CrewKillWeapon { Pistol, Cutlass, Cannon }

    internal readonly struct CrewKill
    {
        internal CrewDamageTarget Attacker { get; }
        internal CrewDamageTarget Victim { get; }
        internal CrewKillWeapon Weapon { get; }

        internal CrewKill(CrewDamageTarget attacker, CrewDamageTarget victim, CrewKillWeapon weapon)
        {
            Attacker = attacker;
            Victim = victim;
            Weapon = weapon;
        }
    }

    public sealed class CrewHealth
    {
        public const int Maximum = 100;
        public int Current { get; private set; } = Maximum;

        public bool Damage(int amount)
        {
            if (amount <= 0 || Current == 0) return false;
            Current = Mathf.Max(0, Current - amount);
            return true;
        }

        internal void Heal(int amount)
        {
            if (amount <= 0 || Current == 0) return;
            Current += Mathf.Min(amount, Maximum - Current);
        }

        public void Restore() => Current = Maximum;
    }

    public abstract class CrewDamageTarget : MonoBehaviour
    {
        internal const int VoiceCount = 3;
        bool repairAudioActive;

        internal event System.Action<CrewKill> Killed;
        internal int VoiceIndex { get; private set; } = -1;
        internal virtual NavalShip AllegianceShip => null;

        public abstract int Health { get; }
        public abstract void TakeDamage(int amount);

        protected void AssignVoice(int rosterIndex)
        {
            if (VoiceIndex < 0) VoiceIndex = rosterIndex % VoiceCount;
        }

        protected void SetRepairAudio(bool active)
        {
            if (active == repairAudioActive) return;
            repairAudioActive = active;
            NavalAudio.SetAction(this, NavalSound.Repair, active);
        }

        protected virtual void OnDisable() => SetRepairAudio(false);

        internal void TakeWeaponDamage(int amount, CrewDamageTarget attacker, CrewKillWeapon weapon)
        {
            // A projectile retains its gunner even if that sailor dies before impact.
            bool canAttribute = amount > 0 && !ReferenceEquals(attacker, null) && Health > 0;
            TakeDamage(amount);
            if (canAttribute && Health == 0)
                Killed?.Invoke(new CrewKill(attacker, this, weapon));
        }
    }
}
