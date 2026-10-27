#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace IronBastion
{
    // Exercise the production AI in an empty test arena, including the imported Blender barrel.
    public sealed class FacingVerification : MonoBehaviour
    {
        [Serializable] public class Receipt { public bool passed; public List<string> checks=new(); public string error=""; }
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        readonly Receipt receipt=new();
        BattleGame Game=>BattleGame.Instance;
        public static void Run()=>new GameObject("Facing regression verification").AddComponent<FacingVerification>().StartCoroutine("RunSafe");
        static void Set(object obj,string member,object value)=>obj.GetType().GetField(member,Private).SetValue(obj,value);
        static Vector3 Heading(BattleShell shell)=>(Vector3)typeof(BattleShell).GetField("direction",Private).GetValue(shell);
        static void Tick(TankActor tank)=>typeof(TankActor).GetMethod("Update",Private).Invoke(tank,null);
        static Vector3 Barrel(TankActor tank)
        {
            var muzzle=tank.Visual.GetComponentsInChildren<Transform>().First(t=>t.name.Contains("Muzzle"));
            Vector3 vector=muzzle.position-tank.transform.position; vector.y=0; return vector.normalized;
        }
        void Check(bool condition,string message)
        { if(!condition)throw new Exception(message); receipt.checks.Add(message); }
        IEnumerator RunSafe()
        {
            var iterator=Verify();
            while(true)
            {
                bool next=false;object current=null;
                try { next=iterator.MoveNext();if(next)current=iterator.Current; }
                catch(Exception ex) { receipt.error=ex.ToString();break; }
                if(!next) {receipt.passed=true;break;}
                yield return current;
            }
            File.WriteAllText("tmp/facing-verification.json",JsonUtility.ToJson(receipt,true));
            Game.ReturnToMenu();
            if(!receipt.passed)Debug.LogError(receipt.error);
            Destroy(gameObject);
        }
        IEnumerator Verify()
        {
            Game.StartCampaign();Set(Game,"nextSpawn",Time.time+1000f);
            Game.Player.enabled=false;Game.Player.ShieldUntil=Time.time+100;
            foreach(var b in FindObjectsByType<BattleBlock>(FindObjectsSortMode.None))
                if(b.Kind!=SurfaceKind.Base)b.gameObject.SetActive(false);
            foreach(var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if(c.gameObject.layer==WorldBuilder.WaterLayer)c.enabled=false;
            yield return null;
            foreach(var aim in new[]{Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
            {
                string label=aim.ToString();
                Game.Player.transform.position=aim*4;
                var enemy=Game.CreateTank(false,Vector3.zero,0,false);enemy.enabled=false;
                Vector3 oldDirection=-aim;
                enemy.Move(oldDirection,0);enemy.Visual.rotation=Quaternion.LookRotation(oldDirection);
                Set(enemy,"nextShot",0f);Set(enemy,"hasWaypoint",true);
                Set(enemy,"waypoint",new Vector3(0,0,-4));Set(enemy,"nextPlan",Time.time+100f);
                enemy.Move(aim,0);
                Check(!enemy.Fire()&&enemy.ActiveShells==0,"No shot or ammo use before a 180-degree turn: "+label);
                Physics.SyncTransforms();
                BattleShell fired=null;
                for(int i=0;i<120;i++)
                {
                    Tick(enemy);
                    fired=FindObjectsByType<BattleShell>(FindObjectsSortMode.None).FirstOrDefault(s=>s.Owner==enemy);
                    if(fired)break;
                    yield return null;
                }
                Check(fired,"AI fires after rotating toward target: "+label);
                Check(Vector3.Angle(Barrel(enemy),Heading(fired))<.1f,"Imported muzzle agrees with shell heading at fire time: "+label);
                Check(Vector3.Angle(enemy.Facing,aim)<.1f&&enemy.transform.position.sqrMagnitude<.001f,"Navigation cannot overwrite aim or move the firing tank: "+label);
                var origin=fired.transform.position;
                yield return new WaitForSeconds(.05f);
                Check(fired&&Vector3.Dot(fired.transform.position-origin,aim)>0&&Vector3.Angle(fired.transform.position-origin,aim)<.1f,"Actual shell travel follows the barrel: "+label);
                Tick(enemy);
                Check(Vector3.Angle(Barrel(enemy),aim)<.1f,"AI holds target direction while reloading: "+label);
                Destroy(fired.gameObject);Destroy(enemy.gameObject);yield return null;
            }
            // A blocked movement direction still rotates the hull, then fires through the same path.
            Game.Player.transform.position=new Vector3(-10,0,-10);
            var blocked=Game.CreateTank(false,Vector3.zero,0,false);blocked.enabled=false;
            var obstacle=WorldBuilder.Cube("Test wall",Game.DynamicRoot,new Vector3(1.3f,.65f,0),new Vector3(.5f,1.3f,2),WorldBuilder.Material("Test steel",Color.gray),WorldBuilder.SolidLayer,true);
            Set(blocked,"nextShot",0f);Set(blocked,"hasWaypoint",true);Set(blocked,"waypoint",new Vector3(4,0,0));Set(blocked,"nextPlan",Time.time+100f);
            BattleShell wallShot=null;
            for(int i=0;i<180;i++)
            {
                Tick(blocked);
                wallShot=FindObjectsByType<BattleShell>(FindObjectsSortMode.None).FirstOrDefault(s=>s.Owner==blocked);
                if(wallShot)break;
                yield return null;
            }
            Check(wallShot&&Vector3.Angle(Barrel(blocked),Heading(wallShot))<.1f,"Obstacle-clearing shot matches the barrel after a 90-degree turn");
            Destroy(obstacle);Destroy(blocked.gameObject);if(wallShot)Destroy(wallShot.gameObject);
        }
    }
}
#endif
