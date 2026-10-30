using UnityEngine;
using UnityEngine.InputSystem;

namespace CoastRush
{
    public enum RaceState { MainMenu, Countdown, Racing, Paused, Finished }
    public sealed class RaceDirector : MonoBehaviour
    {
        public static RaceDirector Instance {get;private set;}
        public CoastTrack track;
        public VehicleController[] racers;
        public RaceHUD hud;
        public ChaseCamera chaseCamera;
        public RaceState State {get;private set;} = RaceState.MainMenu;
        public RaceState pausedState;
        public const int TotalLaps = 3;
        public bool timeTrial;
        public bool verificationRun;
        public float Elapsed {get;private set;}
        public float Countdown {get;private set;}
        public string message;
        public float messageUntil;
        public bool muted;
        public int selectedColor;
        public int finalPlace;
        public float bestRace;
        public VehicleController Player => racers[0];
        public int Position
        {
            get {if(timeTrial)return 1; int p=1;float score=Player.progress.Score(track);for(int i=1;i<racers.Length;i++)if(racers[i].progress.Score(track)>score)p++;return p;}
        }
        public readonly Color[] PaintColors = {new Color(.96f,.27f,.08f),new Color(.05f,.67f,.72f),new Color(.96f,.78f,.24f),new Color(.76f,.81f,.87f)};

        void Awake()
        {
            Instance=this;Application.targetFrameRate=120;Application.runInBackground=true;
            Time.timeScale=1;Time.fixedDeltaTime=.02f;
            selectedColor=PlayerPrefs.GetInt("CoastRush.Paint",0)%PaintColors.Length;
        }
        void Start(){MainMenu();}
        void Update()
        {
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.mKey.wasPressedThisFrame){muted=!muted;AudioListener.pause=muted||State==RaceState.Paused;}
                if(State==RaceState.MainMenu)
                {
                    if(k.enterKey.wasPressedThisFrame)StartRace(false);
                    else if(k.tKey.wasPressedThisFrame)StartRace(true);
                    else if(k.cKey.wasPressedThisFrame)CycleColor();
                }
                else if(State==RaceState.Finished)
                {if(k.enterKey.wasPressedThisFrame)StartRace(timeTrial);else if(k.escapeKey.wasPressedThisFrame)MainMenu();}
                else
                {
                    if(k.escapeKey.wasPressedThisFrame)TogglePause();
                    if(State==RaceState.Racing && k.rKey.wasPressedThisFrame)Player.Respawn();
                }
            }
            if(State==RaceState.Countdown)
            {
                Countdown-=Time.deltaTime;
                if(Countdown<=0){State=RaceState.Racing;ShowMessage("出发！",1.3f);}
            }
            else if(State==RaceState.Racing)
            {
                Elapsed+=Time.deltaTime;
                foreach(var car in racers)
                {
                    if(!car.gameObject.activeSelf)continue;
                    bool finished=car.progress.Observe(track,car.transform.position,Elapsed,TotalLaps);
                    if(finished&&car.isPlayer)Finish();
                }
            }
        }

        public void StartRace(bool trial)
        {
            Time.timeScale=1;AudioListener.pause=muted;timeTrial=trial;
            Elapsed=0;Countdown=3.4f;State=RaceState.Countdown;message="";
            for(int i=0;i<racers.Length;i++)
            {
                var car=racers[i];car.gameObject.SetActive(i==0||!trial);
                float distance=-12-(i/2)*7;
                float lane=i%2==0?-2.0f:2.0f;
                car.Place(distance,lane,false);car.progress.Reset(track,car.transform.position,distance);
                car.respawns=0;car.overrideControls=false;
            }
            bestRace=PlayerPrefs.GetFloat(trial?"CoastRush.BestTrial":"CoastRush.BestRace",0);
            chaseCamera.Snap();
        }

        public void MainMenu()
        {
            State=RaceState.MainMenu;Time.timeScale=1;AudioListener.pause=muted;Elapsed=0;message="";
            for(int i=0;i<racers.Length;i++)
            {
                racers[i].gameObject.SetActive(true);
                racers[i].Place(-12-(i/2)*7,i%2==0?-2:2,true);
                racers[i].progress.Reset(track,racers[i].transform.position,-12-(i/2)*7);
            }
            Player.Paint(PaintColors[selectedColor]);chaseCamera.Snap();
        }
        public void CycleColor()
        {
            selectedColor=(selectedColor+1)%PaintColors.Length;Player.Paint(PaintColors[selectedColor]);
            if(!verificationRun){PlayerPrefs.SetInt("CoastRush.Paint",selectedColor);PlayerPrefs.Save();}
        }
        public void TogglePause()
        {
            if(State==RaceState.Paused){State=pausedState;Time.timeScale=1;AudioListener.pause=muted;}
            else if(State==RaceState.Racing||State==RaceState.Countdown){pausedState=State;State=RaceState.Paused;Time.timeScale=0;AudioListener.pause=true;}
        }
        void Finish()
        {
            finalPlace=Position;State=RaceState.Finished;
            if(!verificationRun&&(bestRace<=0||Elapsed<bestRace))
            {bestRace=Elapsed;PlayerPrefs.SetFloat(timeTrial?"CoastRush.BestTrial":"CoastRush.BestRace",bestRace);PlayerPrefs.Save();}
            ShowMessage("");
        }
        public void ShowMessage(string text,float seconds=2){message=text;messageUntil=Time.unscaledTime+seconds;}
        public void Quit(){Application.Quit();}
        public static string FormatTime(float t)=>t<=0?"--:--.---":$"{(int)t/60:00}:{t%60:00.000}";
        void OnDestroy(){if(Instance==this)Instance=null;Time.timeScale=1;AudioListener.pause=false;}
    }
}
