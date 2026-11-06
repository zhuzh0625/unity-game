using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emberfall
{
    [Serializable] public class Gear
    {
        public string name;
        public int slot, power, vitality, rarity, affix, upgradeLevel;
        public string DisplayName => Chinese.ItemName(name) + (upgradeLevel > 0 ? $" +{upgradeLevel}" : "");
        public string AffixName => affix == 1 ? "疾速" : affix == 2 ? "汲取" : affix == 3 ? "凝神" : "无词缀";
        public string AffixDescription => affix == 1 ? "挥剑速度提升 10%" : affix == 2 ? "伤害的 4% 转为生命" : affix == 3 ? "技能冷却缩短 8%，专注回复 +1" : "无额外效果";
        public int SellValue => 6 + rarity * 6 + upgradeLevel * 8;
        public int UpgradeCost => 20 + rarity * 10 + upgradeLevel * 20;
        public string Description => $"{DisplayName}  ·  {(slot == 0 ? "攻击" : "护甲")} +{power}  生命 +{vitality}";
        public static Gear Roll(int tier = 0)
        {
            int rarity = UnityEngine.Random.value < .18f + tier * .15f ? 2 : UnityEngine.Random.value < .55f ? 1 : 0;
            int slot = UnityEngine.Random.Range(0, 2);
            return new Gear { slot = slot, rarity = rarity, power = UnityEngine.Random.Range(2, 5) + rarity * 3 + tier * 2,
                vitality = UnityEngine.Random.Range(0, 4) + rarity * 3, affix = rarity > 0 ? UnityEngine.Random.Range(1, 4) : 0,
                name = new[] { "Worn", "Tempered", "Emberbound" }[rarity] + (slot == 0 ? " Sword" : " Coat") };
        }
    }

    [Serializable] public class AdventureData
    {
        public int version = 1, potions = 5, gold, victories;
        public bool bossDefeated, returned, muted;
        public Gear weapon = new Gear { name = "Camp Sword", slot = 0, power = 2 };
        public Gear armor = new Gear { name = "Travel Coat", slot = 1, power = 1 };
        public List<Gear> bag = new List<Gear>();
        public int Attack => 12 + weapon.power;
        public int MaxHealth => 100 + weapon.vitality + armor.vitality;
        public int AffixCount(int value) => (weapon.affix == value ? 1 : 0) + (armor.affix == value ? 1 : 0);
        public float AttackInterval => .38f / (1 + AffixCount(1) * .1f);
        public float SkillCooldownFactor => 1 - AffixCount(3) * .08f;
        public float FocusRecovery => 9 + AffixCount(3);
        public float Leech => AffixCount(2) * .04f;
        public int ExpeditionRank => Mathf.Clamp(victories, 0, 5);
    }

    public static class AdventureSave
    {
        public static string VerificationPath;
        public static string PathName => VerificationPath ?? System.IO.Path.Combine(
#if UNITY_EDITOR
            Application.dataPath + "/../SaveData",
#else
            Application.persistentDataPath,
#endif
            "emberfall.json");
        public static void Write(AdventureData data, string path = null)
        {
            path = path ?? PathName;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        public static AdventureData Read(string path = null)
        {
            path = path ?? PathName;
            foreach (var candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var data = JsonUtility.FromJson<AdventureData>(File.ReadAllText(candidate));
                    if (data == null || data.version != 1 || data.weapon == null || data.armor == null || data.bag == null || data.bag.Count > 16)
                        throw new InvalidDataException("Unsupported or invalid save");
                    if (!ValidGear(data.weapon, 0) || !ValidGear(data.armor, 1) || data.bag.Exists(g => !ValidGear(g)))
                        throw new InvalidDataException("Invalid gear in save");
                    data.potions = Mathf.Clamp(data.potions, 0, 9);
                    data.gold = Mathf.Max(0, data.gold); data.victories = Mathf.Max(0, data.victories);
                    return data;
                }
                catch (Exception ex) { Debug.LogWarning("Save unavailable: " + ex.Message); }
            }
            return new AdventureData();
        }
        static bool ValidGear(Gear gear, int slot = -1) => gear != null && gear.name != null &&
            gear.slot >= 0 && gear.slot <= 1 && (slot < 0 || gear.slot == slot) &&
            gear.power >= 0 && gear.power <= 1000 && gear.vitality >= 0 && gear.vitality <= 1000 &&
            gear.rarity >= 0 && gear.rarity <= 2 && gear.affix >= 0 && gear.affix <= 3 &&
            gear.upgradeLevel >= 0 && gear.upgradeLevel <= 3;
    }
}
