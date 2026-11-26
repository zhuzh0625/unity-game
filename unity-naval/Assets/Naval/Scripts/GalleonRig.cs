using System;
using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    /// <summary>Ships use metres, starboard +X, up +Y and bow +Z, with Y=0 at the keel reference.</summary>
    public sealed class GalleonRig : MonoBehaviour
    {
        public Transform Spawn, Helm;
        public Transform[] Pumps, Guns, LeakInside, LeakOutside;
        public MeshFilter[] OuterPlanking, InnerPlanking;
        public ShipDefinition Definition { get; private set; }
        internal Texture2D SectionTexture { get; private set; }
        Transform wheel;
        Transform[] levers;
        readonly List<Transform> canvas = new();
        Vector3 wheelAxis, pumpAxis;

        public static GalleonRig Create(Transform ship, bool pirate, ShipDefinition definition)
        {
            var asset = Resources.Load<GameObject>(definition.ModelPath);
            if (!asset) throw new InvalidOperationException("Missing ship model: " + definition.ModelPath);
            var model = Instantiate(asset, ship, false);
            model.name = definition.DisplayName;
            var rig = model.AddComponent<GalleonRig>();
            rig.Definition = definition;
            rig.SectionTexture = definition.CreateSectionTexture();
            rig.Bind(pirate);
            return rig;
        }

        void Bind(bool pirate)
        {
            var measures = Definition.Data;
            transform.Find("FlagsPirate").gameObject.SetActive(pirate);
            transform.Find("FlagsBritish").gameObject.SetActive(!pirate);
            wheel = transform.Find("Wheel");
            levers = new Transform[measures.pumps.Length];
            for (int i = 0; i < levers.Length; i++) levers[i] = transform.Find("PumpLever" + i);
            wheelAxis = measures.wheelAxis == Vector3.zero ? Vector3.forward : measures.wheelAxis;
            pumpAxis = measures.pumpAxis == Vector3.zero ? Vector3.right : measures.pumpAxis;
            var outer = new List<MeshFilter>();
            var inner = new List<MeshFilter>();
            foreach (Transform part in transform)
            {
                if (part.name.StartsWith("Sail", StringComparison.Ordinal)) canvas.Add(part);
                if (!part.TryGetComponent<MeshFilter>(out var filter)) continue;
                if (part.name.StartsWith("HULL_", StringComparison.Ordinal) || part.name == "TRANSOM_PLANKING") outer.Add(filter);
                if (part.name.StartsWith("INNER_", StringComparison.Ordinal)) inner.Add(filter);
            }
            OuterPlanking = outer.ToArray();
            InnerPlanking = inner.ToArray();
            Spawn = Marker(measures.spawn);
            Helm = Marker(measures.helm);
            Pumps = Markers(measures.pumps, "Pumps");
            Guns = Markers(measures.guns,"Guns");
            LeakInside = Markers(measures.inside,"Inside");
            LeakOutside = Markers(measures.outside,"Outside");
        }

        Transform Marker(ShipMark mark) => Point(mark.name, mark.position, mark.forward);

        Transform Point(string label,Vector3 local,Vector3 forward,Transform parent=null)
        {
            var point = new GameObject(label).transform;
            point.SetParent(parent ? parent : transform,false);
            point.localPosition = local;
            point.localRotation = Quaternion.LookRotation(forward,Vector3.up);
            return point;
        }

        Transform[] Markers(ShipMark[] marks,string group)
        {
            var groupTransform = Point(group, Vector3.zero, Vector3.forward);
            var result = new Transform[marks.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = Point(marks[i].name,marks[i].position,marks[i].forward,groupTransform);
            return result;
        }

        public void SetPumpStroke(int index,float phase01)
        {
            if (index < 0 || index >= levers.Length || !levers[index]) return;
            // Both handles point aft. Their local rotation axes have opposite signs.
            float stroke = .5f - .5f * Mathf.Cos(phase01 * 2f * Mathf.PI);
            float angle = -19f * Mathf.Sign(pumpAxis.x) * stroke;
            levers[index].localRotation = Quaternion.AngleAxis(angle,pumpAxis);
        }

        public void SetWheel(float signed01)
        {
            if (wheel) wheel.localRotation = Quaternion.AngleAxis(-Mathf.Clamp(signed01,-1f,1f)*120f,wheelAxis);
        }

        public void SetSail(float fraction01)
        {
            float drop = Mathf.Lerp(.12f,1f,Mathf.Clamp01(fraction01));
            for (int i = 0; i < canvas.Count; i++)
            {
                if (!canvas[i]) continue;
                // Each cloth group scales perpendicular to its yard.
                canvas[i].localScale = new Vector3(1f,drop,1f);
            }
        }

        void OnDestroy()
        {
            if (SectionTexture) Destroy(SectionTexture);
        }
    }
}
