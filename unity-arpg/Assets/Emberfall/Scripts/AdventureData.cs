using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emberfall
{
    [Serializable] public class Gear
    {
        public string name;
        public int slot, power, vitality, rarity;
        public string Description => $"{Chinese.ItemName(name)}  ·  {(slot == 0 ? "攻击" : "护甲")} +{power}  生命 +{vitality}";
        public static Gear Roll(int tier = 0)
        {
            int rarity = UnityEngine.Random.value < .18f + tier * .15f ? 2 : UnityEngine.Random.value < .55f ? 1 : 0;
            int slot = UnityEngine.Random.Range(0, 2);
            return new Gear { slot = slot, rarity = rarity, power = UnityEngine.Random.Range(2, 5) + rarity * 3 + tier * 2,
                vitality = UnityEngine.Random.Range(0, 4) + rarity * 3,
                name = new[] { "Worn", "Tempered", "Emberbound" }[rarity] + (slot == 0 ? " Sword" : " Coat") };
        }
    }

    [Serializable] public class AdventureData
    {
        public int version = 1, potions = 5, gold, victories;
        public bool bossDefeated, returned;
        public Gear weapon = new Gear { name = "Camp Sword", slot = 0, power = 2 };
        public Gear armor = new Gear { name = "Travel Coat", slot = 1, power = 1 };
        public List<Gear> bag = new List<Gear>();
        public int Attack => 12 + weapon.power;
        public int MaxHealth => 100 + weapon.vitality + armor.vitality;
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
                    data.potions = Mathf.Clamp(data.potions, 0, 9);
                    return data;
                }
                catch (Exception ex) { Debug.LogWarning("Save unavailable: " + ex.Message); }
            }
            return new AdventureData();
        }
    }
}
