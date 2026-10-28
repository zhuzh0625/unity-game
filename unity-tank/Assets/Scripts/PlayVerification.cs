#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;

namespace IronBastion
{
    // Editor-only integration fixtures. These controls are excluded from the shipped player.
    public sealed class PlayVerification : MonoBehaviour
    {
        [Serializable] public class Receipt {public bool passed;public List<string> checks=new();public string error;public int frames;}
        Receipt receipt=new();
        int savedBest;
        InputSettings originalInputSettings, verificationInputSettings;
        HideFlags originalInputFlags;
        Keyboard originalKeyboard, verificationKeyboard;
        BattleGame G=>BattleGame.Instance;
        public static void Run() {new GameObject("Play verification").AddComponent<PlayVerification>().StartCoroutine("RunSafe");}
        IEnumerator RunSafe()
        {
            savedBest=PlayerPrefs.GetInt("IronBastion.Best",0);
            // The Editor normally routes background keyboard events away from the game.
            // Use a temporary settings clone so unattended tests exercise the player input loop.
            originalInputSettings=InputSystem.settings;
            originalInputFlags=originalInputSettings.hideFlags;
            // InputManager destroys HideAndDontSave settings when a replacement is assigned.
            // Keep the original object alive until this fixture restores it.
            if(originalInputFlags==HideFlags.HideAndDontSave)originalInputSettings.hideFlags=HideFlags.HideInHierarchy;
            verificationInputSettings=Instantiate(originalInputSettings);
            verificationInputSettings.hideFlags=HideFlags.HideAndDontSave;
            verificationInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            verificationInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=verificationInputSettings;
            originalKeyboard=Keyboard.current;
            verificationKeyboard=InputSystem.AddDevice<Keyboard>("VerificationKeyboard");
            verificationKeyboard.MakeCurrent();
            var test=Verify();
            while(true)
            {
                object current=null;bool next=false;
                try {next=test.MoveNext();if(next)current=test.Current;}
                catch(Exception e){receipt.error=e.ToString();break;}
                if(!next){receipt.passed=true;break;}
                yield return current;
            }
            if(verificationKeyboard!=null)InputState.Change(verificationKeyboard,new KeyboardState());
            receipt.frames=Time.frameCount;Directory.CreateDirectory("tmp");File.WriteAllText("tmp/play-verification.json",JsonUtility.ToJson(receipt,true));
            if(!receipt.passed) Debug.LogError(receipt.error);
            PlayerPrefs.SetInt("IronBastion.Best",savedBest);PlayerPrefs.Save();Set(G,"<Best>k__BackingField",savedBest);
            G.ReturnToMenu();Destroy(gameObject);
        }
        void OnDestroy()
        {
            if(verificationKeyboard!=null&&verificationKeyboard.added)InputSystem.RemoveDevice(verificationKeyboard);
            if(originalKeyboard!=null&&originalKeyboard.added)originalKeyboard.MakeCurrent();
            if(originalInputSettings && InputSystem.settings==verificationInputSettings)InputSystem.settings=originalInputSettings;
            if(originalInputSettings)originalInputSettings.hideFlags=originalInputFlags;
            if(verificationInputSettings)Destroy(verificationInputSettings);
        }
        void Check(bool condition,string name){if(!condition)throw new Exception(name);receipt.checks.Add(name);}
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        void Quiet(){Set(G,"nextSpawn",Time.time+1000);foreach(var e in G.Enemies)if(e){e.gameObject.SetActive(false);Destroy(e.gameObject);}G.Enemies.Clear();}
        void ClearShells(){foreach(var s in FindObjectsByType<BattleShell>(FindObjectsSortMode.None)){s.gameObject.SetActive(false);Destroy(s.gameObject);}}
        void ClickVisible(string name)
        {
            Canvas.ForceUpdateCanvases();
            var button=G.Hud.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name==name);
            var rect=button.GetComponent<RectTransform>();
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Visible UI raycast reaches "+name);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        BattleShell IncomingShell(TankActor owner)
        {
            var shot=new GameObject("Respawn protection test shell");shot.transform.SetParent(G.DynamicRoot);
            shot.transform.position=G.Player.transform.position+Vector3.up*.78f+Vector3.forward*1.2f;
            var shell=shot.AddComponent<BattleShell>();shell.Configure(owner,Vector3.back,1,13);owner.ActiveShells++;return shell;
        }
        IEnumerator Verify()
        {
            foreach(var map in WorldBuilder.Maps)Check(map.Length==13&&map.All(r=>r.Length==13)&&map.Sum(r=>r.Count(c=>c=='H'))==1,"13x13 map with exactly one base");
            G.ReturnToMenu();yield return null;yield return null;
            ClickVisible("SecondaryAction");yield return null;
            Check(G.Hud.LibraryOpen&&G.State==BattleState.Menu&&GameObject.Find("PickupEntries"),"Main-menu library button opens the pickup page without starting a game");
            ClickVisible("EnemiesTab");yield return null;
            Check(GameObject.Find("EnemyEntries")&&!GameObject.Find("PickupEntries"),"Enemy tab shows enemy descriptions exclusively");
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState(Key.Enter));yield return null;yield return null;
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState());yield return null;
            Check(G.State==BattleState.Menu&&G.Hud.LibraryOpen,"Enter inside library cannot accidentally start the campaign");
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState(Key.Tab));yield return null;yield return null;
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState());yield return null;
            Check(GameObject.Find("PickupEntries"),"Tab switches back to pickups");
            ClickVisible("LibraryBack");yield return null;
            Check(!G.Hud.LibraryOpen&&GameObject.Find("SecondaryAction"),"Library back button restores main menu");
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState(Key.I));yield return null;yield return null;
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState());yield return null;
            Check(G.Hud.LibraryOpen,"I opens the library from the main menu");
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState(Key.Escape));yield return null;yield return null;
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState());yield return null;
            Check(!G.Hud.LibraryOpen&&G.State==BattleState.Menu,"Escape closes library without starting or pausing a game");
            ClickVisible("PrimaryAction");Quiet();yield return null;
            Check(G.State==BattleState.Playing&&!G.Hud.LibraryOpen,"Start button still enters gameplay after returning from library");

            G.Player.Hit(true);yield return new WaitForSeconds(1.6f);
            Check(G.Player&&G.Lives==2&&G.Player.ShieldUntil-Time.time>3.1f,"First respawn receives the full 3.5-second protection window");
            Check(G.Notice.Contains("无敌保护")&&G.Player.transform.Find("Shield ring").gameObject.activeSelf,"Respawn displays protection notice and visible shield ring");
            var shooter=G.CreateTank(false,new Vector3(12,0,12),0,false);shooter.enabled=false;
            var blocked=IncomingShell(shooter);yield return new WaitForSeconds(.18f);
            Check(!blocked&&G.Player&&G.Lives==2&&G.Player.Health==1,"Real enemy projectile is consumed without harming the respawned player");
            float protectionLeft=G.Player.ShieldUntil-Time.time;G.TogglePause();yield return new WaitForSecondsRealtime(.18f);
            Check(Mathf.Abs((G.Player.ShieldUntil-Time.time)-protectionLeft)<.02f,"Pause preserves remaining invulnerability time");G.TogglePause();
            yield return new WaitForSeconds(Mathf.Max(0,G.Player.ShieldUntil-Time.time)+.08f);
            Check(!G.Player.IsProtected&&!G.Player.transform.Find("Shield ring").gameObject.activeSelf,"Protection expires naturally and its visual ring disappears");
            IncomingShell(shooter);yield return new WaitForSeconds(.18f);
            Check(!G.Player&&G.Lives==1,"Enemy projectile damages the player again after protection expires");
            yield return new WaitForSeconds(1.5f);
            Check(G.Player&&G.Player.IsProtected&&G.Player.ShieldUntil-Time.time>3,"Second consecutive respawn also receives fresh protection");
            IncomingShell(shooter);yield return new WaitForSeconds(.18f);
            Check(G.Player&&G.Lives==1,"Protection also blocks enemy fire after the second respawn");
            G.StartCampaign();Quiet();yield return null;
            Check(G.State==BattleState.Playing&&G.Lives==3&&G.Player,"Campaign starts with player and three lives");
            Vector3 before=G.Player.transform.position;
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState(Key.W));
            yield return new WaitForSeconds(.3f);
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState());yield return null;
            Check(G.Player.transform.position.z>before.z+.7f,"Keyboard W moves the actual player: "+before+" -> "+G.Player.transform.position);
            G.TogglePause();Vector3 paused=G.Player.transform.position;
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState(Key.W));yield return new WaitForSecondsRealtime(.15f);
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState());
            Check(G.State==BattleState.Paused&&Time.timeScale==0&&G.Player.transform.position==paused,"Pause freezes simulation and movement");G.TogglePause();yield return null;
            // Adjacent shot through the actual shell sweep must break a wall without skipping it.
            G.Player.transform.position=new Vector3(-4,0,-10);
            G.Player.Move(Vector3.right,0);Set(G.Player,"nextShot",0f);
            int bricks=FindObjectsByType<BattleBlock>(FindObjectsSortMode.None).Count(b=>b.Kind==SurfaceKind.Brick);
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState(Key.Space));yield return new WaitForSeconds(.20f);
            InputSystem.QueueStateEvent(verificationKeyboard,new KeyboardState());yield return null;
            Check(FindObjectsByType<BattleBlock>(FindObjectsSortMode.None).Count(b=>b.Kind==SurfaceKind.Brick)<bricks,"Space fires a swept shell that destroys an adjacent brick");ClearShells();yield return null;
            var steel=FindObjectsByType<BattleBlock>(FindObjectsSortMode.None).First(b=>b.Kind==SurfaceKind.Steel);
            Check(!steel.Hit(1)&&steel.GetComponent<Collider>().enabled,"Steel blocks normal fire");
            Check(steel.Hit(2)&&!steel.GetComponent<Collider>().enabled,"Armor-piercing fire destroys steel");yield return null;
            var water=GameObject.Find("Water");Check(water&&!G.Player.CanOccupy(water.transform.position),"Water blocks tank movement");
            var wall=FindObjectsByType<BattleBlock>(FindObjectsSortMode.None).First(b=>b.Kind==SurfaceKind.Brick);
            Check(!G.Player.CanOccupy(wall.transform.position),"Brick wall blocks tank movement");
            G.Player.ShieldUntil=Time.time+5;G.Player.Hit();Check(G.Player&&G.Lives==3,"Shield prevents tank death");
            G.ApplyPickup(PickupKind.Star);G.ApplyPickup(PickupKind.Star);G.ApplyPickup(PickupKind.Star);Check(G.Player.Upgrade==2,"Weapon upgrade caps at armor-piercing level");
            G.ApplyPickup(PickupKind.Life);Check(G.Lives==4,"Extra-life supply increments lives");
            G.ApplyPickup(PickupKind.Shield);Check(G.Player.ShieldUntil>Time.time+9,"Shield supply lasts ten seconds");
            G.ApplyPickup(PickupKind.Freeze);Check(G.FrozenUntil>Time.time+7,"Freeze supply lasts eight seconds");
            G.Player.Hit(true);yield return new WaitForSeconds(1.6f);
            Check(G.Player&&G.Lives==3&&G.Player.Upgrade==0&&G.Player.ShieldUntil>Time.time,"Death consumes one life, resets upgrades, and respawns protected");
            var target=G.CreateTank(false,new Vector3(-4,0,-6),3,false);G.Enemies.Add(target);
            target.Hit(true);Check(target.Health==2,"Armored enemy survives first hit");
            G.ApplyPickup(PickupKind.Bomb);yield return null;
            Check(!target&&G.Enemies.Count==0,"Bomb clears armored enemies as well as basic tanks");
            // Actual AI must move from its spawn and progress toward headquarters.
            G.StartCampaign();Quiet();yield return null;
            var enemy=G.CreateTank(false,new Vector3(0,0,12),0,false);G.Enemies.Add(enemy);
            float initial=enemy.transform.position.z;yield return new WaitForSeconds(2.3f);
            Check(enemy&&enemy.transform.position.z<initial-1,"Enemy AI advances from deployment lane");
            G.ApplyPickup(PickupKind.Freeze);Vector3 frozen=enemy.transform.position;yield return new WaitForSeconds(.25f);
            Check(enemy&&Vector3.Distance(enemy.transform.position,frozen)<.01f,"Freeze stops actual AI movement");
            // Resolve each stage through production kill accounting, with controlled enemy fixtures.
            G.StartCampaign();Quiet();yield return null;
            for(int stage=1;stage<=3;stage++)
            {
                Check(G.Stage==stage,"Correct campaign stage "+stage);
                Set(G,"<Spawned>k__BackingField",G.Total);
                for(int i=0;i<G.Total;i++) {var e=G.CreateTank(false,new Vector3(-12,0,12),0,false);G.Enemies.Add(e);e.Hit(true);}
                yield return null;yield return null;
                if(stage<3){Check(G.State==BattleState.StageClear,"Stage "+stage+" clear after all enemies destroyed");G.NextStage();Quiet();yield return null;}
                else Check(G.State==BattleState.Victory,"Third stage completes campaign");
            }
            G.StartCampaign();Quiet();yield return null;
            var hq=FindObjectsByType<BattleBlock>(FindObjectsSortMode.None).First(b=>b.Kind==SurfaceKind.Base);hq.Hit(1);
            Check(G.State==BattleState.GameOver&&G.ResultReason.Contains("指挥部"),"Base destruction triggers immediate defeat");
            G.StartCampaign();Quiet();yield return null;
            for(int i=0;i<3;i++){G.Player.Hit(true);if(i<2)yield return new WaitForSeconds(1.6f);}
            Check(G.State==BattleState.GameOver&&G.Lives==0,"Exhausting all lives triggers defeat");
            G.StartCampaign();Quiet();yield return null;
            Check(G.State==BattleState.Playing&&G.Stage==1&&G.Score==0&&G.Lives==3,"Restart resets the full campaign");
        }
    }
}
#endif
