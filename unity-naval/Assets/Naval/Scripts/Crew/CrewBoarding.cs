using UnityEngine;

namespace Galleon
{
    public sealed partial class CrewPlayer
    {
        enum Movement { Deck, Jump, Swim, Climb, Dead }

        internal const float RunSpeed = 3.65f;
        const float Gravity = 18f, JumpHeight = 1.20f;
        const float SwimSpeed = 2.15f, SwimRadius = .28f;
        const float ClimbSpeed = 1.65f, LadderSideSpeed = .90f, LadderTouchDepth = SwimRadius + .07f;
        // The exported landing is 0.40 m inboard of the last step. The capsule meets the step before its centre does.
        const float LandingClearance = .40f, DeckStepReach = LandingClearance - BodyRadius - SkinWidth;
        Movement movement;
        Vector3 airDirection;
        BoardingLadder ladder, releasedLadder;
        float climbDistance, climbLane;

        // Distance increases from the lowest outside rung, over the solid cap, down to the deck.
        // A lane of -1 or +1 follows a physical tread edge, including where the inner steps narrow.
        readonly struct LadderPath
        {
            readonly BoardingLadder data;
            readonly Vector3 outerTop, innerTop, innerFoot, landing;
            readonly float rimStart, rimEnd, deckStep;
            public float Length { get; }

            public LadderPath(BoardingLadder data)
            {
                this.data = data;
                outerTop = new Vector3(data.top.x, data.top.y, data.bottom.z);
                landing = data.landing + Vector3.up * (2f * SkinWidth);
                innerFoot = landing + Vector3.right * (Mathf.Sign(data.bottom.x) * DeckStepReach);
                innerTop = new Vector3(innerFoot.x, data.top.y, data.landing.z);
                rimStart = Vector3.Distance(data.bottom, outerTop);
                rimEnd = rimStart + Vector3.Distance(outerTop, innerTop);
                deckStep = rimEnd + Vector3.Distance(innerTop, innerFoot);
                Length = deckStep + DeckStepReach;
            }

            public float AtHeight(float y) => Mathf.InverseLerp(data.bottom.y, outerTop.y, y) * rimStart;
            public float AtDeckEdge => deckStep;
            Vector3 Center(float distance)
            {
                if (distance <= rimStart) return Vector3.Lerp(data.bottom, outerTop, distance / rimStart);
                if (distance <= rimEnd) return Vector3.Lerp(outerTop, innerTop, (distance - rimStart) / (rimEnd - rimStart));
                if (distance <= deckStep) return Vector3.Lerp(innerTop, innerFoot, (distance - rimEnd) / (deckStep - rimEnd));
                return Vector3.Lerp(innerFoot, landing, (distance - deckStep) / DeckStepReach);
            }

            float SideSpan(float distance, float centerZ, float lateral)
            {
                // The brigantine's aft crossing is off-centre. Both tread edges still use the measured station Z.
                float halfWidth = Mathf.Lerp(data.halfWidth, data.innerHalfWidth, Mathf.InverseLerp(rimStart, rimEnd, distance));
                return halfWidth - Mathf.Sign(lateral) * (centerZ - data.bottom.z);
            }

            public float LaneAt(float distance, float z)
            {
                float center = Center(distance).z;
                float lateral = z - center;
                return lateral / SideSpan(distance, center, lateral);
            }

            public Vector3 Point(float distance, float lane)
            {
                Vector3 center = Center(distance);
                center.z += lane * SideSpan(distance, center.z, lane);
                return center;
            }
        }
        LadderPath climbPath;

        public bool IsJumping => movement == Movement.Jump;
        public bool IsSwimming => movement == Movement.Swim;
        public bool IsClimbing => movement == Movement.Climb;

        Vector3 MoveDirection(Vector2 input) => Quaternion.Euler(0f, yaw, 0f)
            * Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        float WaterHeight(Vector3 position) => sea ? sea.SurfaceHeight(position) : 0f;

        bool IsInOpenSea()
        {
            Vector3 position = transform.position;
            if (verticalSpeed > 0f || position.y > WaterHeight(position)) return false;
            Vector3 local = ship.transform.InverseTransformPoint(position);
            // A flooded gun deck remains walkable right up to the planking.
            bool inside = local.z >= 0f && local.z <= ship.Definition.Length &&
                Mathf.Abs(local.x) <= ship.Definition.HalfBreadth(local.z, local.y) + SkinWidth;
            if (inside) return false;
            return !Physics.Raycast(position + transform.up * .12f, -transform.up, out var hit, .21f,
                WorldMask, QueryTriggerInteraction.Ignore) || !hit.transform.IsChildOf(ship.transform);
        }

