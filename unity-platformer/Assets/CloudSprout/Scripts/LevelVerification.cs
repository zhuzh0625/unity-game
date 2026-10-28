#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CloudSprout
{
    // Editor-only input driver. Exercises the real controller and collision world.
    public sealed class LevelVerification:MonoBehaviour
    {
        public string Status="Starting";
        public List<string> results=new List<string>();
        SproutPlayer p;GameSession g;
        float maxHeight;
        public void StartChecks(){StartCoroutine(Checks());}
        void Check(bool value,string name){results.Add((value?"PASS ":"FAIL ")+name);Debug.Log(results[results.Count-1]);Save();}
        void Save(){Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mechanics.txt",string.Join("\n",results));}
        IEnumerator Settle(Vector3 position){p.autoMove=0;p.autoJump=false;p.autoHold=false;p.Respawn(position);yield return new WaitForSeconds(.5f);}
        IEnumerator Jump(float holdDuration)
        {
            p.autoJump=true;p.autoHold=true;maxHeight=p.transform.position.y;
            float t=Time.time;
            while(Time.time-t<1.15f){p.autoHold=Time.time-t<holdDuration;maxHeight=Mathf.Max(maxHeight,p.transform.position.y);yield return null;}
            p.autoHold=false;
        }
        IEnumerator Checks()
        {
            g=GameSession.Instance;p=g.player;g.Begin();p.automation=true;
            yield return Settle(new Vector3(3,.2f,0));Check(p.Grounded,"Player rests on ground");
            yield return Jump(.05f);float shortJump=maxHeight;
            yield return Settle(new Vector3(3,.2f,0));yield return Jump(1);Check(maxHeight>shortJump+.6f,"Variable jump height (tap="+shortJump.ToString("F2")+", hold="+maxHeight.ToString("F2")+")");
            yield return Settle(new Vector3(3,.2f,0));float x=p.transform.position.x;p.autoMove=1;yield return new WaitForSeconds(.6f);p.autoMove=0;Check(p.transform.position.x>x+2,"Keyboard-equivalent horizontal movement");
            yield return Settle(new Vector3(6,.25f,0));yield return new WaitForSeconds(.1f);int coins=g.Coins;
            yield return Settle(new Vector3(6,.25f,0));Check(g.Coins==coins&&coins>0,"Coin collection is one-shot");
            yield return Settle(new Vector3(10,.25f,0));yield return Jump(1);Check(g.BlocksHit==1,"Reward block activated by head collision");int reward=g.Coins;yield return Jump(1);Check(g.Coins==reward,"Reward block cannot pay twice");
            var enemy=FindObjectsByType<PatrolEnemy>(FindObjectsSortMode.None).First(e=>e.left==27);enemy.speed=0;enemy.gameObject.SetActive(false);
            bool allStomps=true;string failedContact="";
            for(int i=0;i<8;i++)
            {
                var sample=Instantiate(enemy.gameObject);sample.transform.position=new Vector3(30,.08f,0);sample.SetActive(true);int before=g.Stomps;
                p.Respawn(sample.transform.position+Vector3.up*(2.4f+.17f*i));yield return new WaitForSeconds(1.05f);
                if(g.Stomps!=before+1){allStomps=false;failedContact=sample.GetComponent<PatrolEnemy>().LastContact;}
                if(sample)Destroy(sample);
            }
            Check(allStomps,"Stomp and bounce at 8 different fall timings"+(allStomps?"":" ("+failedContact+")"));
            enemy.transform.position=new Vector3(30,.08f,0);enemy.gameObject.SetActive(true);p.Respawn(new Vector3(30,.2f,0));yield return new WaitForSeconds(1.8f);Check(g.Health==2,"Side contact with enemy damages instead of stomping");enemy.gameObject.SetActive(false);
            yield return Settle(new Vector3(92,.25f,0));Check(g.Checkpoints==1,"First checkpoint activates");
            p.Respawn(new Vector3(100,-9,0));yield return new WaitForSeconds(.85f);Check(g.Deaths==1&&Mathf.Abs(p.transform.position.x-g.Spawn.x)<.2f&&g.Health==3,"Pit death returns to checkpoint with full health");
            yield return Settle(new Vector3(35,.25f,0));yield return new WaitForSeconds(1.3f);Check(g.Health==2,"Spike damage and temporary invulnerability");
            var moving=FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None)[0];p.Respawn(moving.transform.position+new Vector3(0,.4f,0));yield return new WaitForSeconds(.45f);float relative=p.transform.position.x-moving.transform.position.x;yield return new WaitForSeconds(.65f);Check(p.Grounded&&Mathf.Abs((p.transform.position.x-moving.transform.position.x)-relative)<.25f,"Moving platform carries standing player");
            int jumps=p.JumpCount;p.autoJump=true;p.autoHold=true;yield return new WaitForSeconds(.2f);Check(p.JumpCount==jumps+1 && p.VerticalSpeed>0,"Jumping from a moving platform");p.autoHold=false;
            g.TogglePause();var frozen=p.transform.position;yield return new WaitForSecondsRealtime(.25f);Check(g.State==GameState.Paused&&p.transform.position==frozen,"Pause freezes movement");g.TogglePause();
            p.Respawn(new Vector3(284,1.1f,0));yield return new WaitForSeconds(.25f);Check(g.State==GameState.Won,"Sun gate triggers completion screen");
            p.automation=false;Status="Complete";Save();
        }
    }
}
#endif
