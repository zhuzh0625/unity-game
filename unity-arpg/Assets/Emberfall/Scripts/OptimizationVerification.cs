using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emberfall
{
    // Regression fixtures are opt-in and isolated from player saves. Journey verification separately uses normal gear/input.
    public class OptimizationVerification : MonoBehaviour
    {
        static string output;
        readonly List<string> results = new List<string>();
        Adventure game;
        bool failed, finished;
        float deadline;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            var args = Environment.GetCommandLineArgs(); if (Array.IndexOf(args, "-optimization-check") < 0) return;
            int i = Array.IndexOf(args, "-optimization-output");
            output = i >= 0 && i + 1 < args.Length ? args[i + 1] : Application.persistentDataPath + "/Optimization";
            Directory.CreateDirectory(output);
            AdventureSave.VerificationPath = output + "/profile-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json";
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            UnityEngine.Random.InitState(8600);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (output != null) new GameObject("Optimization regression").AddComponent<OptimizationVerification>(); }
        IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 90; Application.logMessageReceived += Observe;
            yield return null; yield return null; game = Adventure.Instance;
            if (Keyboard.current != null) InputSystem.EnableDevice(Keyboard.current);
            Check(Resources.Load<Texture2D>("Art/WandererDirections") != null, "Four-direction animation atlas is present");
            Check(game.GetComponent<CombatFeedback>().SoundsReady, "Combat audio clips and playback source initialize");
            int floorCount = 0; foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) if (sr.name == "Ground") floorCount++;
            Check(floorCount == 1, "A zone uses one tiled floor renderer");

            game.player.position = new Vector2(-9, -5);
            var far = new Gear { name = "Worn Sword", slot = 0, power = 3 };
            var near = new Gear { name = "Tempered Coat", slot = 1, power = 5, rarity = 1 };
            game.AddDrop(new Vector2(-7, -5), far); game.AddDrop(new Vector2(-8, -5), near);
            Check(ReferenceEquals(game.ResolveInteraction().drop.gear, near) && game.Context().Contains(near.Description), "Prompt selects the same nearest loot as the action");
            yield return Tap(Key.E);
            Check(game.data.bag.Contains(near) && !game.data.bag.Contains(far), "Keyboard E picks up the displayed item");
            var hitStopFoe=game.Spawn(0,game.Position+Vector2.right);game.Damage(hitStopFoe,hitStopFoe.max);
            var immediateLoot=new Gear{name="Worn Coat",slot=1,power=2};game.AddDrop(game.Position,immediateLoot);
            yield return Tap(Key.E);
            Check(game.data.bag.Contains(immediateLoot),"E immediately after a kill is accepted during hit stop");
            game.data.bag.Clear();
            for (int i = 0; i < 16; i++) game.data.bag.Add(new Gear { name = "Tempered Sword", slot = 0, rarity = 1, power = 5, vitality = 2, affix = 1 });
            game.player.position = new Vector2(18, 0); game.AddDrop(game.Position, far);
            Check(game.ResolveInteraction().kind == Adventure.InteractionKind.Exit, "An entrance takes priority over loot with a full bag");
            yield return Tap(Key.E);
            Check(game.zone == 1 && game.Position.x < -15, "Camp to wilderness enters at its western gate");
            game.player.position = new Vector2(19, 0); yield return Tap(Key.E);
            Check(game.zone == 2 && game.Position.x < -15, "Wilderness to crypt enters at its western gate");
            game.player.position = new Vector2(-19, 0); yield return Tap(Key.E);
            Check(game.zone == 1 && game.Position.x > 15, "Crypt return enters wilderness at the eastern gate");
            game.player.position = new Vector2(-19, 0); yield return Tap(Key.E);
            Check(game.zone == 0 && game.Position.x > 14, "Wilderness return enters camp at the eastern gate");

            game.data.gold = 500; game.selection = 0; yield return Tap(Key.B); yield return new WaitForSeconds(.15f);
            var hud = game.GetComponent<AdventureHud>(); int refreshes = hud.InventoryRefreshCount;
            yield return new WaitForSeconds(.4f);
            Check(hud.InventoryRefreshCount == refreshes, "Unchanged open inventory does not rebuild its text");
            var contents = GameObject.Find("Contents").GetComponent<TMP_Text>();
            var comparison = GameObject.Find("Selected comparison").GetComponent<TMP_Text>();
            Check(contents.preferredHeight <= contents.rectTransform.rect.height + 1 && comparison.preferredHeight <= comparison.rectTransform.rect.height + 1, "Full inventory and affix comparison fit the panel");
            ScreenCapture.CaptureScreenshot(output + "/full-inventory.png"); yield return new WaitForEndOfFrame();
            if(Mouse.current!=null)
            {
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,GameObject.Find("返回 [B]").transform.position);
                InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=point}.WithButton(MouseButton.Left));yield return new WaitForSeconds(.05f);
                InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=point});yield return new WaitForSeconds(.05f);
                Check(!game.inventory,"Mouse button remains usable on the separate inventory canvas");
                if(game.inventory)yield return Tap(Key.Escape);
                yield return Tap(Key.B);
            }
            var gear = game.data.bag[0]; int cost = gear.UpgradeCost;
            yield return Tap(Key.R);
            Check(gear.upgradeLevel == 1 && gear.power == 7 && gear.vitality == 4 && game.data.gold == 500 - cost, "R consumes the displayed gold and upgrades the selected gear");
            yield return Tap(Key.R); yield return Tap(Key.R); int cappedGold = game.data.gold; yield return Tap(Key.R);
            Check(gear.upgradeLevel == 3 && game.data.gold == cappedGold, "Upgrade limit blocks further spending");
            yield return Tap(Key.Enter);
            Check(ReferenceEquals(game.data.weapon, gear) && game.data.AttackInterval < .38f, "Equipping swift gear changes attack speed");
            int count = game.data.bag.Count, gold = game.data.gold, price = game.data.bag[0].SellValue;
            yield return Tap(Key.V);
            Check(game.data.bag.Count == count - 1 && game.data.gold == gold + price, "V sells exactly the selected bag item");
            var restored = AdventureSave.Read();
            Check(restored.weapon.upgradeLevel == 3 && restored.weapon.affix == 1 && restored.gold == game.data.gold, "Save roundtrip preserves affix, upgrades and gold");
            yield return Tap(Key.Escape);
            game.EnterZone(1); yield return Tap(Key.B); count = game.data.bag.Count; gold = game.data.gold;
            yield return Tap(Key.V); yield return Tap(Key.R);
            Check(game.data.bag.Count == count && game.data.gold == gold, "Selling and upgrading are rejected outside camp");
            yield return Tap(Key.Escape); yield return Tap(Key.F1);
            var enemyPosition = game.enemies[0].go.transform.position; var pose = game.player.Find("Body").localPosition;
            yield return new WaitForSeconds(.3f);
            Check(game.enemies[0].go.transform.position == enemyPosition && game.player.Find("Body").localPosition == pose, "Help pauses enemy movement and character animation");
            ScreenCapture.CaptureScreenshot(output + "/help.png"); yield return new WaitForEndOfFrame(); yield return Tap(Key.Escape);

            game.EnterZone(2); ClearEnemies(); game.player.position = new Vector2(-5, 6); game.health = 10000;
            var pursuer = game.Spawn(0, new Vector2(5, 6));
            float until = Time.realtimeSinceStartup + 11;
            while (Time.realtimeSinceStartup < until && Vector2.Distance(game.Position, pursuer.go.transform.position) > 1.6f) yield return null;
            Check(Vector2.Distance(game.Position, pursuer.go.transform.position) < 1.6f, "Enemy navigates through the opening around the crypt divider");
            game.player.position = new Vector2(-1.5f, 6); pursuer.go.transform.position = new Vector2(1.5f, 6); pursuer.hp = 1000; pursuer.attack = 1000; pursuer.slow = 1000;
            game.mana = 100; yield return Tap(Key.U); yield return new WaitForSeconds(.3f);
            Check(pursuer.hp == 1000, "Cleave cannot damage through the divider");
            game.mana = 100; yield return Tap(Key.I); yield return new WaitForSeconds(.3f);
            Check(pursuer.hp == 1000, "Frost cannot damage through the divider");
            game.facing = Vector2.right; game.mana = 100; yield return Tap(Key.O); yield return new WaitForSeconds(.5f);
            Check(pursuer.hp == 1000 && game.CurrentTarget == null, "Wall blocks both auto-targeting and the projectile");
            game.player.position = new Vector2(-2, 0); pursuer.go.transform.position = new Vector2(-.5f, 0); game.cooldown[0] = 0;
            float before = pursuer.hp; yield return Tap(Key.J);
            Check(pursuer.hp == before, "Sword does not apply damage before the hit frame");
            yield return new WaitForSeconds(.2f);
            Check(pursuer.hp < before, "Sword applies damage at its hit frame with a clear path");
            game.cooldown[0]=0;game.cooldown[1]=0;game.mana=100;game.UseAttack(0);yield return Tap(Key.U);yield return new WaitForSeconds(.3f);
            Check(game.cooldown[1]>0,"A skill pressed during a sword windup is buffered and cast");
            game.player.position = new Vector2(-2, 6); pursuer.go.transform.position = new Vector2(-10, 0); game.facing = Vector2.right; game.health = 100;
            yield return Tap(Key.K); game.Hurt(50);
            Check(game.health == 100 && game.IsDodging, "K dodge grants its short invulnerability window");
            yield return new WaitForSeconds(.3f);
            Check(game.Position.x <= -1.35f && game.DodgeRemaining > 0, "Dodge stops at walls and starts its cooldown");

            game.EnterZone(2); ClearEnemies(); game.player.position = Vector2.zero;
            var boss = game.Spawn(3, new Vector2(4, 0)); game.Damage(boss, boss.max * .51f);
            until=Time.realtimeSinceStartup+2;
            while(boss.windup<=0&&Time.realtimeSinceStartup<until)yield return null;
            Check(boss.enraged && boss.windup > 0 && boss.windup < .73f, "Half-health boss enters the faster second phase");
            ScreenCapture.CaptureScreenshot(output + "/boss-phase-two.png"); yield return new WaitForEndOfFrame();
            game.EnterZone(0); game.health = game.data.MaxHealth; game.player.position = new Vector2(-1, 0);
            game.data.bag[0]=new Gear{name="Tempered Sword",slot=0,rarity=1,power=5,affix=3};game.selection=0;
            yield return Tap(Key.B);yield return Tap(Key.Enter);yield return Tap(Key.Escape);
            game.cooldown[2]=0;game.mana=100;yield return Tap(Key.I);
            Check(game.cooldown[2]>0&&game.cooldown[2]<5.6f,"Equipped focus affix shortens a real skill cooldown");
            yield return new WaitForSeconds(.3f);game.mana=10;yield return new WaitForSeconds(.4f);
            Check(game.mana>13.8f,"Equipped focus affix increases resource regeneration");
            game.data.bag[0]=new Gear{name="Tempered Sword",slot=0,rarity=1,power=5,affix=2};game.selection=0;
            yield return Tap(Key.B);yield return Tap(Key.Enter);yield return Tap(Key.Escape);
            var leechTarget=game.Spawn(0,new Vector2(10,0));game.health=50;game.Damage(leechTarget,10);
            Check(Mathf.Abs(game.health-50.4f)<.01f,"Equipped leech affix heals from actual damage");
            ClearEnemies();game.health=game.data.MaxHealth;yield return new WaitForSeconds(.1f);
            var directionKeys=new[]{Key.S,Key.D,Key.W,Key.A};
            for(int row=0;row<4;row++)
            {
                yield return Tap(directionKeys[row]);yield return new WaitForSeconds(.08f);
                Check(game.player.Find("Body").GetComponent<SpriteRenderer>().sprite==WorldArt.HeroFrame(row,0),"Movement selects directional idle frame "+row);
                if(row==2){ScreenCapture.CaptureScreenshot(output+"/hero-north.png");yield return new WaitForEndOfFrame();}
            }
            game.cooldown[0]=0;game.UseAttack(0);yield return new WaitForSeconds(.13f);
            Check(game.player.Find("Body").GetComponent<SpriteRenderer>().sprite==WorldArt.HeroFrame(3,3),"Sword animation displays its directional attack frame");
            ScreenCapture.CaptureScreenshot(output+"/hero-attack.png");yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(.2f);
            ScreenCapture.CaptureScreenshot(output + "/camp.png"); yield return new WaitForEndOfFrame();
            VerifyLegacySave();
            Finish();
        }
        void VerifyLegacySave()
        {
            string path = output + "/legacy.json";
            File.WriteAllText(path, "{\"version\":1,\"potions\":4,\"gold\":17,\"weapon\":{\"name\":\"Camp Sword\",\"slot\":0,\"power\":2},\"armor\":{\"name\":\"Travel Coat\",\"slot\":1,\"power\":1},\"bag\":[]}");
            var legacy = AdventureSave.Read(path);
            Check(legacy.gold == 17 && legacy.weapon.affix == 0 && legacy.weapon.upgradeLevel == 0, "Original version-1 saves load with neutral new fields");
            File.Copy(path, path + ".bak", true);
            File.WriteAllText(path, "{\"version\":1,\"weapon\":{\"name\":\"Camp Sword\",\"slot\":0},\"armor\":{\"name\":\"Travel Coat\",\"slot\":1},\"bag\":[null]}");
            Check(AdventureSave.Read(path).gold == 17, "Invalid gear entries recover the valid backup");
        }
        void ClearEnemies() { foreach (var f in game.enemies) { if (f.telegraph) game.GetComponent<CombatFeedback>().Release(f.telegraph); Destroy(f.go); } game.enemies.Clear(); }
        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key)); yield return new WaitForSeconds(.04f);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); yield return null;
        }
        void Check(bool condition, string text) { results.Add((condition ? "PASS " : "FAIL ") + text); if (!condition) failed = true; Debug.Log(results[results.Count - 1]); }
        void Observe(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) { failed = true; results.Add("ERROR " + text); } }
        void Update() { if (!finished && deadline > 0 && Time.realtimeSinceStartup > deadline) { Check(false, "Regression timeout"); Finish(); } }
        void Finish()
        {
            if (finished) return; finished = true; Application.logMessageReceived -= Observe;
            File.WriteAllLines(output + "/regression-results.txt", results); GracefulExit.Request(failed ? 1 : 0);
        }
    }
}
