using System;
using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    public enum ShipType { Sloop = 1, Brigantine = 2 }

    [Serializable]
    internal sealed class ShipMark
    {
        public string name;
        public Vector3 position, forward;
    }

    [Serializable]
    internal sealed class BoardingLadder
    {
        public string name;
        public float halfWidth, innerHalfWidth;
        public Vector3 bottom, top, landing;
    }

    [Serializable]
    internal sealed class ShipMeasurements
    {
        public string sourceModel, sourceSha256;
        public float length, beam, dryDraft, dryHeight, lowerDeck, mainDeck, upperDeck, deckCrown;
        public float holdAft, holdFore, holdCeiling, liningInset, lowerAft, lowerFore, maxHullHeight;
        public float[] widths;
        public ShipMark spawn, helm;
        public ShipMark[] pumps, guns, inside, outside, repairStands;
        public BoardingLadder[] boardingLadders;
        public Vector3 wheelAxis, pumpAxis;
    }

    /// <summary>Dimensions, section lines and measured work stations, in metres in the model's frame.</summary>
    public sealed class ShipDefinition
    {
        internal const int Columns = 62, Rows = 51;
        static readonly Dictionary<ShipType, ShipDefinition> cache = new();
        internal ShipMeasurements Data { get; }
        public ShipType Type { get; }
        public string DisplayName { get; }
        public string ModelPath { get; }
        public float Length => Data.length;
        public float Beam => Data.beam;
        public float DryDraft => Data.dryDraft;
        internal float EqualizationHeight { get; }
        public float SinkingHeight => Data.deckCrown + .10f;
        public float PivotZ => Length * .5f;
        // Wave shaders use the same grid spacing and clipping envelope as the standing water.
        internal Vector4 SectionBounds => new(Length, Data.maxHullHeight, Beam * .5f + .12f, Data.liningInset * .5f);

        public static ShipDefinition For(ShipType type)
        {
            if (!cache.TryGetValue(type, out var definition))
                cache.Add(type, definition = new ShipDefinition(type));
            return definition;
        }

        ShipDefinition(ShipType type)
        {
            Type = type;
            string measurementsPath;
            (DisplayName, ModelPath, measurementsPath) = type switch
            {
                ShipType.Sloop => ("小型单桅船", "Fleet/Sloop/Model", "Fleet/Sloop/Rig"),
                ShipType.Brigantine => ("中型双桅船", "Fleet/Brigantine/Model", "Fleet/Brigantine/Rig"),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
            var file = Resources.Load<TextAsset>(measurementsPath);
            if (!file) throw new InvalidOperationException("Missing ship measurements: " + measurementsPath);
            Data = JsonUtility.FromJson<ShipMeasurements>(file.text);
            if (Data.widths?.Length != Columns * Rows || Data.guns == null || Data.guns.Length == 0 ||
                Data.pumps == null || Data.pumps.Length == 0 ||
                Data.inside == null || Data.inside.Length < 2 || Data.outside?.Length != Data.inside.Length ||
                Data.repairStands?.Length != Data.inside.Length || Data.boardingLadders == null || Data.boardingLadders.Length < 2 ||
                Data.spawn == null || Data.helm == null ||
                Data.length <= 0f || Data.beam <= 0f || Data.maxHullHeight <= 0f ||
                Data.deckCrown < Data.upperDeck || Data.dryHeight >= Data.deckCrown || Data.dryDraft >= SinkingHeight)
                throw new InvalidOperationException("Incomplete ship measurements: " + measurementsPath);
            float lowestGun = float.PositiveInfinity;
            foreach (var gun in Data.guns) lowestGun = Mathf.Min(lowestGun, gun.position.y);
            EqualizationHeight = Data.lowerDeck > Data.dryDraft ? Data.lowerDeck : (Data.dryDraft + lowestGun) * .5f;
        }

        public float HalfBreadth(float forwardZ, float heightY)
        {
            if (forwardZ < 0 || forwardZ > Length || heightY < 0) return 0f;
            float row = Mathf.Clamp01(heightY / Data.maxHullHeight) * (Rows - 1);
            float col = Mathf.Clamp01(forwardZ / Length) * (Columns - 1);
            int y = Mathf.Min(Rows - 2, (int)row), z = Mathf.Min(Columns - 2, (int)col);
            int k = y * Columns + z;
            float low = Mathf.Lerp(Data.widths[k], Data.widths[k + 1], col - z);
            float high = Mathf.Lerp(Data.widths[k + Columns], Data.widths[k + Columns + 1], col - z);
            return Mathf.Lerp(low, high, row - y);
        }

        internal Texture2D CreateSectionTexture()
        {
            var texture = new Texture2D(Columns, Rows, TextureFormat.RFloat, false, true)
            {
                name = DisplayName + " hull sections", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixelData(Data.widths, 0);
            texture.Apply(false, true);
            return texture;
        }
    }
}
