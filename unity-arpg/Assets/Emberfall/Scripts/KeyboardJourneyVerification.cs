using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emberfall
{
    // Opt-in acceptance route: ordinary keyboard state only, no teleporting, direct damage or fixture gear.
    public class KeyboardJourneyVerification : MonoBehaviour
    {
        static string output;
        Adventure game;
        readonly List<string> checkpoints=new List<string>();
        float deadline;
        bool returning, failed;
        int keyboardEvents;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-keyboard-journey")<0)return;
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-journey-output");
            output=index>=0?args[index+1]:Application.persistentDataPath+"/Journey";
            Directory.CreateDirectory(output);
            AdventureSave.VerificationPath=output+"/profile-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".json";
            UnityEngine.Random.InitState(8600);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-keyboard-journey")>=0)new GameObject("Keyboard journey").AddComponent<KeyboardJourneyVerification>();
        }
        IEnumerator Start()
        {
            yield return null;game=Adventure.Instance;deadline=Time.realtimeSinceStartup+180;
            if(Keyboard.current!=null)InputSystem.EnableDevice(Keyboard.current);
            Application.logMessageReceived+=Observe;
            InputSystem.onEvent+=OnInput;
            Record("Started with normal camp sword, 100 HP and 5 potions");
            yield return Walk(new Vector2(-4,0));yield return Tap(Key.E);Record("Rested at campfire");
            yield return Walk(new Vector2(18,0));yield return Tap(Key.E);
            if(game.zone!=1){Fail("Camp gateway not reached");yield break;}Record("Entered wilderness by walking and E");
            while(!failed && !Expired)
            {
                if(game.dead){Fail("Died during keyboard-only journey");yield break;}
                if(game.health<game.data.MaxHealth-48 && game.data.potions>0)yield return Tap(Key.H);
                if(!returning)
                {
                    Adventure.Foe foe=null;float distance=11;
                    foreach(var f in game.enemies){float d=Vector2.Distance(game.Position,f.go.transform.position);if(d<distance){foe=f;distance=d;}}
                    if(foe!=null)
                    {
                        var keys=new List<Key>{Key.J};
                        if(distance>1.5f)MoveKeys((Vector2)foe.go.transform.position-game.Position,keys);
                        if(game.cooldown[3]==0&&game.mana>=18)keys.Add(Key.O);
                        if(distance<3&&game.cooldown[1]==0&&game.mana>=22)keys.Add(Key.U);
                        if(distance<4&&game.cooldown[2]==0&&game.mana>=28)keys.Add(Key.I);
                        yield return Keys(keys.ToArray(),.08f);continue;
                    }
                    Adventure.Drop drop=null;float nearest=12;
                    foreach(var d in game.drops){float dist=Vector2.Distance(game.Position,d.go.transform.position);if(dist<nearest){nearest=dist;drop=d;}}
                    if(drop!=null&&game.data.bag.Count<16)
                    {
                        yield return Walk(drop.go.transform.position);int count=game.data.bag.Count;yield return Tap(Key.E);
                        if(game.data.bag.Count>count){Record("Picked up normal random loot");yield return EquipBest();}
                        continue;
                    }
                    if(game.zone==2&&game.data.bossDefeated)
                    {
                        returning=true;Record("Warden defeated, loot collected; returning on foot");
                        ScreenCapture.CaptureScreenshot(output+"/boss-cleared.png");yield return new WaitForEndOfFrame();continue;
                    }
                    // Always cross the fixed dungeon's center opening instead of walking into the divider.
                    if(game.zone==2&&game.Position.x<2&&Mathf.Abs(game.Position.y)>.2f){yield return Walk(new Vector2(game.Position.x,0),.15f);continue;}
                    if(game.Position.x<17.5f){yield return Step(new Vector2(18,0));continue;}
                    if(game.zone==1){yield return Tap(Key.E);if(game.zone==2)Record("Entered crypt by walking and E");}
                    else yield return Step(new Vector2(14,0));
                }
                else
                {
                    if(game.zone==2&&game.Position.x>1&&Mathf.Abs(game.Position.y)>.2f){yield return Walk(new Vector2(game.Position.x,0),.15f);continue;}
                    if(Vector2.Distance(game.Position,new Vector2(-19,0))>1.1f){yield return Step(new Vector2(-19,0));continue;}
                    int before=game.zone;yield return Tap(Key.E);
                    if(game.zone!=before)Record("Used western exit; zone "+game.zone);
                    if(game.zone==0)
                    {
                        if(!game.data.returned){Fail("Contract did not complete");yield break;}
                        Record("Contract complete after walking back to camp");
                        yield return Tap(Key.F5);yield return Tap(Key.Escape);yield return Tap(Key.Enter);
                        ScreenCapture.CaptureScreenshot(output+"/returned-camp.png");yield return new WaitForEndOfFrame();
                        Finish();yield break;
                    }
                }
            }
            Fail("Journey exceeded 180 seconds or encountered runtime error");
        }
        bool Expired=>Time.realtimeSinceStartup>deadline;
        IEnumerator EquipBest()
        {
            int index=game.data.bag.FindIndex(g=>g.power>(g.slot==0?game.data.weapon.power:game.data.armor.power));
            if(index<0)yield break;
            yield return Tap(Key.B);
            int guard=0;while(game.selection!=index&&guard++<17)yield return Tap(game.selection<index?Key.S:Key.W);
            yield return Tap(Key.Enter);Record("Equipped random gear through B / arrows / Enter");yield return Tap(Key.Escape);
        }
        IEnumerator Walk(Vector2 target,float stopDistance=.55f)
        {
            while(!Expired&&!game.dead&&Vector2.Distance(game.Position,target)>stopDistance)yield return Step(target);
        }
        IEnumerator Step(Vector2 target)
        {
            Vector2 delta=target-game.Position;bool precise=delta.magnitude<1;
            var keys=new List<Key>();if(!precise)keys.Add(Key.LeftShift);
            MoveKeys(delta,keys);yield return Keys(keys.ToArray(),precise?.016f:.08f);
        }
        static void MoveKeys(Vector2 delta,List<Key> keys)
        {
            if(delta.x>.05f)keys.Add(Key.D);if(delta.x<-.05f)keys.Add(Key.A);
            if(delta.y>.05f)keys.Add(Key.W);if(delta.y<-.05f)keys.Add(Key.S);
        }
        IEnumerator Tap(Key key){yield return Keys(new[]{key},.04f);}
        IEnumerator Keys(Key[] keys,float duration)
        {
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));yield return new WaitForSeconds(duration);
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;
        }
        void OnInput(UnityEngine.InputSystem.LowLevel.InputEventPtr ptr,InputDevice device){if(device is Keyboard)keyboardEvents++;}
        void Observe(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){failed=true;Record("ERROR "+message);}}
        void Record(string text){checkpoints.Add($"{Time.realtimeSinceStartup:0.00}s {text}");Debug.Log("JOURNEY "+text);}
        void Fail(string text){failed=true;Record("FAIL "+text);Finish();}
        void Finish()
        {
            Record((failed?"FAILED":"PASSED")+$"; {keyboardEvents} keyboard events; kills={game.kills}; HP={game.health:0}; potions={game.data.potions}");
            File.WriteAllLines(output+"/keyboard-journey.txt",checkpoints);
            InputSystem.onEvent-=OnInput;Application.logMessageReceived-=Observe;
            GracefulExit.Request(failed?1:0);
        }
    }
}
