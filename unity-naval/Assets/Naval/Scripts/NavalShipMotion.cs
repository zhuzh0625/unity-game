using UnityEngine;

namespace Galleon
{
    public sealed partial class NavalShip
    {
        // Bound the relative travel of the bow/stern as well as the centres during a contact step.
        const float MaxContactTravel = .12f;
        const float ContactReleaseClearance = .15f;
        HullFootprint footprint;
        NavalShip contactPartner;
        bool collisionContact;
        int motionFrame = -1;
        float driveSpeed;

        internal void SetContactPartner(NavalShip other)
        {
            contactPartner = other;
            other.contactPartner = this;
        }

        internal void PlaceAtSea(Vector3 centreAtSea, float course, float? seaTime = null)
        {
            ClearCollisionContact(contactPartner);
            centre = new Vector2(centreAtSea.x, centreAtSea.z);
            Course = Mathf.Repeat(course, 360f);
            Speed = driveSpeed = Rudder = requestedHelm = 0f;
            ApplyPose(seaTime ?? Time.time);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!Rig || Flooding.IsSunk || dt <= 0f || motionFrame == Time.frameCount) return;
            motionFrame = Time.frameCount;
            AdvanceControls(dt);
            NavalShip other = contactPartner;
            if (!other || !other.gameObject.activeInHierarchy || other.Flooding.IsSunk)
            {
                ClearCollisionContact(other);
                AdvancePosition(dt);
                FinishMotion(dt);
                return;
            }

            // Whichever ship updates first integrates both before the crew, camera or guns see either pose.
            bool sailing = other.isActiveAndEnabled;
            Vector2 otherCentre = other.centre;
            float otherCourse = other.Course;
            if (sailing)
            {
                other.motionFrame = Time.frameCount;
                other.AdvanceControls(dt);
            }
            else
            {
                Vector3 pivot = other.transform.TransformPoint(0f, 0f, other.Definition.PivotZ);
                otherCentre = new Vector2(pivot.x, pivot.z);
                Vector3 bow = other.transform.forward;
                otherCourse = Mathf.Atan2(bow.x, bow.z) * Mathf.Rad2Deg;
            }

            float travel = (SweepSpeed + (sailing ? other.SweepSpeed : 0f)) * dt;
            float reach = footprint.Radius + other.footprint.Radius + travel;
            bool near = (centre - otherCentre).sqrMagnitude < reach * reach;
            int steps = near ? Mathf.Max(1, Mathf.CeilToInt(travel / MaxContactTravel)) : 1;
            float step = dt / steps;
            float share = near ? ContactShare(other, sailing) : 0f;
            if (near) SeparateHulls(other, ref otherCentre, otherCourse, share);
            if (sailing) other.centre = otherCentre;
            Vector2 start = centre, otherStart = otherCentre;
            bool touched = false;
            for (int i = 0; i < steps; i++)
            {
                AdvancePosition(step);
                if (sailing)
                {
                    other.AdvancePosition(step);
                    otherCentre = other.centre;
                    otherCourse = other.Course;
                }
                if (near && SeparateHulls(other, ref otherCentre, otherCourse, share))
                {
                    touched = true;
                    if (sailing) other.centre = otherCentre;
                }
            }

            // Penetration correction itself mints a zero-overlap frame. Wait for a real gap
            // before rearming the onset, including when both ships stop against each other.
            if (collisionContact && !footprint.Overlap(centre, Course, other.footprint, otherCentre,
                    otherCourse, out _, ContactReleaseClearance))
                ClearCollisionContact(other);

            // The HUD reports actual motion. Keeping the helm under sail lets the captain steer off a hull.
            if (touched)
            {
                Speed = (centre - start).magnitude / dt;
                if (sailing) other.Speed = (otherCentre - otherStart).magnitude / dt;
            }
            FinishMotion(dt);
            if (sailing) other.FinishMotion(dt);
        }

        float TurnRate => Rudder * Mathf.Min(driveSpeed * 3.6f, 12f);
        float SweepSpeed => driveSpeed + Mathf.Abs(TurnRate) * Mathf.Deg2Rad * footprint.Radius;

        void AdvanceControls(float dt)
        {
            Rudder = Mathf.MoveTowards(Rudder, requestedHelm, (Mathf.Abs(requestedHelm) > .01f ? 1.3f : .48f) * dt);
            float wind = Mathf.Lerp(.48f, 1f, .5f + .5f * Vector2.Dot(Heading(), new Vector2(.60f, .80f)));
            float target = SailStep * 2.9f * wind;
            driveSpeed = Mathf.MoveTowards(driveSpeed, target, dt * (Anchored ? 2f : .68f));
            Speed = driveSpeed;
        }

        Vector2 Heading()
        {
            float angle = Course * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        void AdvancePosition(float dt)
        {
            Course = Mathf.Repeat(Course + TurnRate * dt, 360f);
            centre += Heading() * (driveSpeed * dt);
        }

        float ContactShare(NavalShip other, bool otherMoves)
        {
            if (!otherMoves) return 1f;
            if (Anchored != other.Anchored) return Anchored ? 0f : 1f;
            if (Anchored && (driveSpeed == 0f) != (other.driveSpeed == 0f))
                return driveSpeed == 0f ? 0f : 1f;
            // In open water the smaller hull yields more. Displaced volume is proportional to L × B × draft.
            float volume = Definition.Length * Definition.Beam * Definition.DryDraft;
            float otherVolume = other.Definition.Length * other.Definition.Beam * other.Definition.DryDraft;
            return otherVolume / (volume + otherVolume);
        }

        bool SeparateHulls(NavalShip other, ref Vector2 otherCentre, float otherCourse, float share)
        {
            if (!footprint.Overlap(centre, Course, other.footprint, otherCentre, otherCourse, out Vector2 shift))
                return false;
            // Only cancel penetration along the contact normal, retaining travel along the planking.
            centre += shift * share;
            otherCentre -= shift * (1f - share);
            if (!collisionContact)
            {
                collisionContact = other.collisionContact = true;
                Vector2 near = footprint.ClosestBoundaryPoint(centre, Course, otherCentre);
                Vector2 far = other.footprint.ClosestBoundaryPoint(otherCentre, otherCourse, near);
                Vector2 contact = (footprint.ClosestBoundaryPoint(centre, Course, far) + far) * .5f;
                NavalAudio.Effect(NavalSound.Collision, new Vector3(contact.x, 0f, contact.y), this, other);
            }
            return true;
        }

        void ClearCollisionContact(NavalShip other)
        {
            collisionContact = false;
            if (other) other.collisionContact = false;
        }
    }
}
