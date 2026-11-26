using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        static readonly Color32 KillFeedFriendly = new(0x85, 0xd9, 0xf3, 0xff);
        static readonly Color32 KillFeedEnemy = new(0xff, 0x99, 0x85, 0xff);

        public static void BeginKillFeed(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(session, directory, "kill-feed-check", "Right-side kill feed", check.RunKillFeed());
        }

        Transform KillFeedGameplay => game.transform.Find("航海界面/航行界面");
        Transform KillFeedPanel => KillFeedGameplay.Find("KillFeed");
        Transform KillFeedRow(int index) => KillFeedPanel.Find("KillRow" + index);
        TMP_Text KillFeedPart(int row, string part) => KillFeedRow(row).Find(part).GetComponent<TMP_Text>();
        int ShownKills() => Enumerable.Range(0, 5).Count(i => KillFeedRow(i).gameObject.activeInHierarchy);

        IEnumerator RunKillFeed()
        {
            yield return null;
            type = ShipType.Sloop;
            game.SelectShip(type);
            game.StartVoyage();
            Crew.ReadDeviceInput = false;
            Ship.enabled = game.Navy.enabled = false;
            using (new AcceptanceCompanyFixture(Ship, game.Navy))
            {
                yield return new WaitForEndOfFrame();
                Require(KillFeedPanel && ShownKills() == 0 && KillFeedPanel.childCount == 5,
                    "voyage starts with five available kill-feed rows and no visible record");
                CheckKillFeedPosition();

                var allies = Ship.Company.Mates.Where(m => m.Duty == CrewDuty.Gunner).ToArray();
                var enemies = game.Navy.Company.Mates.Where(m => m.Duty == CrewDuty.Gunner).ToArray();
                var friendlyGunner = allies[0];
                var hostileGunner = enemies[0];
                var environment = game.Navy.Company.Mates.First(m => m.Duty == CrewDuty.DamageControl);
                environment.TakeDamage(100);
                yield return new WaitForEndOfFrame();
                Require(environment.Health == 0 && ShownKills() == 0, "environmental death produced no kill record");

                var hostileVictims = enemies.Skip(1).Take(4).ToArray();
                yield return KillFeedCannon(Ship, friendlyGunner, hostileVictims, new Vector3(-20f, 10f, 4f));
                Require(ShownKills() == 4, "four real crew deaths from one broadside projectile produced four records");
                for (int i = 0; i < 4; i++)
                    CheckKillFeedRow(i, "舷炮", true, false, friendlyGunner, hostileVictims);
                var oldVictims = Enumerable.Range(0, 4).Select(i => KillFeedPart(i, "Victim").text).ToArray();
                Require(oldVictims.Distinct().Count() == 4, "a multi-kill recorded each victim once");

                var friendlyVictims = allies.Skip(1).Take(2).ToArray();
                yield return KillFeedCannon(game.Navy, hostileGunner, friendlyVictims, new Vector3(65f, 10f, 4f));
                double latest = Time.timeAsDouble;
                Require(ShownKills() == 5, "a sixth kill kept the newest five visible rows");
                for (int i = 0; i < 2; i++)
                    CheckKillFeedRow(i, "舷炮", false, true, hostileGunner, friendlyVictims);
                for (int i = 2; i < 5; i++)
                {
                    CheckKillFeedRow(i, "舷炮", true, false, friendlyGunner, hostileVictims);
                    Require(KillFeedPart(i, "Victim").text == oldVictims[i - 2], "new kills went to the top and evicted the oldest");
                }
                var newest = KillFeedPart(0, "Victim").text;
                yield return KillFeedCannon(game.Navy, hostileGunner, Array.Empty<CrewMate>(),
                    new Vector3(65f, 10f, 4f), hit: false);
                Require(ShownKills() == 5 && KillFeedPart(0, "Victim").text == newest,
                    "a cannon shot crossing the same already-dead sailors did not add duplicate kills");
                CheckKillFeedPosition();
                yield return PhotographGameCamera("kill-feed-voyage", withHud: true);

                while (Time.timeAsDouble - latest < 5.55) yield return null;
                yield return new WaitForEndOfFrame();
                Require(ShownKills() > 0, "fresh kill rows still visible shortly before six game seconds");
                game.SetPaused(true);
                yield return HoldKeys(12);
                game.SetPaused(false);
                yield return new WaitForEndOfFrame();
                Require(ShownKills() > 0, "a paused game did not spend the kill feed's display time");
                while (Time.timeAsDouble - latest < 6.25) yield return null;
                yield return new WaitForEndOfFrame();
                Require(ShownKills() == 0, "all four- and two-kill records expired after about six game seconds");
            }

            game.ReturnToMenu();
            yield return null;
            yield return CheckPistolKillFeed();
        }

        IEnumerator KillFeedCannon(NavalShip source, CrewMate gunner, CrewMate[] victims, Vector3 cluster, bool hit = true)
        {
            const int gunIndex = 0;
            var heading = NavalShip.ShotDirection(source.Rig.Guns[gunIndex], 0f, 0f);
            var lateral = Vector3.Cross(Vector3.up, heading).normalized;
            for (int i = 0; i < victims.Length; i++)
                victims[i].transform.position = cluster + lateral * (i * .26f);
            Physics.SyncTransforms();
            source.Load(gunIndex);
            Require(source.Fire(gunIndex, 0f, 0f, gunner), $"{gunner.name} fired a real broadside through NavalShip.Fire");
            var projectile = FindObjectsByType<RoundShot>(FindObjectsSortMode.None).Single();
            // Keep the cannon's actual velocity and attribution. Isolate its landing sweep from ship motion.
            projectile.transform.position = cluster + Vector3.up * .90f - heading * .87f;
            if (hit)
            {
                for (int i = 0; projectile && i < 5; i++) yield return null;
                Require(!projectile && victims.All(v => v.Health == 0), "the round consumed its collision and killed the clustered sailors");
            }
            else
            {
                yield return HoldKeys(4);
                Require(projectile, "a first sweep past the dead sailors found no living hurtbox");
                Destroy(projectile.gameObject);
            }
            yield return new WaitForEndOfFrame();
        }

        IEnumerator CheckPistolKillFeed()
        {
            var training = game.Menu.CombatButton;
            yield return HoldKeys(2);
            Require(game.IsInMenu && training.isActiveAndEnabled && training.IsInteractable(),
                "fleet menu is ready for a new training session");
            training.Select();
            yield return KeyTap(Key.Enter);
            yield return HoldKeys(3);
            Require(game.IsCombatTest && game.TestDummies.Count == 2,
                "submitting the training button started a new session with two dummies");
            var left = game.TestDummies[0];
            var right = game.TestDummies[1];
            Require(ShownKills() == 0, "a new training session starts with a clear kill feed");
            combatMouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return KeyTap(Key.Digit1);
                yield return RightClickAim(true);
                yield return AimCombat(() => left.AimPoint);
                yield return ShootCombat(left, right, true, capture: false);
                Require(left.Health == 50 && ShownKills() == 0, "a live pistol hit took 50 HP and added no kill record");
                for (int i = 0; Crew.PistolCooldownRemaining > 0f && i < 110; i++) yield return null;
                yield return AimCombat(() => left.AimPoint);
                yield return ShootCombat(left, right, true, capture: false);
                Require(left.Health == 0 && right.Health == 100 && ShownKills() == 1,
                    "the second live pistol hit killed one target and added exactly one record");
                Require(KillFeedPart(0, "Attacker").text.Trim() == "你" &&
                    KillFeedPart(0, "Weapon").text.Contains("手枪") && KillFeedPart(0, "Victim").text.Contains(left.name),
                    "training kill labels show 你, 手枪 and the struck dummy");
                Require(KillFeedColor(KillFeedPart(0, "Attacker"), KillFeedFriendly) &&
                    KillFeedColor(KillFeedPart(0, "Victim"), KillFeedEnemy), "player was blue and the training dummy was coral");
                CheckKillFeedIcon(0, 0);
                yield return PhotographGameCamera("kill-feed-pistol", withHud: true);
                yield return CheckCutlassKillFeed(right);
            }
            finally
            {
                InputSystem.RemoveDevice(combatMouse);
                combatMouse = null;
            }

            game.ReturnToMenu();
            yield return null;
            game.StartVoyage();
            yield return new WaitForEndOfFrame();
            Require(ShownKills() == 0, "returning to menu and starting a fresh voyage cleared a still-visible training kill");
        }

        IEnumerator CheckCutlassKillFeed(CombatDummy target)
        {
            target.TakeDamage(100 - CrewPlayer.CutlassDamage);
            Require(target.Health == 25, "right-hand dummy prepared at 25 HP for one real cutlass hit");
            yield return KeyTap(Key.Digit2);
            yield return AimCombat(() => target.AimPoint);
            for (int i = 0; i < 70 && CombatDistance(target) > 1.15f; i++)
            {
                var direction = target.transform.position - Crew.transform.position;
                var forward = Vector3.ProjectOnPlane(game.MainCamera.transform.forward, Vector3.up).normalized;
                float z = Vector3.Dot(direction, forward);
                float x = Vector3.Dot(direction, Vector3.Cross(Vector3.up, forward));
                if (Mathf.Abs(x) > .42f * Mathf.Abs(z) && Mathf.Abs(z) > .42f * Mathf.Abs(x))
                    yield return HoldKeys(1, z >= 0f ? Key.W : Key.S, x >= 0f ? Key.D : Key.A);
                else yield return HoldKeys(1, TowardKey(direction));
            }
            yield return HoldKeys(2);
            Require(Crew.Weapon == CrewWeapon.Cutlass && CombatDistance(target) < 1.30f && !Crew.IsSwimming,
                $"keyboard put the equipped cutlass within {CombatDistance(target):F2}m of the right-hand dummy");
            yield return AimCombat(() => target.AimPoint);
            yield return CutlassCameraPitch(48f);
            CombatMouse(true);
            for (int i = 0; i < 100 && target.Health > 0; i++) yield return null;
            CombatMouse();
            yield return new WaitForEndOfFrame();
            Require(target.Health == 0 && ShownKills() > 0 &&
                KillFeedPart(0, "Attacker").text.Trim() == "你" &&
                KillFeedPart(0, "Weapon").text.Trim() == "砍刀" &&
                KillFeedPart(0, "Victim").text.Trim() == target.name,
                "a real cutlass strike placed 你 / 砍刀 / 右侧假人 in the newest row");
            CheckKillFeedIcon(0, 1);
        }

        void CheckKillFeedRow(int index, string weapon, bool attackerFriendly, bool victimFriendly,
            CrewMate gunner, CrewMate[] victims)
        {
            var attacker = KillFeedPart(index, "Attacker");
            var firingWeapon = KillFeedPart(index, "Weapon");
            var victim = KillFeedPart(index, "Victim");
            string Label(string value) => value.Replace(" ", "");
            Require(KillFeedRow(index).gameObject.activeInHierarchy && Label(attacker.text).Contains(Label(gunner.name)) &&
                firingWeapon.text.Contains(weapon) && victims.Any(v => Label(victim.text).Contains(Label(v.name))),
                $"row {index + 1} shows {attacker.text} / {firingWeapon.text} / {victim.text}, with gunner and victim roles");
            Require(KillFeedColor(attacker, attackerFriendly ? KillFeedFriendly : KillFeedEnemy) &&
                KillFeedColor(victim, victimFriendly ? KillFeedFriendly : KillFeedEnemy),
                $"row {index + 1} applies the two factions' independent text colors");
            CheckKillFeedIcon(index, 2);
        }

        void CheckKillFeedIcon(int row, int column)
        {
            var icon = KillFeedRow(row).Find("WeaponIcon").GetComponent<RawImage>();
            var uv = icon.uvRect;
            Require(icon.isActiveAndEnabled && icon.texture && icon.texture.name == "KillFeedWeapons" &&
                Mathf.Abs(uv.x - column / 3f) < .0001f && Mathf.Abs(uv.width - 1f / 3f) < .0001f &&
                Mathf.Abs(uv.y) < .0001f && Mathf.Abs(uv.height - 1f) < .0001f,
                $"row {row + 1} uses weapon atlas column {column} via {uv}");
        }

        static bool KillFeedColor(TMP_Text label, Color32 target)
        {
            Color32 actual = label.color;
            return Math.Abs(actual.r - target.r) <= 2 && Math.Abs(actual.g - target.g) <= 2 &&
                Math.Abs(actual.b - target.b) <= 2;
        }

        void CheckKillFeedPosition()
        {
            var compass = KillFeedGameplay.Find("航向");
            var notice = KillFeedGameplay.Find("状态反馈");
            Require((!compass || !compass.gameObject.activeInHierarchy) && (!notice || !notice.gameObject.activeInHierarchy),
                "the compass and general top-center feedback no longer occupy the top");
            Canvas.ForceUpdateCanvases();
            var gameplay = (RectTransform)KillFeedGameplay;
            var corners = new Vector3[4];
            ((RectTransform)KillFeedRow(0)).GetWorldCorners(corners);
            var left = gameplay.InverseTransformPoint(corners[0]).x;
            var right = gameplay.InverseTransformPoint(corners[2]).x;
            Require(left > gameplay.rect.center.x && right <= gameplay.rect.xMax + 1f && right - left > 100f,
                $"the newest kill row fits inside the right half ({left:F1}–{right:F1})");
            Require(KillFeedRow(0).position.y > KillFeedRow(4).position.y,
                "row zero is above row four");
        }
    }
}
