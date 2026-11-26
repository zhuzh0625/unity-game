using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Galleon
{
    public sealed partial class VoyageHud
    {
        const int KillFeedCapacity = 5;
        const float KillFeedSeconds = 6f;
        static readonly Color32 FriendlyName = new(0x85, 0xd9, 0xf3, 255);
        static readonly Color32 EnemyName = new(0xff, 0x99, 0x85, 255);
        static readonly Vector2 LeftMiddle = new(0f, .5f);

        sealed class KillRow
        {
            public RectTransform Panel;
            public TextMeshProUGUI Attacker, Weapon, Victim;
            public RawImage Icon;
        }

        struct KillEntry
        {
            public string Attacker, Victim;
            public Color32 AttackerColor, VictimColor;
            public CrewKillWeapon Weapon;
            public float Expires;
        }

        RectTransform killFeed;
        readonly KillRow[] killRows = new KillRow[KillFeedCapacity];
        readonly List<KillEntry> recentKills = new(KillFeedCapacity);

        void BuildKillFeed()
        {
            killFeed = Box(gameplay, "KillFeed", Vector2.one, new Vector2(-24f, -196f), new Vector2(380f, 176f));
            var atlas = Resources.Load<Texture2D>("UI/KillFeedWeapons");
            for (int i = 0; i < killRows.Length; i++)
            {
                var panel = Panel(killFeed, "KillRow" + i, Vector2.one, new Vector2(0f, -36f * i),
                    new Vector2(250f, 32f), new Color(.035f, .051f, .063f, .90f));
                var row = new KillRow
                {
                    Panel = panel,
                    Attacker = Label(panel, "Attacker", LeftMiddle, Vector2.zero, new Vector2(125f, 32f), 17, Paper),
                    Victim = Label(panel, "Victim", LeftMiddle, Vector2.zero, new Vector2(125f, 32f), 17, Paper),
                    Weapon = Label(panel, "Weapon", LeftMiddle, Vector2.zero, new Vector2(34f, 32f), 15, Paper),
                    Icon = Box(panel, "WeaponIcon", LeftMiddle, Vector2.zero, new Vector2(38f, 19f))
                        .gameObject.AddComponent<RawImage>()
                };
                row.Icon.texture = atlas;
                row.Icon.color = Paper;
                row.Icon.raycastTarget = false;
                row.Attacker.fontStyle = row.Victim.fontStyle = FontStyles.Bold;
                row.Attacker.overflowMode = row.Victim.overflowMode = TextOverflowModes.Ellipsis;
                panel.gameObject.SetActive(false);
                killRows[i] = row;
            }
        }

        internal void ShowKill(CrewKill kill, bool attackerFriendly, bool victimFriendly)
        {
            if (recentKills.Count == KillFeedCapacity) recentKills.RemoveAt(KillFeedCapacity - 1);
            recentKills.Insert(0, new KillEntry
            {
                Attacker = CrewName(kill.Attacker, attackerFriendly), Victim = CrewName(kill.Victim, victimFriendly),
                AttackerColor = attackerFriendly ? FriendlyName : EnemyName,
                VictimColor = victimFriendly ? FriendlyName : EnemyName,
                Weapon = kill.Weapon, Expires = Time.time + KillFeedSeconds
            });
            for (int i = 0; i < recentKills.Count; i++) DrawKill(killRows[i], recentKills[i]);
        }

        string CrewName(CrewDamageTarget member, bool friendly)
        {
            if (member == game.Crew) return "你";
            if (member is CombatDummy) return member.name;
            return (friendly ? "海盗" : "海军") + member.name;
        }

        static void DrawKill(KillRow row, KillEntry entry)
        {
            row.Panel.gameObject.SetActive(true);
            row.Attacker.text = entry.Attacker;
            row.Attacker.color = entry.AttackerColor;
            row.Victim.text = entry.Victim;
            row.Victim.color = entry.VictimColor;
            var (weaponName, spriteColumn) = entry.Weapon switch
            {
                CrewKillWeapon.Pistol => ("手枪", 0),
                CrewKillWeapon.Cutlass => ("砍刀", 1),
                CrewKillWeapon.Cannon => ("舷炮", 2),
                _ => throw new System.ArgumentOutOfRangeException(nameof(entry.Weapon))
            };
            row.Weapon.text = weaponName;
            row.Icon.uvRect = new Rect(spriteColumn / 3f, 0f, 1f / 3f, 1f);

            float x = 11f;
            x += PlaceName(row.Attacker, x) + 12f;
            row.Icon.rectTransform.anchoredPosition = new Vector2(x, 0f);
            x += row.Icon.rectTransform.sizeDelta.x + 3f;
            row.Weapon.rectTransform.anchoredPosition = new Vector2(x, 0f);
            x += row.Weapon.rectTransform.sizeDelta.x + 10f;
            x += PlaceName(row.Victim, x) + 11f;
            row.Panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, x);
        }

        static float PlaceName(TMP_Text label, float x)
        {
            float width = Mathf.Min(125f, Mathf.Ceil(label.preferredWidth) + 3f);
            label.rectTransform.anchoredPosition = new Vector2(x, 0f);
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            return width;
        }

        void ExpireKills()
        {
            while (recentKills.Count > 0 && recentKills[recentKills.Count - 1].Expires <= Time.time)
            {
                int last = recentKills.Count - 1;
                killRows[last].Panel.gameObject.SetActive(false);
                recentKills.RemoveAt(last);
            }
        }

        void ClearKillFeed()
        {
            recentKills.Clear();
            foreach (var row in killRows) row.Panel.gameObject.SetActive(false);
        }
    }
}
