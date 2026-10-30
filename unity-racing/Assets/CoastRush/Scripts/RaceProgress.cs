using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoastRush
{
    [Serializable]
    public sealed class RaceProgress
    {
        public int completedLaps;
        public int nextGate;
        public int passedGates;
        public bool started;
        public bool finished;
        public float finishTime;
        public float distance;
        public float lateral;
        public float lapStartedAt;
        public float lastSafeDistance;
        public List<float> lapTimes = new List<float>();
        [NonSerialized] public Vector3 previousPosition;
        public float BestLap { get { float best=float.MaxValue; foreach(float t in lapTimes) best=Mathf.Min(best,t); return best==float.MaxValue ? 0 : best; } }
        public float Score(CoastTrack track) => finished ? 100000f-finishTime : passedGates*track.length/CoastTrack.GateCount + Mathf.Clamp(Mathf.Repeat(distance-(nextGate-1)*track.length/CoastTrack.GateCount,track.length),0,track.length/CoastTrack.GateCount);

        public void Reset(CoastTrack track, Vector3 position, float spawnDistance)
        {
            completedLaps=nextGate=passedGates=0; started=finished=false;
            finishTime=lapStartedAt=0; lapTimes.Clear(); previousPosition=position;
            distance=Mathf.Repeat(spawnDistance,track.length);lastSafeDistance=spawnDistance;
        }

        public bool Observe(CoastTrack track, Vector3 position, float elapsed, int totalLaps)
        {
            distance=track.Project(position,out lateral,out float separation);
            if (separation<2.5f && Mathf.Abs(lateral)<track.halfWidth-1) lastSafeDistance=distance;
            if (finished) {previousPosition=position; return false;}
            float gateDistance=nextGate*track.length/CoastTrack.GateCount;
            var gate=track.Position(gateDistance); var forward=track.Forward(gateDistance);
            float before=Vector3.Dot(previousPosition-gate,forward), after=Vector3.Dot(position-gate,forward);
            bool crossed=before<0 && after>=0 && (position-previousPosition).sqrMagnitude<225;
            if (crossed)
            {
                var intersection=Vector3.Lerp(previousPosition,position,-before/(after-before));
                var delta=intersection-gate;
                crossed=Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,forward).normalized))<track.halfWidth+1 && Mathf.Abs(delta.y)<2.5f;
            }
            previousPosition=position;
            if (!crossed) return false;
            passedGates++;
            if (nextGate==0)
            {
                if (!started) {started=true;lapStartedAt=elapsed;}
                else
                {
                    completedLaps++;lapTimes.Add(elapsed-lapStartedAt);lapStartedAt=elapsed;
                    if(completedLaps>=totalLaps){finished=true;finishTime=elapsed;return true;}
                }
            }
            nextGate=(nextGate+1)%CoastTrack.GateCount;
            return false;
        }
    }
}
