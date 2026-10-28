#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace CloudSprout
{
    public sealed class RouteVerification:MonoBehaviour
    {
        public string Status="Starting";
        public int Step;
        public bool Failed;
        GameSession game;SproutPlayer p;
        List<string> log=new List<string>();
        public void Run(){StartCoroutine(Route());}
        void Record(string s){log.Add(s);Directory.CreateDirectory("Verification");File.WriteAllText("Verification/route.txt",string.Join("\n",log));}
        void Abort(string reason){Failed=true;Status=reason;p.autoMove=0;p.autoHold=false;Record("FAIL "+reason+" at "+p.transform.position);}
        IEnumerator Travel(float x,float y,bool jump)
        {
            if(Failed)yield break;
            Status=(jump?"Jump to ":"Walk to ")+x;Step++;
            if(jump){p.autoJump=true;p.autoHold=true;}
            float start=Time.time;
            while(Time.time-start<5)
            {
                float delta=x-p.transform.position.x;p.autoMove=Mathf.Clamp(delta*6/7.6f,-1,1);
                // A head bump can cancel the first jump. Retry from a grounded position,
                // exactly as a player would; never move or override the controller.
                if(jump && p.Grounded && Time.time-start>.65f && y>p.transform.position.y+.5f && Mathf.Abs(delta)<3)p.autoJump=true;
                if(game.Deaths>0){Abort("Died during "+Status);yield break;}
                if(game.State==GameState.Won)yield break;
                if(Time.time-start>.24f && Mathf.Abs(delta)<.22f && p.Grounded && Mathf.Abs(p.transform.position.y-y)<.45f)
                {p.autoMove=0;p.autoHold=false;Record("PASS "+Status+" at "+p.transform.position+" health="+game.Health);yield return new WaitForSeconds(.08f);yield break;}
                yield return null;
            }
            Abort("Timeout during "+Status);
        }
        IEnumerator Ride(float initialX,float launchX,float exitX,float exitY)
        {
            if(Failed)yield break;
            var platform=FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None).OrderBy(m=>Mathf.Abs(m.transform.position.x-initialX)).First();
            Status="Wait for platform "+initialX;float deadline=Time.time+10;
            while(platform.transform.position.x>initialX+1 && Time.time<deadline)yield return null;
            p.autoJump=true;p.autoHold=true;float start=Time.time;
            while(Time.time-start<2)
            {
                p.autoMove=Mathf.Clamp((platform.transform.position.x-p.transform.position.x)*6/7.6f,-1,1);
                if(Time.time-start>.2f && p.Grounded && Mathf.Abs(p.transform.position.y-platform.transform.position.y)<.3f)break;
                yield return null;
            }
            if(!p.Grounded||Mathf.Abs(p.transform.position.y-platform.transform.position.y)>.3f){Abort("Could not board platform "+initialX);yield break;}
            p.autoMove=0;p.autoHold=false;Record("PASS Board platform "+initialX);
            while(platform.transform.position.x<launchX && Time.time<deadline)yield return null;
            // Move to the outer edge before the long exit jump.
            float edge=platform.transform.position.x+1;
            yield return Travel(edge,platform.transform.position.y,false);
            yield return Travel(exitX,exitY,true);
        }
        IEnumerator Route()
        {
            game=GameSession.Instance;p=game.player;game.Begin();p.automation=true;p.autoRun=true;
            Record("Continuous traversal: real controller inputs; no position warps, immunity or altered physics.");
            yield return new WaitForSeconds(.4f);
            yield return Travel(12,0,false);yield return Travel(15,1.5f,true);yield return Travel(20,0,true);
            yield return Travel(21,0,false);yield return Travel(26,0,true);yield return Travel(31,0,true);yield return Travel(33,0,false);
            yield return Travel(38,1.6f,true);yield return Travel(42,3.1f,true);yield return Travel(46.2f,0,true);yield return Travel(52,.6f,true);
            yield return Travel(55,.6f,false);yield return Travel(57,2.1f,true);yield return Travel(62,3.6f,true);yield return Travel(67,.6f,true);yield return Travel(68,.6f,false);yield return Travel(74,0,true);
            yield return Travel(78,0,true);yield return Travel(83,1.5f,true);yield return Travel(85,0,false);yield return Travel(90.4f,0,true);yield return Travel(95,0,false);
            yield return Ride(99,103.5f,110.8f,.4f);
            yield return Travel(113,.4f,false);yield return Travel(117,2,true);yield return Travel(123,.4f,true);yield return Travel(125,.4f,false);
            yield return Travel(130,1.2f,true);yield return Travel(136,2,true);yield return Travel(139,3.5f,true);yield return Travel(143,1.2f,true);yield return Travel(149,.4f,true);
            yield return Travel(155,2,true);yield return Travel(161,.4f,true);yield return Travel(163,.4f,false);
            yield return Ride(167,170.5f,177,.5f);
            yield return Travel(178.5f,.5f,false);yield return Travel(182,2,true);yield return Travel(187,.5f,true);yield return Travel(194,.5f,false);
            yield return Travel(199,2,true);yield return Travel(203,.5f,true);yield return Travel(209,.5f,true);yield return Travel(212,.5f,false);
            yield return Travel(218,1.6f,true);yield return Travel(223,2.6f,true);yield return Travel(229,1.2f,true);yield return Travel(231,1.2f,false);
            yield return Travel(233,1.2f,false);yield return Travel(235,2.7f,true);yield return Travel(240,4.1f,true);yield return Travel(243.2f,1.2f,true);yield return Travel(248,1.2f,true);
            yield return Travel(252,2.1f,true);yield return Travel(257,3,true);yield return Travel(264,1,true);yield return Travel(266.2f,1,false);
            yield return Travel(271,2.5f,true);yield return Travel(277,1,true);yield return Travel(284,1,false);
            p.automation=false;
            if(!Failed){Status=game.State==GameState.Won?"PASS: continuous full-level traversal":"FAIL: no win state";Record(Status+" | seeds="+game.Seeds+" coins="+game.Coins+" deaths="+game.Deaths+" checkpoints="+game.Checkpoints+" time="+game.Elapsed.ToString("F1"));}
        }
    }
}
#endif
