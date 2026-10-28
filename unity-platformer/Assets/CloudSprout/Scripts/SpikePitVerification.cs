#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CloudSprout
{
    // Editor-only samples: drive the real controller into, out of, and across a pit.
    public sealed class SpikePitVerification : MonoBehaviour
    {
        public string Status="Ready";
        public List<string> results=new List<string>();
        GameSession game;
        SproutPlayer player;
        bool running;

        public void StartChecks()
        {
            if(running)return;
            running=true;
            StartCoroutine(Checks());
        }

        void Check(bool passed,string description)
        {
            results.Add((passed?"PASS ":"FAIL ")+description);
            Debug.Log(results[results.Count-1]);
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/spike-pits.txt",string.Join("\n",results));
        }

        void StopInput()
        {
            player.autoMove=0;player.autoJump=false;player.autoHold=false;player.autoRun=false;
        }

        IEnumerator PlaceAndWait(Vector3 position)
        {
            StopInput();player.Respawn(position);
            // Respawn grants 1.5 seconds of protection; samples must not rely on it.
            yield return new WaitForSeconds(1.8f);
        }

        void Steer(float targetX)
        {
            player.autoMove=Mathf.Clamp((targetX-player.transform.position.x)*6/player.walkSpeed,-1,1);
        }

        void Finish()
        {
            if(player){StopInput();player.automation=false;}
            if(game&&game.State==GameState.Playing)game.TogglePause();
            Status=results.Any(line=>line.StartsWith("FAIL "))?"Failed":"Complete";
            running=false;
        }

        void CheckWholeTileLayout(Hazard[] pits)
        {
            var openings=new[]{new Vector2(35,37),new Vector2(87,89),new Vector2(158,160),new Vector2(204,206),new Vector2(244,246),new Vector2(267,269)};
            // x/y are the original ground range; z is its walking surface height.
            var ranges=new[]{new Vector3(25,47,0),new Vector3(73,96,0),new Vector3(148,164,.4f),new Vector3(194,213,.5f),new Vector3(228,249,1.2f),new Vector3(263,297,1)};
            var terrain=GameObject.Find("01 Terrain");
            var tiles=terrain?terrain.GetComponentsInChildren<Transform>().Where(t=>t.name=="GrassTile").ToArray():new Transform[0];
            bool aligned=true,wholeTiles=terrain!=null,clearOpenings=terrain!=null;
            for(int i=0;i<openings.Length;i++)
            {
                var opening=openings[i];var range=ranges[i];var pit=pits[i];
                var trigger=pit.GetComponent<BoxCollider>();
                aligned&=trigger!=null&&Mathf.Abs(pit.transform.position.x-(opening.x+opening.y)*.5f)<.01f
                    &&Mathf.Abs(pit.transform.position.y-range.z)<.01f&&Mathf.Abs(trigger.center.x)<.01f
                    &&Mathf.Abs(trigger.size.x+.12f-(opening.y-opening.x))<.01f;
                var regionTiles=tiles.Where(t=>Mathf.Abs(t.position.y-range.z)<.01f
                    &&t.position.x+t.lossyScale.x*.5f>range.x+.001f
                    &&t.position.x-t.lossyScale.x*.5f<range.y-.001f).ToArray();
                clearOpenings&=regionTiles.All(t=>t.position.x+t.lossyScale.x*.5f<=opening.x+.01f
                    ||t.position.x-t.lossyScale.x*.5f>=opening.y-.01f);
                int expectedCount=0;
                for(float start=range.x;start<range.y;start+=2)
                {
                    float width=Mathf.Min(2,range.y-start),end=start+width;
                    if(start<opening.y-.01f&&end>opening.x+.01f)continue;
                    expectedCount++;
                    // Exactly one intact tile in every original grid cell, including a 1 m tail.
                    wholeTiles&=regionTiles.Count(t=>Mathf.Abs(t.position.x-(start+end)*.5f)<.01f
                        &&Mathf.Abs(t.lossyScale.x-width)<.01f)==1;
                }
                wholeTiles&=regionTiles.Length==expectedCount;
            }
            Check(aligned,"All six pits match their planned 2 m tile boundaries and ground heights");
            Check(wholeTiles,"All six ground ranges retain complete original 2 m tiles and any original 1 m tail");
            Check(clearOpenings,"No GrassTile overlaps any of the six pit openings");
        }

        IEnumerator Checks()
        {
            Status="Inspecting pit geometry";results.Clear();
            game=GameSession.Instance;
            if(!game){Check(false,"Game session is available");Finish();yield break;}
            player=game.player;game.Begin();player.automation=true;StopInput();
            var pits=FindObjectsByType<Hazard>(FindObjectsSortMode.None)
                .Where(h=>h.name=="Spike Pit").OrderBy(h=>h.transform.position.x).ToArray();
            Check(pits.Length==6,"Six recessed spike pits exist");
            if(pits.Length!=6){Finish();yield break;}
            bool roots=true,floors=true,banks=true,belowSurface=true;
            foreach(var pit in pits)
            {
                var root=pit.transform;var trigger=pit.GetComponent<BoxCollider>();
                roots&=Quaternion.Angle(root.rotation,Quaternion.identity)<.01f&&Vector3.Distance(root.lossyScale,Vector3.one)<.001f;
                if(!trigger||!trigger.isTrigger){floors=banks=belowSurface=false;continue;}
                float width=trigger.size.x+.12f;
                belowSurface&=trigger.bounds.max.y<root.position.y-.15f;
                Vector3 origin=root.position+Vector3.up*.1f;
                floors&=Physics.Raycast(origin,Vector3.down,out var floor,2f,~0,QueryTriggerInteraction.Ignore)
                    &&floor.collider.name=="Pit Floor"&&Mathf.Abs(floor.point.y-(root.position.y-1.08f))<.04f;
                foreach(float side in new[]{-1f,1f})
                {
                    origin=root.position+new Vector3(side*(width*.5f+.4f),.1f,0);
                    banks&=Physics.Raycast(origin,Vector3.down,out var bank,.3f,~0,QueryTriggerInteraction.Ignore)
                        &&Mathf.Abs(bank.point.y-root.position.y)<.04f;
                }
            }
            Check(roots,"Pit collision roots have identity rotation and unit scale");
            Check(floors,"All six openings expose their recessed floor without an invisible ground bridge");
            Check(banks,"Both banks of every pit retain ground support");
            Check(belowSurface,"Damage volumes stay below the walking surface");
            CheckWholeTileLayout(pits);
            bool patrolSupported=true;
            foreach(var enemy in FindObjectsByType<PatrolEnemy>(FindObjectsSortMode.None))
            {
                // Sample 1 cm inside the full clearance to avoid a ray exactly on a collider seam.
                foreach(float edge in new[]{enemy.left-.64f,enemy.right+.64f})
                    patrolSupported&=Physics.Raycast(new Vector3(edge,enemy.transform.position.y+.1f,0),Vector3.down,out var support,.35f,~0,QueryTriggerInteraction.Ignore)&&support.collider.name.StartsWith("Ground ");
                foreach(var pit in pits)
                {
                    if(Mathf.Abs(enemy.transform.position.y-pit.transform.position.y-.08f)>.1f)continue;
                    float half=(pit.GetComponent<BoxCollider>().size.x+.12f)*.5f;
                    float pitLeft=pit.transform.position.x-half,pitRight=pit.transform.position.x+half;
                    patrolSupported&=enemy.right+.65f<=pitLeft+.001f||enemy.left-.65f>=pitRight-.001f;
                }
            }
            Check(patrolSupported,"Every complete patrol interval and visual footprint avoid pits and retain bank support");

            var sample=pits.OrderBy(p=>Mathf.Abs(p.transform.position.x-88.5f)).First();
            var collider=sample.GetComponent<BoxCollider>();
            if(!collider){Finish();yield break;}
            Vector3 center=sample.transform.position;
            float halfWidth=(collider.size.x+.12f)*.5f;
            float left=center.x-halfWidth,right=center.x+halfWidth;
            float rightBank=right+.85f,leftBank=left-.4f;
            Status="Walking into the pit";
            int beforeHealth=game.Health,beforeDeaths=game.Deaths;
            yield return PlaceAndWait(new Vector3(rightBank,center.y+.2f,0));
            Check(player.Grounded&&!player.Invincible&&Mathf.Abs(player.transform.position.y-center.y)<.08f&&game.Health==beforeHealth,
                "Safe bank supports the player without damage after spawn protection expires");
            float minimumFeet=player.transform.position.y;
            float deadline=Time.time+2.5f;
            while(game.Health==beforeHealth&&game.Deaths==beforeDeaths&&Time.time<deadline)
            {
                Steer(center.x);
                minimumFeet=Mathf.Min(minimumFeet,player.transform.position.y);
                yield return null;
            }
            StopInput();
            minimumFeet=Mathf.Min(minimumFeet,player.transform.position.y);
            bool damaged=game.Health==beforeHealth-1&&game.Deaths==beforeDeaths;
            Check(damaged&&minimumFeet<center.y-.15f,
                "Walking into the opening causes a real fall and exactly one heart of damage");
            int afterDamage=game.Health;
            yield return new WaitForSeconds(.45f);
            Check(damaged&&player.Invincible&&game.Health==afterDamage&&game.Deaths==beforeDeaths,
                "Post-hit protection prevents immediate repeated damage");
            if(!damaged||game.State!=GameState.Playing){Finish();yield break;}

            deadline=Time.time+1.1f;
            while((!player.Grounded||player.transform.position.y>center.y-.6f)&&Time.time<deadline)yield return null;
            bool onFloor=player.Grounded&&Mathf.Abs(player.transform.position.y-(center.y-1.08f))<.08f;
            Check(onFloor,"Player can stand on the pit floor after the damage bounce");
            Status="Jumping out of the pit";
            int jumps=player.JumpCount;
            player.autoJump=true;player.autoHold=true;
            deadline=Time.time+3;
            while(Time.time<deadline&&game.Deaths==beforeDeaths)
            {
                Steer(rightBank);
                if(player.JumpCount>jumps&&player.Grounded&&player.transform.position.x>right+.32f&&Mathf.Abs(player.transform.position.y-center.y)<.1f)break;
                yield return null;
            }
            StopInput();
            bool escaped=onFloor&&player.JumpCount==jumps+1&&player.Grounded&&player.transform.position.x>right+.32f
                &&Mathf.Abs(player.transform.position.y-center.y)<.1f&&game.Deaths==beforeDeaths;
            Check(escaped,"A normal jump from the floor returns the player to the bank");
            if(!escaped||game.State!=GameState.Playing){Finish();yield break;}

            Status="Jumping across the pit";
            yield return PlaceAndWait(new Vector3(rightBank,center.y+.2f,0));
            beforeHealth=game.Health;beforeDeaths=game.Deaths;jumps=player.JumpCount;
            player.autoJump=true;player.autoHold=true;
            bool dipped=false;
            deadline=Time.time+3;
            while(Time.time<deadline&&game.Deaths==beforeDeaths)
            {
                Steer(leftBank);
                Vector3 pos=player.transform.position;
                if(pos.x>left&&pos.x<right&&pos.y<center.y-.05f)dipped=true;
                if(player.JumpCount>jumps&&player.Grounded&&pos.x<left-.32f&&Mathf.Abs(pos.y-center.y)<.1f)break;
                yield return null;
            }
            StopInput();
            Check(!dipped&&player.JumpCount==jumps+1&&player.Grounded&&player.transform.position.x<left-.32f
                &&Mathf.Abs(player.transform.position.y-center.y)<.1f&&game.Health==beforeHealth&&game.Deaths==beforeDeaths,
                "A normal held jump crosses the pit without damage or falling in");
            Finish();
        }
    }
}
#endif
