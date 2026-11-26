using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    [DefaultExecutionOrder(120)]
    public sealed partial class CrewPlayer : CrewDamageTarget
    {
        internal enum Role { Helm, Pump, Gun, Leak }
        internal const float CannonLoadSeconds = 5f;
        internal const float HullRepairSeconds = 3f;

        private struct Post
        {
            public Role role;
            public int index;
            public Transform target;
            public string title;
        }

        public string Prompt { get; private set; } = "";
        public string StationName => occupied ? active.title : "";
        private double holdProgress;
        public float HoldProgress { get => (float)holdProgress; private set => holdProgress = value; }
        public bool IsAiming => occupied && active.role == Role.Gun;
        public bool IsAtHelm => occupied && active.role == Role.Helm;
        public bool ReadDeviceInput { get; set; } = true;
        public float GunYaw => gunYaw;
        public float GunPitch => gunPitch;
        internal override NavalShip AllegianceShip => homeShip;

        private const int WorldMask = Physics.DefaultRaycastLayers;
        internal const float BodyHeight = 1.56f, BodyRadius = .215f, SkinWidth = .018f;
        private const float InteractionRange = 1.5f;
        private readonly List<Post> stations = new List<Post>();
        private NavalShip ship, homeShip;
        private NavalWorld sea;
        private CharacterController motor;
        private SailorModel sailor;
        private Camera view;
        private Post active;
        private bool occupied, voyageEnded;
        private Vector3 stationFoot, stationFacing;
        private bool hasStance;
        private float yaw, pitch = 11f, desiredDistance = 3.45f, cameraDistance;
        private float gunYaw, gunPitch, verticalSpeed, stride, workPhase;
        private float safeFloor;
        private bool didWork;

        public void Initialize(NavalShip owner, Camera camera, NavalWorld water = null)
        {
            if (occupied) Leave();
            voyageEnded = false;
            homeShip = owner;
            AssignVoice(0);
            sea = water ? water : FindFirstObjectByType<NavalWorld>();
            view = camera;
            transform.SetParent(owner.transform, true);
            gameObject.layer = 2; // Camera and line-of-sight queries ignore the sailor.
            motor = GetComponent<CharacterController>();
            if (!motor) motor = gameObject.AddComponent<CharacterController>();
            SetSwimCapsule(false);
            motor.slopeLimit = 69f;
            motor.skinWidth = SkinWidth;
            motor.minMoveDistance = 0f;
            motor.enableOverlapRecovery = true;
            if (sailor == null) sailor = new SailorModel(transform);
            life.Restore();
            pistolCooldown = 0f;
            BindShip(owner);
            view.nearClipPlane = 0.045f;
            Recover();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            UpdateCamera(1f);
        }

        private void BindShip(NavalShip owner)
        {
            ship = owner;
            stations.Clear();
            GalleonRig rig = owner.Rig;
            Add(Role.Helm, -1, rig.Helm, "舵轮");
            AddMany(Role.Pump, rig.Pumps, "舱底泵");
            AddMany(Role.Gun, rig.Guns, "舷炮");
            AddMany(Role.Leak, rig.LeakInside, "船壳破口");
            safeFloor = owner.transform.InverseTransformPoint(rig.Spawn.position).y - 12f;
            if (rig.LeakInside != null && rig.LeakInside.Length > 0)
            {
                float lowest = float.PositiveInfinity;
                foreach (var leak in rig.LeakInside)
                    if (leak) lowest = Mathf.Min(lowest, owner.transform.InverseTransformPoint(leak.position).y);
                if (!float.IsInfinity(lowest)) safeFloor = lowest - 2.4f;
            }
        }

        private void Add(Role role, int index, Transform target, string title)
        {
            if (target) stations.Add(new Post { role = role, index = index, target = target, title = title });
        }

        private void AddMany(Role role, Transform[] targets, string title)
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
            {
                string name = title;
                if (role == Role.Gun && targets[i])
                {
                    bool starboard = Vector3.Dot(targets[i].forward, ship.transform.right) > 0f;
                    name = (starboard ? "右" : "左") + title;
                }
                Add(role, i, targets[i], name);
            }
        }

        private void Recover()
        {
            Transform spawn = ship.Rig.Spawn;
            Vector3 heading = ship.transform.InverseTransformDirection(spawn.forward);
            StandOnDeck(ship.transform.InverseTransformPoint(spawn.position) + Vector3.up * .04f,
                Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg);
        }

        private void StandOnDeck(Vector3 localFoot, float heading)
        {
            if (occupied) Leave();
            SetWeapon(CrewWeapon.None);
            motor.enabled = false;
            transform.SetParent(ship.transform, false);
            transform.SetLocalPositionAndRotation(localFoot, Quaternion.identity);
            SetSwimCapsule(false);
            motor.enabled = true;
            movement = Movement.Deck;
            ladder = null;
            releasedLadder = null;
            airDirection = Vector3.zero;
            yaw = heading;
            sailor.Face(yaw, true, 0f);
            verticalSpeed = stride = workPhase = 0f;
            cameraDistance = 0f;
            Prompt = "";
            sailor.Place(Vector3.zero, 1f);
            sailor.Pose("Idle", 0f, 1f);
            Physics.SyncTransforms();
        }

        private void Update()
        {
            if (!homeShip || homeShip.Flooding.IsSunk)
            {
                StopMedicine();
                SetRepairAudio(false);
                return;
            }
            if (Time.timeScale <= 0f) return;
            if (!ReadDeviceInput)
            {
                UpdateLife();
                return;
            }
            CrewCommand command = CrewCommand.Read();
            // The game shell owns pause/menu state. A released cursor must not steer the view.
            if (Cursor.lockState != CursorLockMode.Locked) command.Look = Vector2.zero;
            Tick(command, Time.deltaTime);
        }

        // The keyboard, replay checks and accessibility controls use this same path.
        public void Tick(CrewCommand command, float deltaTime)
        {
            if (!homeShip || homeShip.Flooding.IsSunk || Time.timeScale <= 0f || deltaTime <= 0f) return;
            if (UpdateLife()) return;
            float elapsedSeconds = deltaTime;
            deltaTime = Mathf.Min(deltaTime, 0.08f);
            Physics.SyncTransforms(); // Ship translation and listing precede all deck queries.
            didWork = false;
            if (!IsAiming)
            {
                yaw = Mathf.Repeat(yaw + command.Look.x * 0.115f, 360f);
                pitch = Mathf.Clamp(pitch - command.Look.y * 0.10f, -26f, 62f);
                desiredDistance = Mathf.Clamp(desiredDistance - command.Zoom * 0.35f, 1.35f, 5.2f);
            }

            if (!IsSwimming && ship != homeShip && ship.Flooding.IsSunk)
                EnterSea();
            if (IsSwimming)
            {
                Swim(command, deltaTime);
                return;
            }
            if (IsClimbing)
            {
                Climb(command, deltaTime);
                return;
            }

            if (occupied)
            {
                if (active.role == Role.Gun && ship.IsGunSubmerged(active.index))
                {
                    FinishWork(active.title + "已被淹没");
                }
                else if (command.UsePressed || active.role == Role.Helm && !MaySteer ||
                    !IsAvailable(active) || !InReach(active, 1.7f, out _))
                {
                    Leave();
                    Prompt = "";
                }
                else
                {
                    FacePost(deltaTime);
                    Operate(command, deltaTime, elapsedSeconds);
                }
                AnimateSailor(0f, deltaTime);
                return;
            }

            var deckLadder = LadderAtLanding();
            if (deckLadder != null && command.Move.y < -.1f && !command.JumpPressed &&
                MoveDirection(command.Move).x * Mathf.Sign(deckLadder.bottom.x) > .35f &&
                (transform.localPosition.x - deckLadder.landing.x) * Mathf.Sign(deckLadder.bottom.x) >= DeckStepReach - SkinWidth)
            {
                var path = new LadderPath(deckLadder);
                GripLadder(ship, deckLadder, path, path.AtDeckEdge, transform.localPosition.z, deltaTime);
                Climb(command, deltaTime);
                return;
            }
            float walked = Walk(command, deltaTime);
            if (IsDead) return;
            if ((IsJumping || verticalSpeed < 0f) && TryCatchLadder(MoveDirection(command.Move), deltaTime)) return;
            if (IsInOpenSea())
            {
                EnterSea();
                Swim(default, deltaTime);
                return;
            }
            if (transform.localPosition.y < safeFloor) Recover();
            if (IsJumping)
            {
                Prompt = "";
            }
            else if (Nearest(out Post post))
            {
                Prompt = ApproachPrompt(post);
                if (deckLadder != null) Prompt += "    S  下梯";
                if (command.UsePressed && CanTakePost(post) &&
                    (!ship.Company || ship.Company.TryTakePlayerPost(this, post.role, post.index)))
                {
                    SetWeapon(CrewWeapon.None);
                    active = post;
                    occupied = true;
                    workPhase = 0f;
                    gunYaw = 0f;
                    gunPitch = 4f;
                    hasStance = CrewStationFit.TryGetPose(ship, post.role, post.index, post.target, transform.position,
                        out var foot, out var forward);
                    if (hasStance)
                    {
                        stationFoot = transform.InverseTransformPoint(foot);
                        stationFacing = ship.transform.InverseTransformDirection(forward);
                    }
                    FacePost(deltaTime);
                    Operate(default, deltaTime, elapsedSeconds);
                }
            }
            else Prompt = deckLadder != null ? "S  下梯" : "";
            bool firePistol = !occupied && TickCombat(command, elapsedSeconds, deltaTime);
            AnimateSailor(walked, deltaTime);
            if (firePistol)
            {
                Physics.SyncTransforms(); // Use the raised muzzle at this frame's actual deck position.
                FirePistol();
            }
        }

        private void AnimateSailor(float walked, float dt)
        {
            sailor.Place(occupied && hasStance ? stationFoot : Vector3.zero, dt);
            if (!occupied)
            {
                stride = Mathf.Repeat(stride + walked / 1.6f, 1f);
                bool moving = IsJumping || walked > .0005f;
                if (Weapon == CrewWeapon.Pistol) PosePistol(moving, dt);
                else if (attackClip != null) sailor.Pose(attackClip, attackTime / attackLength, dt);
                else if (Weapon == CrewWeapon.Cutlass && !moving) sailor.Pose("CutlassSlash", 0f, dt);
                else sailor.Pose(moving ? "Run" : "Idle", stride, dt);
                return;
            }

            bool small = ship.Definition.Type == ShipType.Sloop;
            string clip = active.role switch
            {
                Role.Helm => small ? "HelmSmall" : "HelmMedium",
                Role.Gun when small => "GunSmall",
                Role.Gun => ship.Rig.transform.InverseTransformPoint(active.target.position).y < ship.Definition.Data.mainDeck
                    ? "GunMediumLower" : "GunMediumUpper",
                Role.Pump => small ? "PumpSmall" : "PumpMedium",
                Role.Leak => "Repair",
                _ => "Idle"
            };
            sailor.Pose(clip, workPhase, dt);
        }

        private bool NeedsPumping => ship.Flooding.WaterLevel01 > 0f || ship.Flooding.ActiveLeaks > 0;
        private bool MaySteer => ship == homeShip && (!ship.Company || ship.Company.CanSteer(this));

        private bool CanTakePost(Post post)
        {
            if (post.role == Role.Helm) return MaySteer;
            if (post.role == Role.Gun) return !ship.IsGunSubmerged(post.index);
            return post.role != Role.Pump || NeedsPumping;
        }

        private string ApproachPrompt(Post post)
        {
            if (post.role == Role.Helm) return MaySteer ? "E  掌舵" : "舵轮仅限本船船长";
            if (post.role == Role.Pump) return NeedsPumping ? "E  使用舱底泵排水" : "舱底泵  舱水已排干";
            if (post.role == Role.Leak) return "E  修补船壳破口";
            if (post.role == Role.Gun)
            {
                if (ship.IsGunSubmerged(post.index)) return post.title + "已被淹没";
                if (!ship.IsLoaded(post.index)) return "E  装填" + post.title;
            }
            return "E  " + post.title;
        }

        private bool IsAvailable(Post post)
        {
            return post.target && (post.role != Role.Leak || ship.Flooding.IsLeaking(post.index));
        }

        private bool InReach(Post post, float range, out float distance)
        {
            return CanReach(transform.position, ship.transform.up, post.target, post.role == Role.Leak, range, out distance);
        }

        internal static bool CanStandAndRepair(Transform hull, Vector3 localStand, Transform patch)
        {
            Vector3 up = hull.up;
            Vector3 stand = hull.TransformPoint(localStand);
            if (!Physics.Raycast(stand + up * .18f, -up, out var deck, .36f, WorldMask, QueryTriggerInteraction.Ignore) ||
                !deck.transform.IsChildOf(hull) || Vector3.Dot(deck.normal, up) <= .36f)
                return false;

            Vector3 foot = deck.point + up * (SkinWidth * 2f);
            if (Physics.CheckCapsule(foot + up * BodyRadius, foot + up * (BodyHeight - BodyRadius), BodyRadius,
                    WorldMask, QueryTriggerInteraction.Ignore))
                return false;
            return CanReach(foot, up, patch, true, InteractionRange, out _);
        }

        private static bool CanReach(Vector3 foot, Vector3 up, Transform target, bool repair, float range, out float distance)
        {
            Vector3 start = foot + up * 1.02f;
            Vector3 end = target.position;
            Vector3 delta = end - start;
            distance = delta.magnitude;
            if (distance > range || Mathf.Abs(Vector3.Dot(delta, up)) > 1.05f) return false;
            if (repair && Vector3.Dot(start - end, target.forward) < 0.03f) return false;
            return distance < 0.14f || !Physics.Raycast(start, delta / distance, distance - 0.12f,
                WorldMask, QueryTriggerInteraction.Ignore);
        }

        private bool Nearest(out Post result)
        {
            result = default;
            float best = float.PositiveInfinity;
            foreach (Post p in stations)
            {
                if (!IsAvailable(p) || !InReach(p, InteractionRange, out float distance)) continue;
                if (p.role == Role.Leak && !CanSeePatch(p.target.position)) continue;
                if (distance >= best) continue;
                best = distance;
                result = p;
            }
            return !float.IsInfinity(best);
        }

        private bool CanSeePatch(Vector3 surface)
        {
            Vector3 screen = view.WorldToViewportPoint(surface);
            if (screen.z <= view.nearClipPlane || screen.x < .03f || screen.x > .97f || screen.y < .03f || screen.y > .97f)
                return false;
            Vector3 start = view.transform.position;
            Vector3 delta = surface - start;
            return delta.magnitude < .12f || !Physics.Raycast(start, delta.normalized, delta.magnitude - .12f,
                WorldMask, QueryTriggerInteraction.Ignore);
        }

        private void FacePost(float dt)
        {
            if (hasStance)
            {
                sailor.Face(Mathf.Atan2(stationFacing.x, stationFacing.z) * Mathf.Rad2Deg, false, dt);
                return;
            }
            Vector3 direction = active.role == Role.Gun || active.role == Role.Helm
                ? active.target.forward : active.target.position - transform.position;
            Vector3 local = ship.transform.InverseTransformDirection(direction);
            if (local.x * local.x + local.z * local.z > .001f)
                sailor.Face(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, false, dt);
        }

        private float Walk(CrewCommand command, float dt)
        {
            Vector3 up = ship.transform.up;
            Vector3 before = transform.position;
            Vector3 local = MoveDirection(command.Move);
            Vector3 desired = ship.transform.TransformDirection(local);
            bool ground = Physics.SphereCast(before + up * 0.38f, 0.145f, -up,
                out RaycastHit contact, 0.49f, WorldMask, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(contact.normal, up) > 0.34f && (motor.isGrounded || contact.distance < .34f);
            bool walking = local.sqrMagnitude > 0.001f;
            if (command.JumpPressed && ground && !IsJumping)
            {
                movement = Movement.Jump;
                verticalSpeed = Mathf.Sqrt(2f * Gravity * JumpHeight);
                airDirection = local;
            }
            // Keep an idle sailor planted on steep stair ramps and on a turning or rolling deck.
            float rise;
            if (ground && !IsJumping && verticalSpeed >= -SafeLandingSpeed)
            {
                verticalSpeed = 0f;
                if (!walking) return 0f;
                desired = Vector3.ProjectOnPlane(desired, contact.normal).normalized * local.magnitude;
                rise = -.45f * dt;
            }
            else
            {
                float oldSpeed = verticalSpeed;
                verticalSpeed = Mathf.Max(-14f, oldSpeed - Gravity * dt);
                rise = (oldSpeed + verticalSpeed) * .5f * dt;
                if (IsJumping)
                {
                    if (walking) airDirection = local;
                    desired = ship.transform.TransformDirection(airDirection);
                }
            }
            float speed = command.Sprint || IsJumping ? RunSpeed : 2.24f;
            CollisionFlags flags = motor.Move(desired * (speed * dt) + up * rise);
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((flags & CollisionFlags.Below) != 0 && rise <= 0f)
            {
                float impactSpeed = -verticalSpeed;
                verticalSpeed = 0f;
                movement = Movement.Deck;
                if (impactSpeed > SafeLandingSpeed)
                    TakeDamage(Mathf.CeilToInt((impactSpeed - SafeLandingSpeed) * 25f));
            }
            if (walking && !IsDead && Weapon != CrewWeapon.Cutlass && !IsPistolAiming)
                sailor.Face(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, false, dt);
            return Vector3.ProjectOnPlane(transform.position - before, up).magnitude;
        }

        private void Operate(CrewCommand command, float dt, float elapsedSeconds)
        {
            string action;
            switch (active.role)
            {
                case Role.Helm:
                    ship.SetHelm(command.Move.x);
                    if (command.SailChange != 0) ship.ChangeSail(command.SailChange);
                    didWork = Mathf.Abs(command.Move.x) > .05f || command.SailChange != 0;
                    action = ship.SailStep switch
                    {
                        0 => "A/D 转舵   W 起锚放半帆",
                        1 => "A/D 转舵   W 放满帆   S 收帆下锚",
                        _ => "A/D 转舵   S 收至半帆"
                    };
                    break;
                case Role.Gun:
                    gunYaw = Mathf.Clamp(gunYaw + command.Look.x * .10f + command.Move.x * dt * 24f, -24f, 24f);
                    gunPitch = Mathf.Clamp(gunPitch + command.Look.y * .085f + command.Move.y * dt * 18f, -5f, 26f);
                    if (!ship.IsLoaded(active.index))
                    {
                        if (AdvanceWork(CannonLoadSeconds, elapsedSeconds))
                        {
                            ship.Load(active.index);
                            HoldProgress = 0f;
                        }
                    }
                    else if (command.FirePressed)
                    {
                        ship.Fire(active.index, gunYaw, gunPitch, this);
                        HoldProgress = 0f;
                    }
                    action = ship.IsLoaded(active.index) ? "鼠标  瞄准   左键 / 空格  开炮" : "正在装填";
                    break;
                case Role.Pump:
                    // Drain and ingress use the same elapsed time, even on a long movement frame.
                    ship.Flooding.Pump(elapsedSeconds);
                    if (!NeedsPumping)
                    {
                        FinishWork("舱底泵  舱水已排干");
                        return;
                    }
                    didWork = true;
                    HoldProgress = ship.Flooding.WaterLevel01;
                    int percent = Mathf.CeilToInt(HoldProgress * 100f);
                    action = percent == 0 ? "排水中   船壳仍在漏水" : $"排水中   舱水 {percent}%";
                    break;
                default:
                    SetRepairAudio(true);
                    if (AdvanceWork(HullRepairSeconds, elapsedSeconds) && ship.Flooding.Repair(active.index))
                    {
                        FinishWork("已封堵破口");
                        return;
                    }
                    action = "正在修补破口";
                    break;
            }
            if (didWork) workPhase = Mathf.Repeat(workPhase + dt * 1.35f, 1f);
            if (active.role == Role.Pump) ship.Rig.SetPumpStroke(active.index, workPhase);
            Prompt = action + "     E  离开";
        }

        private bool AdvanceWork(float duration, float dt)
        {
            didWork = true;
            holdProgress = System.Math.Min(1d, holdProgress + (double)dt / duration);
            return holdProgress >= 1d;
        }

        private void FinishWork(string message)
        {
            Leave();
            Prompt = message;
        }

        internal void EndVoyage(NavalShip sunkShip)
        {
            voyageEnded = true;
            if (occupied) Leave();
            SetWeapon(CrewWeapon.None);
            if (!IsDead && !IsSwimming) sailor.Pose("Idle", 0f, 1f);
            sailor.SetVisible(true);
            Prompt = "";
            // Frame the flooded weather deck even when the sailor was below it or on the raised stern.
            float length = sunkShip.Definition.Length;
            float deckHeight = sunkShip.Flooding.SinkingHeight + .6f;
            Vector3 eye = sunkShip.transform.TransformPoint(.54f * length, deckHeight + .42f * length, .13f * length);
            Vector3 deck = sunkShip.transform.TransformPoint(0f, deckHeight, sunkShip.Definition.PivotZ);
            view.fieldOfView = 52f;
            view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(deck - eye, sunkShip.transform.up));
        }

        private void Leave()
        {
            SetRepairAudio(false);
            if (ship.Company) ship.Company.ReleasePlayerPost(this);
            if (active.role == Role.Helm) ship.SetHelm(0f);
            if (active.role == Role.Pump) ship.Rig.SetPumpStroke(active.index, 0f);
            if (IsAiming) cameraDistance = 0f;
            occupied = false;
            hasStance = false;
            didWork = false;
            HoldProgress = 0f;
        }

        private void LateUpdate()
        {
            sailor?.ApplyPistolAim();
            if (homeShip && view && !voyageEnded && !homeShip.Flooding.IsSunk) UpdateCamera(Time.unscaledDeltaTime);
        }

        static readonly float[] CabinViewOffsets = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };
        Vector3 CabinCameraArm => new(.08f, 0f, -Mathf.Min(desiredDistance, 2.35f));

        internal void ResetRespawnView()
        {
            Vector3 foot = transform.localPosition;
            bool sloop = ship.Definition.Type == ShipType.Sloop;
            // The sloop's stair rises forward. From its forward bay the sailor
            // first walks aft down either side to reach the bottom of the flight.
            Vector3 landing = sloop
                ? new Vector3(foot.x < 0f ? -.90f : .90f, foot.y, 7.18f)
                : new Vector3(0f, foot.y, 12.65f);
            Vector3 heading = landing - foot;
            yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
            pitch = sloop ? 3f : 8f;
            if (sloop) yaw = CabinArrivalYaw(yaw);
            sailor.Face(yaw, true, 0f);
            cameraDistance = 0f;
            UpdateCamera(1f);
        }

        float CabinArrivalYaw(float preferred)
        {
            Vector3 pivot = sailor.Foot + transform.up * 1.10f;
            float bestYaw = preferred, bestClearance = 0f;
            foreach (float angle in CabinViewOffsets)
            {
                float candidate = preferred + angle;
                Quaternion rotation = transform.rotation * Quaternion.Euler(pitch, candidate, 0f);
                float distance = ClearCameraArm(pivot, rotation * CabinCameraArm, out _);
                if (distance >= 1.05f) return candidate;
                if (distance <= bestClearance) continue;
                bestClearance = distance;
                bestYaw = candidate;
            }
            return bestYaw;
        }

        private bool InSloopCabin()
        {
            if (IsDead || IsSwimming || IsClimbing || ship.Definition.Type != ShipType.Sloop) return false;
            Vector3 foot = transform.localPosition;
            return foot.y < ship.Definition.Data.mainDeck - .20f && Mathf.Abs(foot.x) < 1.44f
                && foot.z > 6.32f && foot.z < 10.78f;
        }

        private void UpdateCamera(float dt)
        {
            Vector3 up = transform.up;
            if (IsAiming)
            {
                Vector3 barrel = NavalShip.ShotDirection(active.target, gunYaw, gunPitch);
                Vector3 eye = ship.GunSightPosition(active.target, barrel);
                view.fieldOfView = 52f;
                view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(barrel, up));
                sailor.SetVisible(false);
                return;
            }

            bool cabin = InSloopCabin();
            float orbitPitch = IsDead ? Mathf.Clamp(pitch, 13f, 38f) : IsSwimming ? Mathf.Max(3f, pitch) : pitch;
            Quaternion rotation = transform.rotation * Quaternion.Euler(orbitPitch, yaw, 0f);
            Vector3 pivot = sailor.Foot + up * (IsDead ? .32f : IsSwimming ? .36f : cabin ? 1.10f : 1.34f);
            Vector3 arm = cabin ? CabinCameraArm : new Vector3(.16f, .06f, -desiredDistance);
            if (IsPistolAiming)
                arm = new Vector3(cabin ? .28f : .57f, .06f, -Mathf.Min(desiredDistance, cabin ? 1.8f : 2.7f));
            float length = ClearCameraArm(pivot, rotation * arm, out var direction);
            // This cabin has solid downward-facing ceiling geometry. The sphere cast
            // sees it directly; the allowance for one-sided decks would clip the view early.
            if (!cabin) length = ClipAgainstDeckUndersides(pivot, direction, length, up);
            if (cameraDistance <= 0f || length < cameraDistance) cameraDistance = length;
            else cameraDistance = Mathf.Lerp(cameraDistance, length, 1f - Mathf.Exp(-7f * dt));
            float field = cabin ? Mathf.Lerp(84f, 72f, Mathf.InverseLerp(.65f, 1.75f, cameraDistance)) : 63f;
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, field, 1f - Mathf.Exp(-9f * dt));
            view.transform.SetPositionAndRotation(pivot + direction * cameraDistance, rotation);
            sailor.SetVisible(IsDead || cameraDistance > .48f);
        }

        static float ClearCameraArm(Vector3 pivot, Vector3 offset, out Vector3 direction)
        {
            float distance = offset.magnitude;
            direction = offset / distance;
            return Physics.SphereCast(pivot, .13f, direction, out var obstacle, distance,
                WorldMask, QueryTriggerInteraction.Ignore) ? Mathf.Max(.045f, obstacle.distance - .035f) : distance;
        }

        private static float ClipAgainstDeckUndersides(Vector3 pivot, Vector3 direction, float length, Vector3 up)
        {
            // Deck colliders face up. Cast back towards the camera pivot to find a ceiling
            // from its solid side, stopping at the nearest deck in a stacked hold.
            if (Vector3.Dot(direction, up) <= .001f) return length;
            // Reserve clearance for exposed beams and the near plane below the deck.
            Vector3 top = pivot + up * .39f;
            const float segment = .75f;
            for (float start = 0f; start < length; start += segment)
            {
                float end = Mathf.Min(start + segment, length);
                if (Physics.Raycast(top + direction * end, -direction, out RaycastHit roof,
                        end - start + .004f, WorldMask, QueryTriggerInteraction.Ignore))
                    return Mathf.Max(.045f, end - roof.distance - .035f);
            }
            return length;
        }

        protected override void OnDisable()
        {
            StopMedicine();
            base.OnDisable();
        }

        private void OnDestroy()
        {
            if (ship && occupied) Leave();
        }
    }
}
