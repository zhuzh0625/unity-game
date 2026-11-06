using UnityEngine;

namespace Emberfall
{
    // Shared by input, combat, HUD and verification; values no longer allocate arrays per attack.
    public static class AdventureRules
    {
        public static readonly int[] Cost = { 0, 22, 28, 18 };
        public static readonly float[] Cooldown = { .38f, 3.5f, 6f, 2f };
        public static readonly float[] Windup = { .11f, .18f, .16f, .12f };
        public static readonly float[] Range = { 1.9f, 3.2f, 4.5f, 10f };
        public static readonly string[] Names = { "挥剑", "顺劈", "冰霜", "余烬" };
        public static readonly string[] Keys = { "J", "U", "I", "O" };
        public static readonly string[] Zones = { "余烬营地", "灰烬荒野", "幽寂古墓" };
        public const float DodgeCooldown = 2.2f, DodgeDuration = .2f;
    }
}
