using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CoastRush
{
    // Opt-in acceptance runner. Ordinary game launches never attach this component.
    public sealed class NativeVerification : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string version,reason;
            public int width,height,frames,checkpoints,respawns;
            public float measuredSeconds,averageFps,speedKph;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-coast-verify")>=0)
                new GameObject("Native acceptance runner").AddComponent<NativeVerification>();
        }
        IEnumerator Start()
        {
            yield return null;
            var r=RaceDirector.Instance;r.verificationRun=true;r.StartRace(false);r.Player.useAutopilot=true;
            float began=Time.realtimeSinceStartup;int frames=0;float measuredStart=0;
            while(Time.realtimeSinceStartup-began<38)
            {
                float elapsed=Time.realtimeSinceStartup-began;
                if(elapsed>=8){if(measuredStart==0)measuredStart=Time.realtimeSinceStartup;frames++;}
                yield return null;
            }
            var report=new Report{version=Application.version,width=Screen.width,height=Screen.height,frames=frames,measuredSeconds=Time.realtimeSinceStartup-measuredStart,checkpoints=r.Player.progress.passedGates,respawns=r.Player.respawns,speedKph=r.Player.SpeedKph};
            report.averageFps=frames/report.measuredSeconds;
            report.passed=r.State==RaceState.Racing&&report.checkpoints>=8&&report.respawns==0;
            report.reason=report.passed?"Standalone renderer and physical driving advanced normally":"Standalone driving did not meet progression assertions";
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../doc"));Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"native-verification.json"),JsonUtility.ToJson(report,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"native-racing.png"));
            yield return new WaitForSecondsRealtime(1);
            Debug.Log("[CoastChecks] Native acceptance "+JsonUtility.ToJson(report));Application.Quit(report.passed?0:2);
        }
    }
}
