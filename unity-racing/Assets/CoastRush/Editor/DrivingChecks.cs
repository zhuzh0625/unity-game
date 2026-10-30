using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CoastRush;
using UnityEngine;

public static class DrivingChecks
{
    [Serializable] public class Report {public bool passed;public string error;public float accelerationKph,brakingKph,reverseMetersPerSecond;public List<string> checks=new List<string>();}
    public static string Begin()
    {
        if(!Application.isPlaying)throw new Exception("Run in Play mode");
        RaceDirector.Instance.StartCoroutine(Run());return "Driving checks started; report: doc/driving-verification.json";
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static IEnumerator Run()
    {
        var report=new Report();var r=RaceDirector.Instance;r.verificationRun=true;r.StartRace(true);
        var car=r.Player;car.useAutopilot=false;car.overrideControls=true;car.testInput=Vector3.zero;
        while(r.State==RaceState.Countdown)yield return null;
        car.testInput=new Vector3(1,0,0);yield return new WaitForSeconds(3);
        report.accelerationKph=car.SpeedKph;
        car.testInput=new Vector3(0,0,1);yield return new WaitForSeconds(3);
        report.brakingKph=car.SpeedKph;
        car.testInput=new Vector3(-.55f,0,0);yield return new WaitForSeconds(2);
        report.reverseMetersPerSecond=car.ForwardSpeed;
        car.testInput=Vector3.zero;
        r.TogglePause();float elapsed=r.Elapsed;yield return new WaitForSecondsRealtime(.6f);
        bool held=Mathf.Abs(r.Elapsed-elapsed)<.001f;r.TogglePause();
        try
        {
            Require(report.accelerationKph>25,"Acceleration failed");report.checks.Add("three-second acceleration");
            Require(report.brakingKph<5,"Braking failed");report.checks.Add("three-second full brake");
            Require(report.reverseMetersPerSecond<-2,"Reverse failed");report.checks.Add("reverse gear");
            Require(held&&Time.timeScale==1,"Pause/resume failed");report.checks.Add("pause freezes race clock; resume restores simulation");
            int gate=car.progress.nextGate;car.Respawn();Require(car.progress.nextGate==gate,"Respawn bypassed gate");
            float roadDistance=r.track.Project(car.transform.position,out float lateral,out float separation);Require(Mathf.Abs(lateral)<r.track.halfWidth-1 && Mathf.Abs(car.transform.position.y-r.track.Position(roadDistance).y)<.5f,"Respawn did not return to road");report.checks.Add("safe respawn preserves checkpoint sequence");
            r.StartRace(false);Require(r.racers[1].gameObject.activeSelf&&r.Countdown>3&&r.Elapsed==0,"Restart failed");report.checks.Add("competition restart restores AI, clock and countdown");
            r.MainMenu();Require(r.State==RaceState.MainMenu&&car.Body.isKinematic,"Menu return failed");report.checks.Add("return to main menu");
            report.passed=true;
        }
        catch(Exception e){report.error=e.ToString();}
        finally
        {
            car.overrideControls=false;r.MainMenu();Directory.CreateDirectory("doc");File.WriteAllText("doc/driving-verification.json",JsonUtility.ToJson(report,true));
            Debug.Log("[CoastChecks] Driving checks "+(report.passed?"PASS":report.error));
        }
    }
}
