using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emberfall
{
    // Opt-in integration verification. Never runs during ordinary play.
    public class PrototypeVerification : MonoBehaviour
    {
        readonly List<string> results=new List<string>();
        string output;
        bool failed;
        Adventure game;
        static readonly List<string> earlyErrors=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ObserveStartup()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-emberfall-smoke")<0)return;
            earlyErrors.Clear();Application.logMessageReceived+=CaptureStartup;
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-smoke-output");
            string directory=arg>=0&&arg+1<args.Length?args[arg+1]:Application.persistentDataPath+"/Verification";
            AdventureSave.VerificationPath=directory+"/isolated-profile.json";
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        }
        static void CaptureStartup(string message,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception)earlyErrors.Add("STARTUP ERROR "+message);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-emberfall-smoke")>=0)
                new GameObject("Verification").AddComponent<PrototypeVerification>();
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-smoke-output");
            output=arg>=0&&arg+1<args.Length?args[arg+1]:Application.persistentDataPath+"/Verification";
            Directory.CreateDirectory(output);
            AdventureSave.VerificationPath=output+"/isolated-profile.json";
            Application.logMessageReceived-=CaptureStartup;
            results.AddRange(earlyErrors);failed=earlyErrors.Count>0;
            Application.logMessageReceived+=Log;
            yield return null;yield return null;
            game=Adventure.Instance;
            if(Keyboard.current!=null)InputSystem.EnableDevice(Keyboard.current);
            Check(Resources.Load<Texture2D>("Art/EmberfallAtlas")!=null,"Original character and scenery atlas loads");
            Check(Chinese.ItemName("Worn Sword")=="磨损长剑"&&game.data.version==1,"Legacy English item identifiers display in Chinese without a save migration");
            yield return null;
            int missingGlyphs=0;var allLabels=FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(var label in allLabels)
            {
                if(string.IsNullOrEmpty(label.text))continue;
                if(!label.font){missingGlyphs++;continue;}
                foreach(char c in label.text)if(!char.IsControl(c)&&!label.font.HasCharacter(c))missingGlyphs++;
            }
            Check(allLabels.Length>5&&missingGlyphs==0,"Chinese HUD labels have complete glyph coverage");
            // Isolated test save path, preserving an ordinary player's progression.
            game.data=new AdventureData();game.health=game.data.MaxHealth;
            game.EnterZone(0);
            // Let first-frame texture uploads and font materials settle before timing movement.
            for(int warmup=0;warmup<20;warmup++)yield return null;
            yield return new WaitForSeconds(.2f);
            Check(Camera.main.rect==new Rect(0,0,1,1),"World camera clears the full target without viewport residue");
            int tentCount=0;bool roofsVisible=true;
            foreach(var sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))if(sr.gameObject.name=="Camp tent"){tentCount++;roofsVisible&=Camera.main.WorldToViewportPoint(sr.bounds.max).y<1-AdventureHud.HeaderHeight/900f;}
            Check(tentCount==2&&roofsVisible,"Both camp tent roofs stay within the playable camera viewport");
            int frame=Time.frameCount;Vector2 start=game.Position;
            yield return Hold(Key.D,.35f);
            Check(Time.frameCount>frame+3,"Player loop advances");
            Check(game.Position.x>start.x+1,"Keyboard D moves the player");
            yield return Tap(Key.B);Check(game.inventory,"B opens keyboard inventory");
            start=game.Position;yield return Hold(Key.D,.2f);Check(Vector2.Distance(start,game.Position)<.01f,"Inventory pauses world movement");
            yield return Tap(Key.Escape);Check(!game.inventory,"Escape closes inventory");
            yield return Tap(Key.F1);Check(game.help,"F1 opens the keyboard help panel");
            start=game.Position;yield return Hold(Key.D,.2f);Check(Vector2.Distance(start,game.Position)<.01f,"Keyboard help pauses world movement");
            ScreenCapture.CaptureScreenshot(output+"/help.png");yield return new WaitForEndOfFrame();
            yield return Tap(Key.Escape);Check(!game.help,"Escape closes keyboard help");
            game.player.position=new Vector2(18,0);yield return Tap(Key.E);Check(game.zone==1,"Keyboard E leaves camp");
            if(game.zone!=1){Finish();yield break;}
            // First playable stage: regular attack, enemy death, actual dropped gear, keyboard pickup.
            var foe=game.enemies[0];game.player.position=(Vector2)foe.go.transform.position-Vector2.right;
            int guard=0;while(game.enemies.Contains(foe)&&guard++<10)yield return Hold(Key.J,.42f);
            Check(!game.enemies.Contains(foe),"Keyboard sword attack defeats first enemy");
            // Guaranteed fixture verifies pickup independently of random drop probability.
            var reward=new Gear{name="Verification Sword",slot=0,power=12,vitality=5,rarity=2};
            game.AddDrop(game.Position,reward);int count=game.data.bag.Count;
            yield return Tap(Key.E);Check(game.data.bag.Count==count+1,"E picks up equipment");
            game.data.bag.Clear();game.data.bag.Add(reward);
            yield return Tap(Key.B);yield return Tap(Key.Enter);
            Check(game.data.weapon.name==reward.name&&game.data.Attack==24,"Keyboard inventory equips and changes damage");
            Check(GameObject.Find("Inventory")!=null,"Keyboard inventory panel is visible");
            var comparison=GameObject.Find("Selected comparison").GetComponent<TMPro.TMP_Text>();
            Check(comparison.text.Contains("攻击")&&comparison.text.Contains("生命")&&comparison.text.Contains("对比已装备"),"Inventory comparison explicitly labels primary and health attributes");
            ScreenCapture.CaptureScreenshot(output+"/inventory.png");yield return new WaitForEndOfFrame();
            var savedBag=game.data.bag;game.data.bag=new List<Gear>();
            for(int slot=0;slot<16;slot++)game.data.bag.Add(new Gear{name="Emberbound Sword",slot=0,power=14,vitality=9,rarity=2});
            game.GetComponent<AdventureHud>().Refresh();yield return null;
            var contents=GameObject.Find("Contents").GetComponent<TMPro.TMP_Text>();contents.ForceMeshUpdate();
            Check(contents.preferredHeight<=contents.rectTransform.rect.height+1,"Full 16-slot Chinese inventory fits its panel");
            ScreenCapture.CaptureScreenshot(output+"/full-inventory.png");yield return new WaitForEndOfFrame();game.data.bag=savedBag;
            yield return Tap(Key.Escape);
            game.health=30;int potions=game.data.potions;yield return Tap(Key.H);
            Check(game.health>30&&game.data.potions==potions-1,"H consumes a healing potion");
            game.mana=100;yield return Tap(Key.U);Check(game.cooldown[1]>0&&game.mana<90,"U cleave spends focus and starts cooldown");
            yield return new WaitForSeconds(.25f);
            yield return Tap(Key.I);Check(game.cooldown[2]>0,"I frost starts cooldown");
            yield return new WaitForSeconds(.25f);
            yield return Tap(Key.O);Check(game.cooldown[3]>0,"O ember projectile starts cooldown");
            yield return Tap(Key.Escape);float timer=game.cooldown[1];yield return new WaitForSeconds(.2f);
            Check(game.paused&&Mathf.Approximately(timer,game.cooldown[1]),"Escape pauses combat timers");
            ScreenCapture.CaptureScreenshot(output+"/pause.png");yield return new WaitForEndOfFrame();yield return Tap(Key.Enter);
            game.player.position=new Vector2(19,0);yield return Tap(Key.E);Check(game.zone==2,"E enters fixed dungeon");
            game.player.position=new Vector2(-2,6);start=game.Position;yield return Hold(Key.D,.3f);
            Check(game.Position.x<-.99f,"Dungeon wall blocks keyboard movement");
            var boss=game.enemies.Find(f=>f.kind==3);Check(boss!=null,"Dungeon spawns Warden boss");
            // Combat is exercised with live ticks; teleporting here only shortens traversal.
            if(boss!=null)
            {
                game.health=game.data.MaxHealth;game.player.position=(Vector2)boss.go.transform.position-Vector2.right*1.2f;
                guard=0;
                while(game.enemies.Contains(boss)&&!game.dead&&guard++<60)
                {
                    yield return Hold(Key.J,.4f);
                    if(game.health<65&&game.data.potions>0)yield return Tap(Key.H);
                    if(game.enemies.Contains(boss))game.player.position=(Vector2)boss.go.transform.position-Vector2.right*1.2f;
                }
            }
            Check(game.data.bossDefeated,"Live keyboard combat defeats boss");
            Check(game.drops.Count>0,"Boss produces equipment loot");
            if(game.drops.Count>0){game.player.position=game.drops[game.drops.Count-1].go.transform.position;yield return Tap(Key.E);}
            ScreenCapture.CaptureScreenshot(output+"/crypt.png");yield return new WaitForEndOfFrame();
            game.player.position=new Vector2(-19,0);yield return Tap(Key.E);Check(game.zone==1,"Crypt west gate returns to wilderness");
            game.player.position=new Vector2(-19,0);yield return Tap(Key.E);Check(game.zone==0&&game.data.returned,"Return to camp completes contract");
            game.player.position=new Vector2(-4,0);yield return Tap(Key.E);Check(game.data.potions==5&&game.health==game.data.MaxHealth,"Campfire restores health and potions");
            var path=output+"/roundtrip.json";AdventureSave.Write(game.data,path);var restored=AdventureSave.Read(path);
            Check(restored.weapon.name==reward.name&&restored.bag.Count==game.data.bag.Count&&restored.victories==1,"Local save roundtrip preserves gear and progress");
            AdventureSave.Write(game.data,path);File.WriteAllText(path,"broken json");restored=AdventureSave.Read(path);
            Check(restored.weapon.name==reward.name,"Corrupt primary save recovers backup");
            ScreenCapture.CaptureScreenshot(output+"/camp.png");yield return new WaitForEndOfFrame();
            Finish();
        }
        IEnumerator Tap(Key key){yield return Hold(key,.04f);yield return null;}
        IEnumerator Hold(Key key,float seconds)
        {
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            yield return null;
        }
        void Check(bool condition,string name){results.Add((condition?"PASS ":"FAIL ")+name);if(!condition)failed=true;Debug.Log(results[results.Count-1]);}
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){failed=true;results.Add("RUNTIME ERROR "+message);}}
        void Finish()
        {
            Application.logMessageReceived-=Log;
            File.WriteAllLines(output+"/smoke-results.txt",results);Debug.Log(failed?"EMBERFALL_SMOKE_FAILED":"EMBERFALL_SMOKE_PASSED");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            GracefulExit.Request(failed?1:0);
#endif
        }
    }
}