        void SetSwimCapsule(bool swimming)
        {
            motor.radius = swimming ? SwimRadius : BodyRadius;
            motor.height = swimming ? .60f : BodyHeight;
            motor.center = swimming ? Vector3.zero : Vector3.up * (BodyHeight * .5f);
            motor.stepOffset = swimming ? 0f : .23f;
        }

        void ChangeFrame(NavalShip deck)
        {
            Vector3 look = transform.TransformDirection(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            transform.SetParent(deck ? deck.transform : homeShip.transform.parent, true);
            transform.rotation = deck ? deck.transform.rotation : Quaternion.identity;
            look = transform.InverseTransformDirection(look);
            yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
        }

        void EnterSea()
        {
            if (occupied) Leave();
            SetWeapon(CrewWeapon.None);
            motor.enabled = false;
            ChangeFrame(null);
            Vector3 position = transform.position;
            position.y = WaterHeight(position);
            transform.position = position;
            SetSwimCapsule(true);
            motor.enabled = true;
            verticalSpeed = 0f;
            movement = Movement.Swim;
            swimStartedAt = Time.timeAsDouble;
            ladder = null;
            sailor.Place(Vector3.zero, 1f);
            sailor.Face(yaw, true, 0f);
            Physics.SyncTransforms();
        }

        void Swim(CrewCommand command, float dt)
        {
            Vector3 direction = MoveDirection(command.Move);
            Vector3 position = transform.position;
            Vector3 next = position + direction * (SwimSpeed * dt);
            next.y = WaterHeight(next);
            ClearHull(homeShip, ref next);
            NavalShip other = sea ? sea.Navy : null;
            if (other != homeShip) ClearHull(other, ref next);
            motor.Move(next - position);
            if (direction.sqrMagnitude > .001f)
                sailor.Face(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, false, dt);
            sailor.Swim((float)(Time.timeAsDouble - swimStartedAt), dt);

            Prompt = "WASD  游泳";
            TryCatchLadder(direction, dt);
        }

        // A moving hull can sweep into a stationary swimmer between controller moves.
        static void ClearHull(NavalShip vessel, ref Vector3 position)
        {
            if (!vessel) return;
            Vector3 local = vessel.transform.InverseTransformPoint(position);
            if (local.z < 0f || local.z > vessel.Definition.Length) return;
            float width = vessel.Definition.HalfBreadth(local.z, local.y);
            float overlap = width + SwimRadius - Mathf.Abs(local.x);
            if (width <= 0f || overlap <= 0f) return;
            float toBow = vessel.Definition.Length - local.z;
            if (local.z < overlap && local.z < toBow) local.z = -SwimRadius;
            else if (toBow < overlap) local.z = vessel.Definition.Length + SwimRadius;
            else local.x = Mathf.Sign(local.x) * (width + SwimRadius);
            Vector3 outside = vessel.transform.TransformPoint(local);
            position.x = outside.x;
            position.z = outside.z;
        }

        bool TryCatchLadder(Vector3 direction, float dt)
        {
            NavalShip owner = null;
            BoardingLadder nearest = null;
            float best = float.PositiveInfinity;
            var worldDirection = transform.TransformDirection(direction);
            FindLadder(homeShip, worldDirection, ref best, ref owner, ref nearest);
            if (sea && sea.Navy != homeShip) FindLadder(sea.Navy, worldDirection, ref best, ref owner, ref nearest);
            if (nearest == null) return false;
            Vector3 foot = owner.transform.InverseTransformPoint(transform.position);
            var path = new LadderPath(nearest);
            GripLadder(owner, nearest, path, path.AtHeight(foot.y), foot.z, dt);
            return true;
        }

        void FindLadder(NavalShip vessel, Vector3 worldDirection, ref float best, ref NavalShip owner,
            ref BoardingLadder nearest)
        {
            if (!vessel || vessel.Flooding.IsSunk) return;
            Vector3 local = vessel.transform.InverseTransformPoint(transform.position);
            Vector3 motion = vessel.transform.InverseTransformDirection(worldDirection);
            foreach (var candidate in vessel.Definition.Data.boardingLadders)
            {
                float fraction = Mathf.InverseLerp(candidate.bottom.y, candidate.top.y, local.y);
                float depth = Mathf.Abs(local.x - Mathf.Lerp(candidate.bottom.x, candidate.top.x, fraction));
                float offset = local.z - candidate.bottom.z;
                float outward = Mathf.Sign(candidate.bottom.x) * motion.x;
                if (candidate == releasedLadder && vessel == ship)
                {
                    // Surfacing after the last rung can put the swimmer back against the same ladder.
                    // A deliberate return or swimming clear of it makes it available again.
                    if (depth > LadderTouchDepth + .10f || Mathf.Abs(offset) > candidate.halfWidth + .10f)
                        releasedLadder = null;
                    else if (outward >= -.15f && offset * motion.z >= -.015f) continue;
                }
                if (depth > LadderTouchDepth || Mathf.Abs(offset) > candidate.halfWidth ||
                    local.y < candidate.bottom.y - SkinWidth || local.y > candidate.top.y + SkinWidth ||
                    (!IsSwimming && outward > .15f)) continue;
                float distance = depth * depth + offset * offset;
                if (distance >= best) continue;
                best = distance;
                owner = vessel;
                nearest = candidate;
            }
        }

        BoardingLadder LadderAtLanding()
        {
            if (IsJumping) return null;
            Vector3 foot = transform.localPosition;
            foreach (var candidate in ship.Definition.Data.boardingLadders)
            {
                Vector3 delta = foot - candidate.landing;
                float outboard = delta.x * Mathf.Sign(candidate.bottom.x);
                if (Mathf.Abs(foot.z - candidate.bottom.z) <= candidate.innerHalfWidth &&
                    outboard >= -BodyRadius && outboard <= LandingClearance + SkinWidth && Mathf.Abs(delta.y) <= .20f) return candidate;
            }
            return null;
        }

        void GripLadder(NavalShip owner, BoardingLadder candidate, LadderPath path, float distance, float z, float dt)
        {
            SetWeapon(CrewWeapon.None);
            motor.enabled = false;
            ChangeFrame(owner);
            if (ship != owner) BindShip(owner);
            SetSwimCapsule(false);
            movement = Movement.Climb;
            ladder = candidate;
            releasedLadder = null;
            climbPath = path;
            climbDistance = distance;
            climbLane = path.LaneAt(distance, z);
            verticalSpeed = 0f;
            transform.localPosition = path.Point(distance, climbLane);
            yaw = ladder.top.x > 0f ? -90f : 90f;
            sailor.Place(Vector3.zero, 1f);
            sailor.Face(yaw, true, 0f);
            sailor.Pose("Run", stride, dt);
            Prompt = "W / S  上下     A / D  左右";
        }

        void Climb(CrewCommand command, float dt)
        {
            Vector2 input = Vector2.ClampMagnitude(command.Move, 1f);
            float side = Mathf.Sign(ladder.bottom.x);
            float along = climbDistance + input.y * ClimbSpeed * dt;
            climbDistance = Mathf.Clamp(along, 0f, climbPath.Length);
            Vector3 before = transform.localPosition;
            Vector3 foot = climbPath.Point(climbDistance, climbLane);
            if (input.x != 0f)
            {
                foot.z += side * input.x * LadderSideSpeed * dt;
                climbLane = climbPath.LaneAt(climbDistance, foot.z);
            }
            transform.localPosition = foot;
            stride = Mathf.Repeat(stride + Vector3.Distance(before, transform.localPosition) / 1.25f, 1f);
            sailor.Pose("Run", stride, dt);

            bool offLadder = Mathf.Abs(climbLane) > 1f || (along <= 0f && input.y < 0f);
            if (!offLadder && !(along >= climbPath.Length && input.y > 0f)) return;
            releasedLadder = offLadder ? ladder : null;
            ladder = null;
            movement = offLadder ? Movement.Jump : Movement.Deck;
            airDirection = Vector3.forward * (side * input.x * LadderSideSpeed / RunSpeed);
            verticalSpeed = 0f;
            motor.enabled = true;
            Physics.SyncTransforms();
            Prompt = "";
            if (offLadder && IsInOpenSea()) EnterSea();
        }
    }
}
