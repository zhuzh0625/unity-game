using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CoastRush
{
    // Opt-in integration acceptance: goes through Keyboard and the normal UI/vehicle update loops.
    // This is intentionally separate from OS keyboard injection and never substitutes for that evidence.
    public sealed class KeyboardVerification : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string source="Unity Input System KeyboardState events; autopilot and control overrides disabled",error="";
            public float accelerationKph,displacement,leftYaw,rightYaw,reverseMetersPerSecond,arrowAccelerationKph,pauseClockDelta,pauseMovement;
            public List<string> checks=new List<string>();
        }
        readonly Report report=new Report();
        Keyboard keyboard,previousKeyboard;
        RaceDirector race;
        string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-coast-keyboard-verify")>=0)Begin();
        }
        public static string Begin()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run in Play mode.");
            new GameObject("Keyboard acceptance runner").AddComponent<KeyboardVerification>();
            return "Keyboard acceptance started";
        }
        IEnumerator Start()
        {
            yield return null;
            race=RaceDirector.Instance;race.verificationRun=true;
            previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>("CoastAcceptanceKeyboard");keyboard.MakeCurrent();
            output=Application.isEditor?Path.GetFullPath("doc"):Path.GetFullPath(Path.Combine(Application.dataPath,"../../../doc"));
            Directory.CreateDirectory(output);
            var checks=Run();
            while(true)
            {
                bool next;
                try{next=checks.MoveNext();}
                catch(Exception exception){report.error=exception.ToString();break;}
                if(!next){report.passed=true;break;}
                yield return checks.Current;
            }
            InputSystem.RemoveDevice(keyboard);if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();
            race.MainMenu();
            File.WriteAllText(Path.Combine(output,"keyboard-verification.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[CoastChecks] Keyboard acceptance "+JsonUtility.ToJson(report));
            yield return new WaitForSecondsRealtime(.5f);
            if(!Application.isEditor)Application.Quit(report.passed?0:2);
            else Destroy(gameObject);
        }
        IEnumerator Press(float seconds,Action sample,params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            float until=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<until){yield return null;sample?.Invoke();}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return null;yield return null;
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        IEnumerator Run()
        {
            var car=race.Player;
            Require(!car.useAutopilot&&!car.overrideControls,"Acceptance requires ordinary player input.");
            Require(race.State==RaceState.MainMenu,"Expected a fresh main menu.");
            yield return Press(.15f,null,Key.Enter);
            Require(race.State==RaceState.Countdown,"Enter did not start competition.");
            float deadline=Time.realtimeSinceStartup+5;
            while(race.State==RaceState.Countdown&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(race.State==RaceState.Racing&&!race.timeTrial,"Countdown did not enter competition.");
            report.checks.Add("Enter starts four-car race and countdown");

            Vector3 origin=car.transform.position;
            yield return Press(3,null,Key.W);
            report.accelerationKph=car.SpeedKph;report.displacement=Vector3.Distance(origin,car.transform.position);
            Require(report.accelerationKph>25&&report.displacement>10,"W did not accelerate and move the player.");
            report.checks.Add("W accelerates through the ordinary keyboard path");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"keyboard-driving.png"));

            // Exercise steering on a clear straight at moderate speed, with a fresh start for each direction.
            // A full-lock left/right slalom at 70 km/h can hit the rail before the second assertion.
            yield return Press(.12f,null,Key.R);yield return Press(.8f,null,Key.W);
            Vector3 heading=car.transform.forward;float leftInput=0,rightInput=0;
            yield return Press(.4f,()=>leftInput=Mathf.Min(leftInput,car.steering),Key.W,Key.A);
            report.leftYaw=Vector3.SignedAngle(heading,car.transform.forward,Vector3.up);
            yield return Press(.6f,null,Key.S);yield return new WaitForSecondsRealtime(.5f);
            yield return Press(.12f,null,Key.R);yield return Press(.8f,null,Key.W);
            heading=car.transform.forward;
            yield return Press(.4f,()=>rightInput=Mathf.Max(rightInput,car.steering),Key.W,Key.D);
            report.rightYaw=Vector3.SignedAngle(heading,car.transform.forward,Vector3.up);
            Require(leftInput<-.9f&&rightInput>.9f&&report.leftYaw<-1&&report.rightYaw>1,"A/D steering did not turn the physical car.");
            report.checks.Add("W+A / W+D turn left and right on a clear straight at moderate speed");

            bool handbrake=false;
            yield return Press(.25f,()=>handbrake|=car.handbrake&&car.wheels[2].brakeTorque>1000&&car.wheels[2].sidewaysFriction.stiffness<1,Key.Space);
            Require(handbrake,"Space did not apply rear handbrake.");
            report.checks.Add("Space applies handbrake and reduces rear grip");

            bool stopped=false;float reverse=0;
            yield return Press(4,()=>{stopped|=car.SpeedKph<2;reverse=Mathf.Min(reverse,car.ForwardSpeed);},Key.S);
            report.reverseMetersPerSecond=reverse;
            Require(stopped&&reverse<-2,"S did not brake then reverse.");
            report.checks.Add("S brakes to a stop and then reverses");

            int gate=car.progress.nextGate,resets=car.respawns;
            yield return Press(.12f,null,Key.R);
            Require(car.respawns==resets+1&&car.progress.nextGate==gate&&Mathf.Abs(car.ForwardSpeed)<1,$"R reset: respawns {resets}->{car.respawns}; gate {gate}->{car.progress.nextGate}; forward speed {car.ForwardSpeed:F3}m/s.");
            report.checks.Add("R resets on the road without skipping a checkpoint");

            yield return Press(2,null,Key.UpArrow);report.arrowAccelerationKph=car.SpeedKph;
            bool leftArrow=false,rightArrow=false,arrowBrake=false;
            yield return Press(.2f,()=>leftArrow|=car.steering<-.9f,Key.LeftArrow);
            yield return Press(.2f,()=>rightArrow|=car.steering>.9f,Key.RightArrow);
            yield return Press(1.3f,()=>arrowBrake|=car.braking>.9f,Key.DownArrow);
            Require(report.arrowAccelerationKph>20&&leftArrow&&rightArrow&&arrowBrake,"Arrow key aliases did not reach driving controls.");
            report.checks.Add("Arrow keys accelerate, steer and brake");

            yield return Press(.12f,null,Key.Escape);
            Require(race.State==RaceState.Paused&&Time.timeScale==0,"Esc did not pause.");
            float pausedAt=race.Elapsed;var parked=new Vector3[race.racers.Length];
            for(int i=0;i<parked.Length;i++)parked[i]=race.racers[i].transform.position;
            yield return Press(.75f,null,Key.W);
            report.pauseClockDelta=Mathf.Abs(race.Elapsed-pausedAt);
            for(int i=0;i<parked.Length;i++)report.pauseMovement=Mathf.Max(report.pauseMovement,Vector3.Distance(parked[i],race.racers[i].transform.position));
            Require(report.pauseClockDelta<.001f&&report.pauseMovement<.001f,"Pause did not freeze the clock and every car.");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"keyboard-pause.png"));
            yield return Press(.12f,null,Key.Escape);yield return new WaitForSecondsRealtime(.25f);
            Require(race.State==RaceState.Racing&&Time.timeScale==1&&race.Elapsed>pausedAt,"Esc did not resume.");
            report.checks.Add("Esc pauses/resumes; held W cannot move any car or advance time while paused");

            yield return Press(.12f,null,Key.Escape);
            Require(EventSystem.current.currentSelectedGameObject==race.hud.resumeButton.gameObject,"Pause menu has no default keyboard selection.");
            yield return Press(.15f,null,Key.DownArrow);
            Require(EventSystem.current.currentSelectedGameObject==race.hud.restartButton.gameObject,"Down did not select Restart.");
            yield return Press(.15f,null,Key.Enter);
            Require(race.State==RaceState.Countdown&&race.Countdown>2.8f&&race.Elapsed==0&&Time.timeScale==1,"Keyboard restart did not reset clock/countdown.");
            foreach(var racer in race.racers)Require(racer.gameObject.activeSelf&&racer.progress.completedLaps==0&&racer.progress.passedGates==0&&racer.respawns==0,"Restart did not reset all cars and progress.");
            report.checks.Add("Esc, Down, Enter restarts all four cars, laps, checkpoints, clock and countdown");
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"keyboard-restart.png"));
            deadline=Time.realtimeSinceStartup+5;
            while(race.State==RaceState.Countdown&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return Press(1.2f,null,Key.W);
            Require(race.State==RaceState.Racing&&car.SpeedKph>10,"Driving did not work after restart.");
            report.checks.Add("W driving works again after restart");
        }
    }
}
