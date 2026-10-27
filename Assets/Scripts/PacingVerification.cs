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
    public sealed class PacingVerification : MonoBehaviour
    {
        [Serializable] public class Receipt {public bool passed;public string error="";public List<string> checks=new();public List<float> firstSpawnTimes=new();}
        readonly Receipt receipt=new();
        BattleGame G=>BattleGame.Instance;
        int savedBest;
        public static void Run()=>new GameObject("Spawn pacing verification").AddComponent<PacingVerification>().StartCoroutine("RunSafe");
        static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        void Check(bool ok,string name){if(!ok)throw new Exception(name);receipt.checks.Add(name);}
        void StopEnemies()
        {
            // Park controlled fixtures clear of all three deployment points so the count cap,
            // rather than occupied spawn cells, determines whether another enemy can appear.
            float[] parking={-12,-8,4,12};int index=0;
            foreach(var e in G.Enemies)if(e){e.enabled=false;e.transform.position=new Vector3(parking[index++],0,12);}
        }
        IEnumerator RunSafe()
        {
            savedBest=PlayerPrefs.GetInt("IronBastion.Best",0);var run=Verify();
            while(true)
            {
                bool next=false;object current=null;
                try{next=run.MoveNext();if(next)current=run.Current;}catch(Exception ex){receipt.error=ex.ToString();break;}
                if(!next){receipt.passed=true;break;}yield return current;
            }
            Directory.CreateDirectory("tmp");File.WriteAllText("tmp/pacing-verification.json",JsonUtility.ToJson(receipt,true));
            G.ReturnToMenu();PlayerPrefs.SetInt("IronBastion.Best",savedBest);PlayerPrefs.Save();Set(G,"<Best>k__BackingField",savedBest);
            if(!receipt.passed)Debug.LogError(receipt.error);Destroy(gameObject);
        }
        IEnumerator Verify()
        {
            G.StartCampaign();float started=Time.time;G.Player.enabled=false;G.Player.ShieldUntil=Time.time+100;
            while(Time.time-started<4.6f)yield return null;
            Check(G.Spawned==0,"First stage gives at least 4.6 seconds without enemies");
            for(int count=1;count<=3;count++)
            {
                float deadline=Time.time+6;
                while(G.Spawned<count&&Time.time<deadline)yield return null;
                StopEnemies();receipt.firstSpawnTimes.Add(Time.time-started);
                Check(G.Spawned==count,"Scheduled first-stage wave arrives: "+count);
                var e=G.Enemies.Last();Check(e.Type==0&&e.Health==1,"Opening wave is a one-hit ordinary tank: "+count);
                if(count==1)Check(e.Carrier&&e.FixedPickup==PickupKind.Star,"First ordinary enemy carries a guaranteed star");
            }
            Check(receipt.firstSpawnTimes[0]>=4.99f&&receipt.firstSpawnTimes[0]<5.4f,"Actual first spawn occurs at five seconds");
            Check(Mathf.Abs(receipt.firstSpawnTimes[1]-receipt.firstSpawnTimes[0]-4.5f)<.15f&&Mathf.Abs(receipt.firstSpawnTimes[2]-receipt.firstSpawnTimes[1]-4.5f)<.15f,"Actual spawn intervals are 4.5 seconds");
            Set(G,"nextSpawn",Time.time-1);yield return null;yield return null;
            Check(G.Spawned==3&&G.Enemies.Count==3,"Due spawn remains blocked at first-stage three-enemy cap");
            G.Enemies[0].Hit(true);yield return null;yield return null;StopEnemies();
            var pickup=FindObjectsByType<BattlePickup>(FindObjectsSortMode.None).Single();
            Check(pickup.Kind==PickupKind.Star,"Destroying first ordinary enemy drops an actual star pickup");
            Check(G.Spawned==4&&G.Enemies.Last().Type==0,"Fourth opening enemy remains ordinary after a slot frees");
            G.Player.transform.position=pickup.transform.position;yield return null;yield return null;
            Check(G.Player.Upgrade==1&&!pickup,"Picking up the dropped star upgrades the real player");
            for(int ordinal=5;ordinal<=12;ordinal++)
            {
                var oldest=G.Enemies[0];while(oldest.Health>0)oldest.Hit(true);Set(G,"nextSpawn",Time.time-1);yield return null;yield return null;StopEnemies();
                var enemy=G.Enemies.Last();
                Check(G.Spawned==ordinal&&enemy.Type==(ordinal-4)%4,"Later first-stage enemy type matches progression: "+ordinal);
                Check(enemy.Carrier==(ordinal==8||ordinal==12)&&enemy.FixedPickup==null,"Later supply remains random, with markers at eight and twelve: "+ordinal);
            }
            foreach(var enemy in G.Enemies.ToArray())while(enemy.Health>0)enemy.Hit(true);yield return null;yield return null;
            Check(G.State==BattleState.StageClear&&G.Kills==12,"First stage still clears after all twelve enemies are defeated");
            for(int stage=2;stage<=3;stage++)
            {
                G.NextStage();started=Time.time;G.Player.enabled=false;G.Player.ShieldUntil=Time.time+100;
                Check(G.Stage==stage&&G.Total==12+(stage-1)*4&&G.EnemyLimit==4,"Later-stage enemy count and cap unchanged: "+stage);
                while(G.Spawned<1&&Time.time-started<4)yield return null;StopEnemies();
                Check(G.Spawned==1&&Time.time-started>=2.49f&&Time.time-started<2.9f&&G.Enemies[0].Type==stage-1,"Later stage retains 2.5-second opening and original first type: "+stage);
                float first=Time.time;
                while(G.Spawned<2&&Time.time-first<4)yield return null;StopEnemies();
                Check(G.Spawned==2&&Mathf.Abs(Time.time-first-(3.4f-stage*.4f))<.15f,"Later stage retains original spawn interval: "+stage);
                for(int count=3;count<=4;count++){Set(G,"nextSpawn",Time.time-1);yield return null;yield return null;StopEnemies();}
                Check(G.Enemies.Count==4&&G.Enemies[3].Carrier&&G.Enemies[3].FixedPickup==null,"Later stage still spawns four enemies with random fourth supply: "+stage);
                Set(G,"nextSpawn",Time.time-1);yield return null;yield return null;
                Check(G.Spawned==4,"Later-stage four-enemy cap still blocks more spawns: "+stage);
                // Finish the remainder through production kill accounting to reach the next stage.
                Set(G,"<Spawned>k__BackingField",G.Total);Set(G,"<Kills>k__BackingField",G.Total-4);
                foreach(var enemy in G.Enemies.ToArray())while(enemy.Health>0)enemy.Hit(true);
                yield return null;yield return null;
            }
            G.StartCampaign();Check(G.ResultReason=="","New campaign clears prior outcome text");
        }
    }
}
#endif
