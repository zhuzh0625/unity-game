using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastRush;
using UnityEditor;
using UnityEngine;

public static class CoastChecks
{
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static string RulesAndAssets()
    {
        var track=UnityEngine.Object.FindFirstObjectByType<CoastTrack>();Require(track!=null,"Missing track");
        var p=new RaceProgress();float clock=0;
        void Cross(int gate,bool reverse=false,float lateral=0)
        {
            float s=gate*track.length/CoastTrack.GateCount;
            p.previousPosition=track.Position(s+(reverse?2:-2),lateral)+Vector3.up*.18f;
            p.Observe(track,track.Position(s+(reverse?-2:2),lateral)+Vector3.up*.18f,++clock,3);
        }
        p.Reset(track,track.Position(-2),-2);Cross(0);
        Require(p.started&&p.nextGate==1&&p.completedLaps==0,"Start crossing should arm the first lap, not complete it");
        Cross(0);Require(p.completedLaps==0&&p.nextGate==1,"Repeated start crossing bypassed checkpoints");
        Cross(1,true);Require(p.nextGate==1,"Reverse crossing counted");
        Cross(1,false,22);Require(p.nextGate==1,"Outside-road crossing counted");
        Cross(3);Require(p.nextGate==1,"Skipped checkpoints counted");
        for(int lap=0;lap<3;lap++){for(int gate=1;gate<CoastTrack.GateCount;gate++)Cross(gate);Cross(0);}
        Require(p.finished&&p.completedLaps==3&&p.lapTimes.Count==3,"Three valid laps did not finish");
        float finish=p.finishTime;Cross(0);Require(p.finishTime==finish&&p.completedLaps==3,"Finished result changed after further crossings");
        Physics.SyncTransforms();int samples=0,holes=0;
        for(float s=0;s<track.length;s+=3)foreach(float lane in new[]{-5f,0,5f})
        {
            var point=track.Position(s,lane);samples++;
            if(!Physics.Raycast(point+Vector3.up*1.7f,Vector3.down,out var hit,2.5f,1<<8)||Mathf.Abs(hit.point.y-point.y)>.25f)holes++;
        }
        Require(holes==0,$"Road surface has {holes}/{samples} missing or displaced collider samples");
        var cars=UnityEngine.Object.FindObjectsByType<VehicleController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        Require(cars.Length==4,"Expected four cars");
        foreach(var car in cars)
        {
            Require(car.wheels.Length==4&&car.wheelVisuals.Length==4,"Incomplete wheels");
            foreach(var w in car.wheels)Require(w.radius>.3f&&w.radius<.5f,"Wheel radius invalid");
        }
        var badShaders=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .SelectMany(r=>r.sharedMaterials).Where(m=>m&&(m.shader==null||m.shader.name.Contains("InternalError")||ShaderUtil.ShaderHasError(m.shader))).Select(m=>m.name).Distinct().ToArray();
        Require(badShaders.Length==0,"Invalid shaders: "+string.Join(",",badShaders));
        string result=$"PASS: sequential checkpoints, reverse/outside/shortcut rejection, 3-lap finish, immutable finish, {samples} road raycasts, 4 vehicles, shaders.";
        Directory.CreateDirectory("doc");File.WriteAllText("doc/rules-verification.txt",DateTime.Now.ToString("s")+"\n"+result+"\n");return result;
    }

    public static string SaveDriveResult()
    {
        var r=RaceDirector.Instance;Require(r&&r.verificationRun,"This is not an authorized verification session");
        Require(r.State==RaceState.Finished,"Drive has not finished");
        Require(r.Player.progress.completedLaps==3&&r.Player.progress.lapTimes.Count==3,"Player did not drive three laps");
        Require(r.Player.respawns==0,"Player needed a respawn");
        var report=new DriveReport{time=DateTime.Now.ToString("s"),elapsed=r.Elapsed,place=r.finalPlace,frame=Time.frameCount,cars=new List<CarReport>()};
        foreach(var car in r.racers)report.cars.Add(new CarReport{name=car.driverName,laps=car.progress.completedLaps,gates=car.progress.passedGates,finished=car.progress.finished,finishTime=car.progress.finishTime,respawns=car.respawns,lapTimes=car.progress.lapTimes.ToArray()});
        Directory.CreateDirectory("doc");string json=JsonUtility.ToJson(report,true);File.WriteAllText("doc/autopilot-verification.json",json);return json;
    }
    [Serializable]class DriveReport{public string time;public float elapsed;public int place,frame;public List<CarReport> cars;}
    [Serializable]class CarReport{public string name;public int laps,gates,respawns;public bool finished;public float finishTime;public float[] lapTimes;}
}
