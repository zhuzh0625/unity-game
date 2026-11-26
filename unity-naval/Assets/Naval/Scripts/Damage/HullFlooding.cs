using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galleon
{
    /// <summary>Tracks breaches and the waterline in metres above the ship's keel.</summary>
    [DisallowMultipleComponent]
    public sealed class HullFlooding : MonoBehaviour
    {
        public float DryHeight => rig.Definition.Data.dryHeight;
        public float SinkingHeight => rig.Definition.SinkingHeight;
        private const float IngressPerLeak = 0.027f;
        private const float PumpRate = 0.146f;
        internal const float NeighbourRadius = 3.4f;
        private const float OuterHalfWidth = .78f, OuterHalfHeight = .56f;
        private const float InnerHalfWidth = .46f, InnerHalfHeight = .32f;

        private sealed class Leak
        {
            public bool Open => Inside != null;
            public bool SignalLit;
            public GameObject Inside, Outside;
            public Bounds InsideBounds, OutsideBounds;
        }

        private GalleonRig rig;
        private Transform hull;
        private Leak[] leaks;
        private readonly List<(float distanceSquared, int index)> nearby = new();
        private HullDecalSurface insideSurface;
        private HullDecalSurface outsideSurface;
        private HoldWaterSurface water;
        private Material decalMaterial;
        private Material litDecalMaterial;
        private Material waterMaterial;
        private Material waterVolumeMaterial;

        public int ActiveLeaks { get; private set; }
        public float WaterHeight { get; private set; }
        public float WaterLevel01 => Mathf.InverseLerp(DryHeight, SinkingHeight, WaterHeight);
        public bool IsSunk => WaterHeight >= SinkingHeight;
        public event System.Action Sunk;
        public bool WaterVisible => water != null && water.Visible;
        public int ActiveDecals { get; private set; }

        public void Initialize(GalleonRig source)
        {
            if (rig == source && leaks != null)
                return;

            ReleaseVisuals();
            rig = source;
            hull = source.transform;
            leaks = new Leak[Mathf.Min(rig.LeakInside.Length, rig.LeakOutside.Length)];
            for (int i = 0; i < leaks.Length; i++)
                leaks[i] = new Leak();

            ActiveLeaks = 0;
            ActiveDecals = 0;
            WaterHeight = DryHeight;
            insideSurface = new HullDecalSurface(hull, rig.InnerPlanking);
            outsideSurface = new HullDecalSurface(hull, rig.OuterPlanking);

            decalMaterial = LoadMaterial("HullImpact");
            litDecalMaterial = LoadMaterial("HullImpactLit");
            Texture2D impactMap = Resources.Load<Texture2D>("Damage/SplitOak");
            if (decalMaterial != null)
                decalMaterial.SetTexture("_ImpactMap", impactMap);
            if (litDecalMaterial != null)
                litDecalMaterial.SetTexture("_ImpactMap", impactMap);

            waterMaterial = LoadMaterial("HoldWater");
            waterVolumeMaterial = LoadMaterial("HoldWaterVolume");
            if (waterMaterial != null && waterVolumeMaterial != null)
                water = new HoldWaterSurface(rig, waterMaterial, waterVolumeMaterial);
        }

        public bool Hit(Vector3 worldPoint, Vector3 worldNormal)
        {
            if (IsSunk || leaks == null || leaks.Length == 0 || worldNormal.sqrMagnitude < 0.001f)
                return false;

            Vector3 point = hull.InverseTransformPoint(worldPoint);
            Vector3 normal = hull.InverseTransformDirection(worldNormal).normalized;
            Mesh impact = outsideSurface.Project(point, normal, OuterHalfWidth, OuterHalfHeight, out bool fits);
            if (!impact)
                return false;

            Physics.SyncTransforms();
            FindNearbySites(point, normal);
            bool impactIsClear = fits && !OverlapsOpenLeak(impact.bounds, false);
            for (int order = 0; order < nearby.Count; order++)
            {
                int index = nearby[order].index;
                if (!CanUseSite(index)) continue;
                // The direct hit belongs to the nearest station. A replacement uses its own
                // exterior anchor, so the mark and the repair stay on the same part of the hull.
                if (order == 0 && impactIsClear && TryOpen(index, impact)) return true;
                if (TryOpenAtMarker(index)) break;
            }
            Destroy(impact);
            // Hitting solid planking still counts when every local repair site is occupied.
            return true;
        }

        public void StartDrill()
        {
            if (leaks == null || leaks.Length < 2 || ActiveLeaks != 0 || WaterLevel01 > 0f)
                return;

            Physics.SyncTransforms();
            if (CanUseSite(0)) TryOpenAtMarker(0);
            int last = leaks.Length - 1;
            if (CanUseSite(last)) TryOpenAtMarker(last);
        }

        public bool IsSignalLit(int index) => IsLeaking(index) && leaks[index].SignalLit;

        public bool IsLeaking(int index)
        {
            return leaks != null && index >= 0 && index < leaks.Length && leaks[index].Open;
        }

        public bool Repair(int index)
        {
            if (IsSunk || !IsLeaking(index))
                return false;

            ClearLeak(leaks[index]);
            return true;
        }

        private void ClearLeak(Leak leak)
        {
            leak.SignalLit = false;
            ActiveLeaks--;
            DisposeVisual(leak.Inside);
            DisposeVisual(leak.Outside);
            leak.Inside = leak.Outside = null;
            ActiveDecals -= 2;
        }

        public void Pump(float deltaSeconds)
        {
            if (!IsSunk && deltaSeconds > 0f)
                SetWater(WaterHeight - PumpRate * deltaSeconds);
        }

        private void Update()
        {
            if (ActiveDecals != 0 && litDecalMaterial != null)
            {
                for (int i = 0; i < leaks.Length; i++)
                {
                    Leak leak = leaks[i];
                    if (!leak.Open) continue;
                    bool lit = Mathf.Repeat(Time.time + i * 0.23f, 1.08f) < 0.46f;
                    if (lit == leak.SignalLit)
                        continue;

                    leak.SignalLit = lit;
                    Material material = lit ? litDecalMaterial : decalMaterial;
                    leak.Inside.GetComponent<MeshRenderer>().sharedMaterial = material;
                    leak.Outside.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
            }
        }

        private void LateUpdate()
        {
            // Crew actions resolve first, so an operating pump can save the ship on the last frame.
            if (!IsSunk && ActiveLeaks != 0)
                SetWater(WaterHeight + ActiveLeaks * IngressPerLeak * Time.deltaTime);
        }

        private void SetWater(float height)
        {
            height = Mathf.Clamp(height, DryHeight, SinkingHeight);
            if (WaterHeight == height) return;
            WaterHeight = height;
            water?.SetLevel(height);
            if (IsSunk) Sunk?.Invoke();
        }

        private void FindNearbySites(Vector3 point, Vector3 normal)
        {
            nearby.Clear();
            float side = Mathf.Sign(Mathf.Abs(point.x) > .01f ? point.x : normal.x);
            var anchors = rig.Definition.Data.outside;
            for (int i = 0; i < leaks.Length; i++)
            {
                Vector3 anchor = anchors[i].position;
                if (anchor.x * side <= 0f) continue;
                float distanceSquared = (anchor - point).sqrMagnitude;
                if (distanceSquared <= NeighbourRadius * NeighbourRadius)
                    nearby.Add((distanceSquared, i));
            }
            nearby.Sort(); // Nearest first, with the rig index breaking equal-distance ties.
        }

        private bool CanUseSite(int index)
        {
            var stand = rig.Definition.Data.repairStands[index];
            return !leaks[index].Open && stand != null && decalMaterial && litDecalMaterial &&
                   CrewPlayer.CanStandAndRepair(hull, stand.position, rig.LeakInside[index]);
        }

        private bool TryOpenAtMarker(int index)
        {
            var anchor = rig.Definition.Data.outside[index];
            Mesh exterior = outsideSurface.Project(anchor.position, anchor.forward, OuterHalfWidth, OuterHalfHeight, out bool fits);
            if (!exterior) return false;
            if (fits && !OverlapsOpenLeak(exterior.bounds, false) && TryOpen(index, exterior)) return true;
            Destroy(exterior);
            return false;
        }

        private bool OverlapsOpenLeak(Bounds footprint, bool interior)
        {
            // Both meshes and their bounds are in hull space, independent of the ship's pose.
            footprint.Expand(.04f);
            foreach (var leak in leaks)
                if (leak.Open && footprint.Intersects(interior ? leak.InsideBounds : leak.OutsideBounds))
                    return true;
            return false;
        }

        // The caller owns the exterior mesh until both faces are successfully installed.
        private bool TryOpen(int index, Mesh exterior)
        {
            var anchor = rig.Definition.Data.inside[index];
            Mesh interior = insideSurface.Project(anchor.position, anchor.forward, InnerHalfWidth, InnerHalfHeight, out bool fits);
            if (!interior) return false;
            if (!fits || OverlapsOpenLeak(interior.bounds, true))
            {
                Destroy(interior);
                return false;
            }

            Leak leak = leaks[index];
            leak.InsideBounds = interior.bounds;
            leak.OutsideBounds = exterior.bounds;
            leak.Inside = CreateVisual(interior, $"Hull damage {index + 1} - hold");
            leak.Outside = CreateVisual(exterior, $"Hull damage {index + 1} - exterior");
            ActiveDecals += 2;
            ActiveLeaks++;
            return true;
        }

        private GameObject CreateVisual(Mesh mesh, string label)
        {
            GameObject visual = new(label);
            visual.transform.SetParent(hull, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = decalMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return visual;
        }

        private static Material LoadMaterial(string name)
        {
            Shader shader = Resources.Load<Shader>($"Damage/{name}");
            if (shader != null)
                return new Material(shader) { name = name };

            Debug.LogError($"Damage shader {name} is missing.");
            return null;
        }

        internal static void DisposeVisual(GameObject visual)
        {
            if (visual == null)
                return;

            visual.SetActive(false);
            Destroy(visual.GetComponent<MeshFilter>().sharedMesh);
            Destroy(visual);
        }

        private void ReleaseVisuals()
        {
            if (leaks != null)
            {
                foreach (Leak leak in leaks)
                    if (leak.Open) ClearLeak(leak);
            }

            water?.Dispose();
            water = null;
            Destroy(decalMaterial);
            Destroy(litDecalMaterial);
            Destroy(waterMaterial);
            Destroy(waterVolumeMaterial);
        }

        private void OnDestroy()
        {
            ReleaseVisuals();
        }
    }
}
