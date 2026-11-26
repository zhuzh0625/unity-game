using UnityEngine;

namespace Galleon
{
    public sealed class CombatDummy : CrewDamageTarget
    {
        const float RecoverySeconds = 10f;
        readonly CrewHealth life = new();
        SailorModel sailor;
        float recoverAt;

        public override int Health => life.Current;
        public Vector3 AimPoint => transform.TransformPoint(Vector3.up * 1.12f);

        internal static CombatDummy Create(Transform deck, string label, Vector3 foot)
        {
            var dummy = new GameObject(label).AddComponent<CombatDummy>();
            dummy.transform.SetParent(deck, false);
            dummy.transform.SetLocalPositionAndRotation(foot, Quaternion.Euler(0f, 180f, 0f));
            dummy.sailor = new SailorModel(dummy.transform);
            dummy.life.Restore();
            return dummy;
        }

        public override void TakeDamage(int amount)
        {
            if (!life.Damage(amount) || Health > 0) return;
            sailor.LieDown(transform.forward, transform.up);
            recoverAt = Time.time + RecoverySeconds;
        }

        void Update()
        {
            if (Health == 0)
            {
                if (Time.time < recoverAt) return;
                life.Restore();
            }
            sailor.Pose("Idle", Time.time * .24f, Time.deltaTime);
        }
    }
}
