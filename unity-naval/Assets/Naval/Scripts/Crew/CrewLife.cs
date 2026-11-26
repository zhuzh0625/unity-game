using UnityEngine;

namespace Galleon
{
    public sealed partial class CrewPlayer
    {
        public const int MaxHealth = CrewHealth.Maximum;
        readonly CrewHealth life = new();
        const float SwimLimit = 20f, RespawnDelay = 10f, SafeLandingSpeed = 10f;
        const float FloatingBodyDraft = .085f;
        static readonly float[] FallAngles = { 0f, 90f, -90f, 180f, 45f, -45f, 135f, -135f };

        double swimStartedAt, respawnAt;
        bool floatingBody;

        public override int Health => life.Current;
        public bool IsDead => movement == Movement.Dead;
        public float SwimSecondsRemaining => IsSwimming ? SecondsUntil(swimStartedAt + SwimLimit) : SwimLimit;
        public float RespawnSecondsRemaining => IsDead ? SecondsUntil(respawnAt) : 0f;

        static float SecondsUntil(double deadline) => Mathf.Max(0f, (float)(deadline - Time.timeAsDouble));

        public override void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead || !homeShip || homeShip.Flooding.IsSunk ||
                !isActiveAndEnabled || Time.timeScale <= 0f) return;
            if (life.Damage(amount) && Health == 0) Fall();
        }

        bool UpdateLife()
        {
            if (IsDead)
            {
                if (floatingBody) FloatBody();
                if (Time.timeAsDouble >= respawnAt && CrewRespawnArea.TryPickLocalFoot(homeShip, out var foot))
                    Respawn(foot);
                return true;
            }
            if (!IsSwimming || Time.timeAsDouble < swimStartedAt + SwimLimit) return false;
            TakeDamage(Health);
            return true;
        }

        void Fall()
        {
            SetWeapon(CrewWeapon.None);
            Vector3 foot = sailor.Foot;
            Vector3 forward = sailor.Forward;
            Vector3 up = transform.up;
            bool wasSwimming = IsSwimming;
            if (occupied) Leave();
            motor.enabled = false;
            Physics.SyncTransforms();

            floatingBody = true;
            if (!wasSwimming && Physics.Raycast(foot + up * .24f, -up, out var support, Mathf.Infinity,
                    WorldMask, QueryTriggerInteraction.Ignore))
            {
                var deck = support.collider.GetComponentInParent<NavalShip>();
                if (deck && Vector3.Dot(support.normal, up) > .35f)
                {
                    ChangeFrame(deck);
                    FitOnDeck(deck, ref support, ref forward);
                    transform.position = support.point + support.normal * .008f;
                    up = support.normal;
                    floatingBody = false;
                }
            }
            if (floatingBody)
            {
                ChangeFrame(null);
                up = Vector3.up;
                FloatBody();
            }

            movement = Movement.Dead;
            ladder = null;
            verticalSpeed = 0f;
            HoldProgress = 0f;
            Prompt = "";
            respawnAt = Time.timeAsDouble + RespawnDelay;
            cameraDistance = 0f;
            sailor.LieDown(forward, up);
            NavalAudio.Voice(this, SailorLine.Death);
        }

        internal static void FitOnDeck(NavalShip deck, ref RaycastHit support, ref Vector3 forward)
        {
            Vector3 up = deck.transform.up;
            Vector3 ahead = Vector3.ProjectOnPlane(forward, up).normalized;
            Vector3 across = Vector3.Cross(up, ahead);
            // Start at the station. Where the sailor is wedged between fittings, allow a short fall to either side.
            for (int ring = 0; ring <= 2; ring++)
            {
                int samples = ring == 0 ? 1 : 8;
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * .25f;
                    Vector3 centre = support.point + ring * .36f * (across * Mathf.Cos(angle) + ahead * Mathf.Sin(angle));
                    if (!HasSupport(deck, centre, up, out var floor)) continue;
                    foreach (float turn in FallAngles)
                    {
                        Vector3 direction = Quaternion.AngleAxis(turn, floor.normal) *
                            Vector3.ProjectOnPlane(ahead, floor.normal).normalized;
                        // The prone mesh spans 2.05 by .94 m and rises .285 m from its support plane.
                        if (Physics.CheckBox(floor.point + floor.normal * .171f + direction * .04f,
                                new Vector3(.49f, .13f, 1.04f), Quaternion.LookRotation(direction, floor.normal),
                                WorldMask, QueryTriggerInteraction.Ignore)) continue;
                        if (!HasSupport(deck, floor.point + direction * .91f, up, out _) ||
                            !HasSupport(deck, floor.point - direction * .85f, up, out _)) continue;
                        support = floor;
                        forward = direction;
                        return;
                    }
                }
            }
        }

        static bool HasSupport(NavalShip deck, Vector3 point, Vector3 up, out RaycastHit floor)
        {
            return Physics.Raycast(point + up * .18f, -up, out floor, .36f, WorldMask, QueryTriggerInteraction.Ignore) &&
                floor.transform.IsChildOf(deck.transform) && Vector3.Dot(floor.normal, up) > .5f;
        }

        void FloatBody()
        {
            Vector3 position = transform.position;
            ClearHull(homeShip, ref position);
            if (sea && sea.Navy != homeShip) ClearHull(sea.Navy, ref position);
            position.y = WaterHeight(position) - FloatingBodyDraft;
            transform.position = position;
        }

        void Respawn(Vector3 localFoot)
        {
            sailor.Remove();
            if (ship != homeShip) BindShip(homeShip);
            sailor = new SailorModel(transform);
            floatingBody = false;
            life.Restore();
            pistolCooldown = 0f;
            pitch = 8f;
            StandOnDeck(localFoot, 0f);
            ResetRespawnView();
        }
    }
}
