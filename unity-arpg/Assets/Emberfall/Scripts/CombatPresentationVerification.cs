using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emberfall
{
    // Explicit visual fixtures; normal play and normal save files never run this component.
    public class CombatPresentationVerification:MonoBehaviour
    {
        static string output;
        readonly List<string> results=new List<string>();
        Adventure game;CombatFeedback feedback;
        bool failed,finished;float deadline;
        Vector2 cameraCenter=new Vector2(14,0);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"-combat-presentation-check")<0)return;
            int i=Array.IndexOf(args,"-presentation-output");output=i>=0&&i+1<args.Length?args[i+1]:Application.persistentDataPath+"/CombatPresentation";
            Directory.CreateDirectory(output);AdventureSave.VerificationPath=output+"/profile-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".json";
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(output!=null)new GameObject("Combat presentation verification").AddComponent<CombatPresentationVerification>();}
        IEnumerator Start()
        {
            deadline=Time.realtimeSinceStartup+80;Application.logMessageReceived+=Observe;
            yield return null;yield return null;game=Adventure.Instance;feedback=game.GetComponent<CombatFeedback>();
            Check(Resources.Load<Texture2D>("Art/EnemyAttacks")!=null,"Enemy attack sheet is included in the player build");
            for(int kind=0;kind<4;kind++)
            {
                Setup();game.player.position=new Vector2(kind==0?14:17,0);
                yield return new WaitForSeconds(.1f);
                var foe=game.Spawn(kind,new Vector2(kind==0?12.7f:12,0));foe.attack=0;
                if(kind==3)foe.hp=foe.max*.49f;
                float health=game.health;
                while(foe.windup<=0)yield return null;
                yield return new WaitForEndOfFrame();
                Check(foe.visual.AttackFrame==1&&Active()==0,"Enemy "+kind+" anticipates before launching or dealing damage");
                yield return Shot("enemy-"+kind+"-windup");
                if(kind==1)
                {
                    game.paused=true;float remaining=foe.windup;
                    var sprite=foe.go.transform.Find("Body").GetComponent<SpriteRenderer>().sprite;
                    yield return new WaitForSeconds(.12f);
                    Check(foe.windup==remaining&&foe.go.transform.Find("Body").GetComponent<SpriteRenderer>().sprite==sprite,"Pause freezes both the attack pose and its windup");
                    game.paused=false;
                }
                while(foe.windup>0)yield return null;
                Check(foe.visual.AttackFrame==2&&foe.recovery>0,"Enemy "+kind+" displays the release pose on the actual combat event");
                foe.attack=20;
                yield return Shot("enemy-"+kind+"-release");
                if(kind==0||kind==3)Check(game.health<health,"Enemy "+kind+" damage occurs with its release pose");
                if(kind>0)
                {
                    ProjectileKind expected=kind==1?ProjectileKind.Arrow:kind==2?ProjectileKind.Hex:ProjectileKind.Warden;
                    var missile=First(expected);
                    Check(missile!=null&&missile.Body.sprite!=WorldArt.Disc,"Enemy "+kind+" emits its distinct projectile graphic");
                    if(kind==3)Check(Active()==6,"Enraged slam retains its six radial projectiles");
                    yield return new WaitForSeconds(.07f);
                    if(kind==1&&missile)
                    {
                        Check(Vector2.Dot(missile.Heading,Vector2.right)>.98f,"Arrow head follows the actual flight direction");
                        game.paused=true;float age=missile.Age;Vector3 point=missile.transform.position;
                        yield return new WaitForSeconds(.12f);
                        Check(age==missile.Age&&point==missile.transform.position,"Pause freezes the projectile and its animated trail");game.paused=false;
                        yield return new WaitForSeconds(.06f);
                    }
                    yield return Shot("enemy-"+kind+"-projectile");
                }
                while(foe.visual.AttackFrame==2)yield return null;
                Check(foe.visual.AttackFrame==3,"Enemy "+kind+" has a visible follow-through pose");
                yield return Shot("enemy-"+kind+"-recovery");
                while(foe.visual.AttackFrame!=0)yield return null;
                Check(foe.go.transform.Find("Body").GetComponent<SpriteRenderer>().sprite==WorldArt.EnemyFrame(kind,0),"Enemy "+kind+" returns to its ready pose");
                if(kind==1||kind==2)
                {
                    yield return new WaitForSeconds(1.2f);
                    Check(game.health<health,"Enemy "+kind+" projectile still reaches and damages its target");
                }
            }
            Setup();game.player.position=new Vector2(12,0);game.facing=Vector2.right;
            var victim=game.Spawn(0,new Vector2(18,0));victim.attack=20;float hp=victim.hp;
            game.UseAttack(3);while(First(ProjectileKind.Ember)==null)yield return null;
            yield return new WaitForSeconds(.12f);yield return Shot("player-ember");
            Check(First(ProjectileKind.Ember)!=null,"Player O uses a flame core and animated ember trail");
            yield return new WaitForSeconds(.6f);
            Check(victim.hp<hp&&Active()==0,"Player flame deals damage and returns its visual to the pool");

            Setup();cameraCenter=new Vector2(0,5);game.player.position=new Vector2(-3,5);game.facing=Vector2.right;
            var hidden=game.Spawn(1,new Vector2(3,5));hidden.attack=20;hp=hidden.hp;game.UseAttack(3);
            yield return new WaitForSeconds(.5f);
            Check(hidden.hp==hp&&Active()==0,"New projectile graphics retain the swept wall collision");

            Setup();var cancelled=game.Spawn(2,new Vector2(12,0));game.player.position=new Vector2(17,0);cancelled.attack=0;
            while(cancelled.windup<=0)yield return null;
            game.Damage(cancelled,cancelled.max);yield return new WaitForSeconds(.65f);
            Check(Active()==0,"Killing a caster during windup cancels its shot and charge");
            int capacity=feedback.Projectiles.Count;var pooled=feedback.Projectiles[0];game.EnterZone(0);yield return null;
            Check(Active()==0&&feedback.Projectiles.Count==capacity&&feedback.Projectiles[0]==pooled,"Zone changes clear active projectile effects and preserve the reusable pool");
            Finish();
        }
        void Setup()
        {
            game.paused=false;game.EnterZone(2);
            foreach(var foe in game.enemies)Destroy(foe.go);game.enemies.Clear();
            game.health=game.data.MaxHealth;game.mana=100;for(int i=0;i<4;i++)game.cooldown[i]=0;
            cameraCenter=new Vector2(14,0);
        }
        int Active(){int count=0;foreach(var value in feedback.Projectiles)if(value.gameObject.activeSelf)count++;return count;}
        ProjectileVisual First(ProjectileKind kind){foreach(var value in feedback.Projectiles)if(value.gameObject.activeSelf&&value.Kind==kind)return value;return null;}
        IEnumerator Shot(string label){yield return null;ScreenCapture.CaptureScreenshot(output+"/"+label+".png");yield return new WaitForEndOfFrame();}
        void LateUpdate(){if(game&&Camera.main){Camera.main.orthographicSize=4.5f;Camera.main.transform.position=new Vector3(cameraCenter.x,cameraCenter.y,-10);}}
        void Check(bool ok,string description){results.Add((ok?"PASS ":"FAIL ")+description);if(!ok)failed=true;Debug.Log(results[results.Count-1]);}
        void Observe(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){failed=true;results.Add("ERROR "+message);}}
        void Update(){if(!finished&&deadline>0&&Time.realtimeSinceStartup>deadline){Check(false,"Presentation verification timed out");Finish();}}
        void Finish(){if(finished)return;finished=true;Application.logMessageReceived-=Observe;File.WriteAllLines(output+"/presentation-results.txt",results);GracefulExit.Request(failed?1:0);}
    }
}
