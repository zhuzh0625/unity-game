#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace IronBastion
{
    // Observation and keyboard-only strategies. No edits to combat, spawns, maps or player stats.
    [DefaultExecutionOrder(-100)]
    public sealed class BalancePlaytest : MonoBehaviour
    {
        [Serializable] public class Sample
        {
            public string strategy,outcome,reason;
            public int seed,kills,spawned,lives,peakEnemies,observedEnemyShells,observedPlayerShells,observedSupplies;
            public float seconds,firstEnemy,firstKill,firstDeath,firstEnemyInSouth,firstSupply,minimumEnemyBaseDistance,maxFrameDelta;
            public List<string> events=new();
            public List<string> snapshots=new();
        }
        [Serializable] public class Report { public string version,method,error="";public bool completed;public List<Sample> samples=new(); }
        public Report Results=new();
        public Sample Current;
        public string Progress=>Current==null?"idle":Current.strategy+" seed="+Current.seed+" t="+(Time.time-started).ToString("F1")+" kills="+G.Kills+" spawned="+G.Spawned+" lives="+G.Lives;
        BattleGame G=>BattleGame.Instance;
        Keyboard originalKeyboard,keyboard;
        InputSettings originalSettings,settings;
        HideFlags originalSettingsFlags;
        UnityEngine.Random.State originalRandom;
        int originalBest;
        float started,nextDecision,nextSnapshot;
        readonly HashSet<int> shellIds=new(),pickupIds=new();
        int priorKills,priorLives;
        Vector3 waypoint;
        bool hasWaypoint;
        TankActor lastPlayer;
        Key direction;
        string strategyFilter;
        float tapRelease;
        static readonly Vector2Int[] Steps={new(0,1),new(-1,0),new(1,0),new(0,-1)};

        public static void Run(string strategy=null)
        {
            if(FindFirstObjectByType<BalancePlaytest>())throw new InvalidOperationException("Playtest already running");
            var runner=new GameObject("First-stage balance playtest").AddComponent<BalancePlaytest>();
            runner.strategyFilter=strategy;runner.StartCoroutine("RunSafe");
        }
        public static void RunGuard()=>Run("central-guard");
        public static void RunAll()=>Run("all");
        IEnumerator RunSafe()
        {
            originalBest=PlayerPrefs.GetInt("IronBastion.Best",0);
            originalRandom=UnityEngine.Random.state;
            originalSettings=InputSystem.settings;originalSettingsFlags=originalSettings.hideFlags;
            if(originalSettingsFlags==HideFlags.HideAndDontSave)originalSettings.hideFlags=HideFlags.HideInHierarchy;
            settings=Instantiate(originalSettings);settings.hideFlags=HideFlags.HideAndDontSave;
            settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=settings;originalKeyboard=Keyboard.current;
            keyboard=InputSystem.AddDevice<Keyboard>("BalancePlaytestKeyboard");keyboard.MakeCurrent();
            Results.version=Application.version;
            Results.method="First stage, original rules, timeScale 1; virtual keyboard; deterministic seeds 101/202/303; 180-second limit per trial. Scripted strategies, not human win-rate estimates.";
            var run=Trials();
            while(true)
            {
                bool next=false;object current=null;
                try{next=run.MoveNext();if(next)current=run.Current;}
                catch(Exception e){Results.error=e.ToString();break;}
                if(!next){Results.completed=true;break;}
                yield return current;
            }
            Save();Current=null;G.ReturnToMenu();Destroy(gameObject);
        }
        IEnumerator Trials()
        {
            foreach(string strategy in strategyFilter=="all"?new[]{"idle","spawn-sentry","intercept","central-guard"}:strategyFilter==null?new[]{"idle","spawn-sentry","intercept"}:new[]{strategyFilter})
            foreach(int seed in new[]{101,202,303})
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                UnityEngine.Random.InitState(seed);G.StartCampaign();yield return null;
                Current=new Sample{strategy=strategy,seed=seed,firstEnemy=-1,firstKill=-1,firstDeath=-1,firstEnemyInSouth=-1,firstSupply=-1,minimumEnemyBaseDistance=100};
                Results.samples.Add(Current);started=Time.time;nextDecision=0;nextSnapshot=0;
                priorKills=0;priorLives=3;shellIds.Clear();pickupIds.Clear();hasWaypoint=false;lastPlayer=null;direction=Key.None;tapRelease=0;
                while(G.State==BattleState.Playing&&Time.time-started<180)yield return null;
                Current.seconds=Time.time-started;Current.outcome=G.State==BattleState.Playing?"Timeout":G.State.ToString();
                Current.reason=G.ResultReason;Current.kills=G.Kills;Current.spawned=G.Spawned;Current.lives=G.Lives;
                Current.events.Add("END "+Current.outcome+" at "+Current.seconds.ToString("F1")+"s; kills="+G.Kills+" lives="+G.Lives);
                Save();Current=null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            }
        }
        void Save(){Directory.CreateDirectory("tmp");File.WriteAllText("tmp/first-stage-balance.json",JsonUtility.ToJson(Results,true));}
        void Update()
        {
            if(Current==null||G.State!=BattleState.Playing)return;
            if(tapRelease>0&&Time.time>=tapRelease){tapRelease=0;Send(Key.None,true);}
            if(Time.time>=nextDecision)
            {
                nextDecision=Time.time+.2f;
                if(Current.strategy=="idle"||!G.Player)Send(Key.None,false);
                else if(Current.strategy=="spawn-sentry")Send(Key.None,true);
                else if(Current.strategy=="central-guard")CentralGuard();
                else Intercept();
            }
        }
        void LateUpdate()
        {
            if(Current==null)return;
            float t=Time.time-started;Current.maxFrameDelta=Mathf.Max(Current.maxFrameDelta,Time.deltaTime);
            Current.peakEnemies=Mathf.Max(Current.peakEnemies,G.Enemies.Count);
            if(Current.firstEnemy<0&&G.Spawned>0)Current.firstEnemy=t;
            if(G.Kills>priorKills){if(Current.firstKill<0)Current.firstKill=t;Current.events.Add(t.ToString("F1")+"s kills "+G.Kills);priorKills=G.Kills;}
            if(G.Lives<priorLives){if(Current.firstDeath<0)Current.firstDeath=t;Current.events.Add(t.ToString("F1")+"s life lost; remaining "+G.Lives);}
            if(G.Lives>priorLives)Current.events.Add(t.ToString("F1")+"s extra life");priorLives=G.Lives;
            foreach(var e in G.Enemies)if(e)
            {
                Current.minimumEnemyBaseDistance=Mathf.Min(Current.minimumEnemyBaseDistance,Vector3.Distance(e.transform.position,WorldBuilder.BasePosition));
                if(Current.firstEnemyInSouth<0&&e.transform.position.z<=-6)Current.firstEnemyInSouth=t;
            }
            foreach(var s in FindObjectsByType<BattleShell>(FindObjectsSortMode.None))if(shellIds.Add(s.GetInstanceID()))
            {if(s.Friendly)Current.observedPlayerShells++;else Current.observedEnemyShells++;}
            foreach(var p in FindObjectsByType<BattlePickup>(FindObjectsSortMode.None))if(pickupIds.Add(p.GetInstanceID()))
            {Current.observedSupplies++;if(Current.firstSupply<0)Current.firstSupply=t;Current.events.Add(t.ToString("F1")+"s supply "+p.Kind);}
            if(t>=nextSnapshot)
            {
                nextSnapshot=t+10;
                Current.snapshots.Add(t.ToString("F1")+"s player="+(G.Player?G.Player.transform.position.ToString("F1"):"respawning")+" enemies="+string.Join(";",G.Enemies.Where(e=>e).Select(e=>e.Type+"@"+e.transform.position.ToString("F1"))));
                Save();
            }
        }
        void Send(Key key,bool fire)
        {
            direction=key;
            var held=new List<Key>();if(key!=Key.None)held.Add(key);if(fire)held.Add(Key.Space);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(held.ToArray()));
        }
        static Vector3 Vector(Key key)=>key==Key.W?Vector3.forward:key==Key.S?Vector3.back:key==Key.A?Vector3.left:key==Key.D?Vector3.right:Vector3.zero;
        static Key Heading(Vector3 delta)=>Mathf.Abs(delta.x)>Mathf.Abs(delta.z)?(delta.x>0?Key.D:Key.A):(delta.z>0?Key.W:Key.S);
        bool ClearShot(Vector3 origin,Vector3 target)
        {
            var delta=target-origin;
            return delta.magnitude<10&&(Mathf.Abs(delta.x)<.4f||Mathf.Abs(delta.z)<.4f)&&!Physics.Raycast(origin+Vector3.up*.78f,Vector(Heading(delta)),delta.magnitude,1<<WorldBuilder.SolidLayer);
        }
        void Intercept()
        {
            var player=G.Player;var pos=player.transform.position;
            if(player!=lastPlayer){hasWaypoint=false;lastPlayer=player;}
            var target=G.Enemies.Where(e=>e).OrderBy(e=>Vector3.Distance(e.transform.position,WorldBuilder.BasePosition)).FirstOrDefault();
            if(target&&ClearShot(pos,target.transform.position))
            {
                Key aim=Heading(target.transform.position-pos);
                if(player.Facing==Vector(aim)){Send(Key.None,true);return;}
                Send(aim,false);hasWaypoint=false;return;
            }
            // Plan toward a southern interception lane. Controls are held for a 200 ms reaction interval.
            var goal=target?new Vector2Int(WorldBuilder.Grid(target.transform.position).x,9):new Vector2Int(4,9);
            if(!hasWaypoint||Vector3.Distance(pos,waypoint)<.45f)
            {waypoint=NextCell(player,goal);hasWaypoint=true;}
            var delta=waypoint-pos;
            if(delta.magnitude<.4f){Send(Key.None,true);hasWaypoint=false;return;}
            Key move=Heading(delta);
            // When crossing a corner, finish the old axis before taking the next grid step.
            if(Mathf.Abs(delta.x)>.35f&&Mathf.Abs(delta.z)>.35f&&direction!=Key.None)
            {if(direction==Key.A||direction==Key.D)move=delta.x>0?Key.D:Key.A;else move=delta.z>0?Key.W:Key.S;}
            bool fire=!(move==Key.S&&Mathf.Abs(pos.x)<1.5f&&pos.z>-12);
            Send(move,fire);
        }
        void CentralGuard()
        {
            var player=G.Player;var pos=player.transform.position;
            // Deliberately chosen route: up the left spawn corridor, then right to the HQ approach.
            // This is a map-informed guard strategy, with the same 200 ms decision interval.
            if(player!=lastPlayer){lastPlayer=player;hasWaypoint=false;}
            if(!hasWaypoint)
            {
                if(pos.z<-8.25f){Send(Key.W,true);return;}
                if(Mathf.Abs(pos.x)>.25f){Send(pos.x<0?Key.D:Key.A,true);return;}
                hasWaypoint=true;
            }
            var target=G.Enemies.Where(e=>e&&ClearShot(pos,e.transform.position)).OrderBy(e=>Vector3.Distance(e.transform.position,WorldBuilder.BasePosition)).FirstOrDefault();
            Key aim=target?Heading(target.transform.position-pos):Key.W;
            if(player.Facing!=Vector(aim))
            {Send(aim,true);tapRelease=Time.time+.025f;}
            else Send(Key.None,true);
        }
        static Vector3 NextCell(TankActor player,Vector2Int goal)
        {
            Physics.SyncTransforms();Vector2Int start=WorldBuilder.Grid(player.transform.position);
            Vector3 center=WorldBuilder.Cell(start.x,start.y);
            if(Vector3.Distance(center,player.transform.position)>.45f&&player.CanOccupy(center))return center;
            var cost=new float[169];var prev=new int[169];var done=new bool[169];var weight=new float[169];
            for(int i=0;i<169;i++){cost[i]=float.PositiveInfinity;prev[i]=-1;weight[i]=1;}
            for(int r=0;r<13;r++)for(int c=0;c<13;c++)foreach(var hit in Physics.OverlapBox(WorldBuilder.Cell(c,r)+Vector3.up*.6f,new Vector3(.7f,.5f,.7f),Quaternion.identity,WorldBuilder.MovementMask))
            {
                if(hit.GetComponent<TankActor>()==player)continue;
                var block=hit.GetComponent<BattleBlock>();
                if(block&&block.Kind==SurfaceKind.Brick)weight[r*13+c]=Mathf.Max(weight[r*13+c],3);
                else if(hit.GetComponent<TankActor>())weight[r*13+c]=Mathf.Max(weight[r*13+c],4);
                else weight[r*13+c]=9999;
            }
            int from=start.y*13+start.x,to=goal.y*13+goal.x;cost[from]=0;
            for(int k=0;k<169;k++)
            {
                int current=-1;float best=float.PositiveInfinity;
                for(int i=0;i<169;i++)if(!done[i]&&cost[i]<best){best=cost[i];current=i;}
                if(current<0||current==to)break;done[current]=true;
                foreach(var step in Steps)
                {
                    int x=current%13+step.x,y=current/13+step.y;if(x<0||x>12||y<0||y>12)continue;
                    int n=y*13+x;if(weight[n]>=9999)continue;
                    float value=cost[current]+weight[n];if(value<cost[n]){cost[n]=value;prev[n]=current;}
                }
            }
            if(from==to||prev[to]<0)return center;int at=to;
            while(prev[at]>=0&&prev[at]!=from)at=prev[at];return WorldBuilder.Cell(at%13,at/13);
        }
        void OnDestroy()
        {
            if(keyboard!=null&&keyboard.added){InputState.Change(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);}
            if(originalKeyboard!=null&&originalKeyboard.added)originalKeyboard.MakeCurrent();
            if(originalSettings&&InputSystem.settings==settings)InputSystem.settings=originalSettings;
            if(originalSettings)originalSettings.hideFlags=originalSettingsFlags;
            if(settings)Destroy(settings);
            UnityEngine.Random.state=originalRandom;PlayerPrefs.SetInt("IronBastion.Best",originalBest);PlayerPrefs.Save();
            if(G)typeof(BattleGame).GetField("<Best>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(G,originalBest);
        }
    }
}
#endif
