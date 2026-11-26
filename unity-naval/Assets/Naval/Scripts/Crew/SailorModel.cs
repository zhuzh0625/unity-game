using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galleon
{
    internal sealed class SailorModel
    {
        const string ModelName = "Deckhand_R06";

        readonly Transform stance;
        readonly CapsuleCollider hurtbox;
        readonly Animation animation;
        readonly float swimCycleSeconds;
        readonly Renderer[] renderers;
        readonly Renderer hammer, pistol, cutlass;
        readonly GameObject medicineBottle;
        readonly Transform muzzle, spine, rightShoulder;
        readonly AnimationState pistolStride, medicineHold;
        readonly Quaternion standingSpine;
        AnimationState current, previous;
        Quaternion sampledSpine, sampledShoulder;
        float aimPitch, strideWeight;
        float facing, blend;
        bool posingPistol, medicineHeld, visible = true;

        internal Vector3 Foot => stance.position;
        internal Vector3 Forward => stance.forward;
        internal Vector3 PistolMuzzle => muzzle.position;
        internal float PistolClipSeconds { get; }

        internal SailorModel(Transform parent)
        {
            const string path = "Crew/" + ModelName;
            var prefab = Resources.Load<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Missing deckhand model: " + path);
            stance = new GameObject("水手站位").transform;
            stance.SetParent(parent, false);
            stance.gameObject.layer = parent.gameObject.layer;
            hurtbox = stance.gameObject.AddComponent<CapsuleCollider>();
            hurtbox.isTrigger = true;
            hurtbox.radius = .25f;
            hurtbox.height = 1.80f;
            var model = UnityEngine.Object.Instantiate(prefab, stance, false);
            model.name = ModelName;
            animation = model.GetComponent<Animation>();
            if (!animation) throw new InvalidOperationException(ModelName + " requires its baked animation clips.");
            PistolClipSeconds = ClipSeconds("PistolFire");
            var swim = animation.GetClip("Swim");
            if (!swim || swim.length <= 0f) throw new InvalidOperationException(ModelName + " requires a swimming cycle.");
            swimCycleSeconds = swim.length;
            animation.playAutomatically = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            animation.Stop();
            renderers = model.GetComponentsInChildren<Renderer>(true);
            hammer = Array.Find(renderers, renderer => renderer.name == "RepairHammer");
            if (!hammer) throw new InvalidOperationException(ModelName + " is missing its repair hammer.");
            pistol = Array.Find(renderers, renderer => renderer.name == "Pistol");
            cutlass = Array.Find(renderers, renderer => renderer.name == "Cutlass");
            muzzle = pistol ? pistol.transform.Find("PistolMuzzle") : null;
            if (!pistol || !cutlass || !muzzle)
                throw new InvalidOperationException(ModelName + " is missing a hand weapon or its muzzle.");
            var bottleRenderer = Array.Find(renderers, renderer => renderer.name == "MedicineBottle");
            if (!bottleRenderer) throw new InvalidOperationException(ModelName + " is missing its medicine bottle.");
            medicineBottle = bottleRenderer.gameObject;
            var hierarchy = model.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name)
            {
                Transform found = null;
                foreach (var candidate in hierarchy)
                {
                    if (candidate.name != name) continue;
                    if (found) throw new InvalidOperationException(ModelName + " has duplicate bone " + name);
                    found = candidate;
                }
                return found ?? throw new InvalidOperationException(ModelName + " is missing bone " + name);
            }
            spine = Bone("Spine");
            rightShoulder = Bone("UpperArm.R");
            animation.AddClip(animation.GetClip("Run"), "PistolStride");
            pistolStride = animation["PistolStride"];
            pistolStride.layer = 1;
            pistolStride.speed = 0f;
            pistolStride.wrapMode = WrapMode.Loop;
            pistolStride.AddMixingTransform(Bone("Pelvis"), false);
            pistolStride.AddMixingTransform(Bone("Thigh.L"), true);
            pistolStride.AddMixingTransform(Bone("Thigh.R"), true);
            medicineHold = animation["MedicineHold"];
            if (medicineHold == null) throw new InvalidOperationException(ModelName + " is missing its medicine pose.");
            medicineHold.layer = 1;
            medicineHold.speed = 0f;
            medicineHold.weight = 1f;
            medicineHold.AddMixingTransform(rightShoulder, true);
            foreach (var renderer in renderers) renderer.gameObject.layer = parent.gameObject.layer;
            ShowWeapon(CrewWeapon.None);
            Pose("Idle", 0f, 1f);
            standingSpine = Quaternion.Inverse(stance.rotation) * spine.rotation;
        }

        internal void ShowWeapon(CrewWeapon weapon)
        {
            pistol.enabled = weapon == CrewWeapon.Pistol;
            cutlass.enabled = weapon == CrewWeapon.Cutlass;
            SetMedicineHeld(weapon == CrewWeapon.Medicine);
        }

        internal void SetMedicineHeld(bool held)
        {
            medicineHeld = held;
            if (RefreshMedicineVisual() && current != null) animation.Sample();
        }

        bool RefreshMedicineVisual()
        {
            // Walking and standing keep their base clips. Only the holding arm is replaced.
            bool show = medicineHeld && (current?.name == "Idle" || current?.name == "Run");
            if (medicineBottle.activeSelf == show) return false;
            medicineBottle.SetActive(show);
            medicineHold.enabled = show;
            return true;
        }

        internal float ClipSeconds(string clip)
        {
            var asset = animation.GetClip(clip);
            if (!asset || asset.length <= 0f)
                throw new InvalidOperationException("Missing or empty " + ModelName + " clip: " + clip);
            return asset.length;
        }

        internal void Face(float degrees, bool snap, float dt)
        {
            facing = snap ? degrees : Mathf.MoveTowardsAngle(facing, degrees, 620f * dt);
            stance.localRotation = Quaternion.Euler(0f, facing, 0f);
        }

        internal void Place(Vector3 localFoot, float dt)
        {
            stance.localPosition = Vector3.MoveTowards(stance.localPosition, localFoot, 4.5f * dt);
        }

        internal void Swim(float secondsInWater, float dt) => Pose("Swim", secondsInWater / swimCycleSeconds, dt);

        internal void Pose(string clip, float phase, float dt)
        {
            RestorePistolAim();
            pistolStride.enabled = false;
            strideWeight = 0f;
            SamplePose(clip, phase, dt);
        }

        internal void PosePistol(float phase, bool moving, float stride, float dt, float pitch)
        {
            RestorePistolAim();
            strideWeight = Mathf.MoveTowards(strideWeight, moving ? 1f : 0f, 9f * dt);
            pistolStride.enabled = strideWeight > 0f;
            pistolStride.weight = strideWeight;
            pistolStride.normalizedTime = Mathf.Repeat(stride, 1f);
            SamplePose("PistolFire", phase, dt);

            sampledSpine = spine.localRotation;
            sampledShoulder = rightShoulder.localRotation;
            // The source raises the pistol through sample 14 of 36, holds it
            // through 25 and then lowers it. Leave the low carry pose intact.
            float raised = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase * 36f / 14f));
            float lowered = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase * 36f - 25f) / 11f));
            aimPitch = pitch * raised * (1f - lowered);
            posingPistol = true;
            ApplyPistolAim();
        }

        internal void ApplyPistolAim()
        {
            if (!posingPistol) return;
            // Rebuild from the sampled local rotations before each application:
            // a hit query runs during Tick, and Legacy Animation resamples before LateUpdate.
            spine.localRotation = sampledSpine;
            rightShoulder.localRotation = sampledShoulder;
            Quaternion upperBody = Quaternion.Slerp(spine.rotation, stance.rotation * standingSpine, strideWeight);
            spine.rotation = Quaternion.AngleAxis(aimPitch * .25f, stance.right) * upperBody;
            rightShoulder.rotation = Quaternion.AngleAxis(aimPitch * .75f, stance.right) * rightShoulder.rotation;
        }

        void RestorePistolAim()
        {
            if (!posingPistol) return;
            spine.localRotation = sampledSpine;
            rightShoulder.localRotation = sampledShoulder;
            posingPistol = false;
        }

        void SamplePose(string clip, float phase, float dt)
        {
            if (current == null || current.name != clip)
            {
                var next = animation[clip];
                if (next == null) throw new InvalidOperationException("Missing " + ModelName + " clip: " + clip);
                if (previous != null) previous.enabled = false;
                previous = current;
                current = next;
                current.wrapMode = current.clip.wrapMode;
                current.speed = 0f;
                current.enabled = true;
                blend = previous == null ? 1f : 0f;
                hammer.enabled = clip == "Repair";
                hurtbox.enabled = clip != "Prone";
                hurtbox.direction = clip == "Swim" ? 2 : 1;
                hurtbox.center = clip == "Swim" ? Vector3.zero : Vector3.up * .90f;
                RefreshMedicineVisual();
            }

            // The gameplay clock drives the feet, pump lever and hammer, including while replaying input.
            current.normalizedTime = current.wrapMode == WrapMode.ClampForever
                ? Mathf.Clamp01(phase) : Mathf.Repeat(phase, 1f);
            blend = Mathf.MoveTowards(blend, 1f, dt * 7f);
            current.weight = blend;
            if (previous != null)
            {
                previous.weight = 1f - blend;
                if (blend >= 1f)
                {
                    previous.enabled = false;
                    previous = null;
                }
            }
            animation.Sample();
        }

        internal void LieDown(Vector3 forward, Vector3 up)
        {
            stance.localPosition = Vector3.zero;
            stance.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, up).normalized, up);
            ShowWeapon(CrewWeapon.None);
            SetVisible(true);
            Pose("Prone", 0f, 1f);
        }

        internal void Remove()
        {
            stance.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(stance.gameObject);
        }

        internal void SetVisible(bool show)
        {
            if (visible == show) return;
            visible = show;
            foreach (var renderer in renderers)
                renderer.shadowCastingMode = show ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }
    }
}
