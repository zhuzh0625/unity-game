using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CoastRush
{
    public sealed class RaceHUD : MonoBehaviour
    {
        public TMP_FontAsset font;
        public GameObject menu,racing,pause,results;
        public TextMeshProUGUI speed,position,lap,timer,best,countdown,message,resultTitle,resultStats,modeLabel;
        public Image speedFill;
        public Button raceButton,trialButton,colorButton,quitButton,resumeButton,restartButton,menuButton,againButton,backButton;
        static readonly Color Ink=new Color(.018f,.055f,.073f,.90f),Muted=new Color(.64f,.75f,.76f),Accent=new Color(1,.36f,.12f),Paper=new Color(.96f,.97f,.9f);
        RaceState previous=(RaceState)(-1);

        public void Construct(TMP_FontAsset source,CoastTrack track)
        {
            font=source;
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            menu=Full("MainMenu");racing=Full("RaceHUD");pause=Full("Pause");results=Full("Results");
            var panel=Panel(menu.transform,"MenuPanel",new Vector2(0,0),new Vector2(715,1080),Ink,Vector2.zero);
            Panel(panel,"Accent",new Vector2(72,973),new Vector2(72,5),Accent,Vector2.zero);
            Label(panel,"Kicker","日光 · 海风 · 全速前行",new Vector2(72,920),new Vector2(570,40),22,Muted);
            Label(panel,"Wordmark","COAST\nRUSH",new Vector2(65,700),new Vector2(610,228),100,Paper,FontStyles.Bold);
            Label(panel,"ChineseTitle","海 岸 疾 驰",new Vector2(72,638),new Vector2(570,66),45,Paper,FontStyles.Bold);
            Label(panel,"Description","穿过海滨小镇，追逐下一个弯道。",new Vector2(72,576),new Vector2(570,42),23,Muted);
            raceButton=ButtonAt(panel,"Race","开始竞速     ENTER",new Vector2(72,454),new Vector2(565,76),Accent,Ink);
            trialButton=ButtonAt(panel,"TimeTrial","单人计时     T",new Vector2(72,360),new Vector2(565,76),new Color(.10f,.20f,.23f),Paper);
            colorButton=ButtonAt(panel,"Paint","切换车身配色     C",new Vector2(72,281),new Vector2(565,54),new Color(.07f,.15f,.18f),Muted);
            Label(panel,"Course","01  /  日光海岸环线",new Vector2(72,206),new Vector2(570,40),24,Paper,FontStyles.Bold);
            Label(panel,"CourseFacts",$"{track.length/1000:0.00} KM    ·    3 圈    ·    4 辆赛车",new Vector2(72,164),new Vector2(570,36),21,Muted);
            Label(panel,"Controls","WASD / 方向键 驾驶     空格 手刹\nR 复位     ESC 暂停     M 静音",new Vector2(72,70),new Vector2(570,76),20,Muted);
            quitButton=ButtonAt(panel,"Quit","退出",new Vector2(560,22),new Vector2(78,36),Color.clear,Muted);
            Label(menu.transform,"Version","COAST GT  /  WINDOWS EDITION  /  v1.0.0",new Vector2(-48,30),new Vector2(670,35),18,Paper,FontStyles.Normal,new Vector2(1,0),TextAlignmentOptions.Right);

            var top=Panel(racing.transform,"Header",new Vector2(42,-34),new Vector2(530,106),Ink,new Vector2(0,1));
            Panel(top,"Accent",new Vector2(0,0),new Vector2(5,106),Accent,Vector2.zero);
            Label(top,"Brand","COAST RUSH",new Vector2(28,53),new Vector2(380,44),30,Paper,FontStyles.Bold);
            modeLabel=Label(top,"Mode","海岸巡游赛",new Vector2(28,18),new Vector2(450,28),18,Muted);
            var stats=Panel(racing.transform,"RaceProgress",new Vector2(0,-34),new Vector2(390,106),Ink,new Vector2(.5f,1));
            Label(stats,"PositionCaption","名次",new Vector2(25,70),new Vector2(120,24),17,Muted);
            position=Label(stats,"Position","1 / 4",new Vector2(25,12),new Vector2(155,55),41,Paper,FontStyles.Bold);
            Label(stats,"LapCaption","圈数",new Vector2(222,70),new Vector2(120,24),17,Muted);
            lap=Label(stats,"Lap","1 / 3",new Vector2(222,12),new Vector2(155,55),41,Paper,FontStyles.Bold);
            var clock=Panel(racing.transform,"Clock",new Vector2(-42,-34),new Vector2(360,106),Ink,new Vector2(1,1));
            timer=Label(clock,"Time","00:00.000",new Vector2(24,49),new Vector2(310,43),35,Paper,FontStyles.Bold);
            best=Label(clock,"Best","最佳圈  --:--.---",new Vector2(24,15),new Vector2(310,29),18,Muted);
            var dash=Panel(racing.transform,"Speedometer",new Vector2(-42,42),new Vector2(310,180),Ink,new Vector2(1,0));
            speed=Label(dash,"Speed","000",new Vector2(23,43),new Vector2(215,115),86,Paper,FontStyles.Bold,Vector2.zero,TextAlignmentOptions.Right);
            Label(dash,"Unit","KM/H",new Vector2(240,59),new Vector2(64,38),16,Muted);
            Panel(dash,"SpeedTrack",new Vector2(28,28),new Vector2(254,6),new Color(.18f,.28f,.30f),Vector2.zero);
            speedFill=Panel(dash,"SpeedFill",new Vector2(28,28),new Vector2(1,6),Accent,Vector2.zero).GetComponent<Image>();
            var mapPanel=Panel(racing.transform,"Minimap",new Vector2(42,42),new Vector2(260,275),Ink,Vector2.zero);
            Label(mapPanel,"MapTitle","日光海岸  /  CIRCUIT 01",new Vector2(16,239),new Vector2(240,27),15,Muted);
            var mapGo=new GameObject("TrackMap",typeof(RectTransform));mapGo.transform.SetParent(mapPanel,false);
            var mapRect=(RectTransform)mapGo.transform;mapRect.anchorMin=mapRect.anchorMax=new Vector2(.5f,.5f);mapRect.sizeDelta=new Vector2(235,230);mapRect.anchoredPosition=new Vector2(0,-13);
            var map=mapGo.AddComponent<RaceMap>();map.track=track;map.raycastTarget=false;
            Label(racing.transform,"KeyboardHints","WASD / 方向键  驾驶       空格  手刹       R  复位       ESC  暂停",new Vector2(0,38),new Vector2(1130,32),18,Paper,FontStyles.Normal,new Vector2(.5f,0),TextAlignmentOptions.Center);
            countdown=Label(racing.transform,"Countdown","",new Vector2(0,0),new Vector2(900,230),140,Paper,FontStyles.Bold,new Vector2(.5f,.5f),TextAlignmentOptions.Center);
            message=Label(racing.transform,"Message","",new Vector2(0,180),new Vector2(980,80),33,Paper,FontStyles.Bold,new Vector2(.5f,0),TextAlignmentOptions.Center);

            Panel(pause.transform,"Dim",Vector2.zero,new Vector2(1920,1080),new Color(0,.025f,.04f,.68f),Vector2.zero);
            var pauseBox=Panel(pause.transform,"PausePanel",Vector2.zero,new Vector2(650,590),Ink,new Vector2(.5f,.5f));
            Label(pauseBox,"Title","暂停休息",new Vector2(60,444),new Vector2(530,80),48,Paper,FontStyles.Bold);
            Label(pauseBox,"Subtitle","海风会等你，比赛也会。",new Vector2(60,394),new Vector2(530,42),22,Muted);
            resumeButton=ButtonAt(pauseBox,"Resume","继续比赛    ESC",new Vector2(60,279),new Vector2(530,72),Accent,Ink);
            restartButton=ButtonAt(pauseBox,"Restart","重新开始",new Vector2(60,187),new Vector2(530,72),new Color(.1f,.2f,.23f),Paper);
            menuButton=ButtonAt(pauseBox,"Menu","返回主菜单",new Vector2(60,95),new Vector2(530,72),new Color(.1f,.2f,.23f),Paper);
            Label(pauseBox,"KeyboardNavigation","↑↓ 选择   ·   Enter 确认   ·   Esc 继续",new Vector2(60,32),new Vector2(530,32),20,Muted);
            var choices=new[]{resumeButton,restartButton,menuButton};
            for(int i=0;i<choices.Length;i++)
            {
                var navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=choices[(i+2)%3],selectOnDown=choices[(i+1)%3]};
                choices[i].navigation=navigation;
            }

            Panel(results.transform,"Dim",Vector2.zero,new Vector2(1920,1080),new Color(0,.025f,.04f,.66f),Vector2.zero);
            var finish=Panel(results.transform,"FinishPanel",Vector2.zero,new Vector2(840,660),Ink,new Vector2(.5f,.5f));
            Label(finish,"Tag","FINISH LINE  /  日光海岸",new Vector2(65,584),new Vector2(710,40),20,Accent);
            resultTitle=Label(finish,"Title","冲线！",new Vector2(65,492),new Vector2(710,90),58,Paper,FontStyles.Bold);
            resultStats=Label(finish,"Stats","",new Vector2(65,238),new Vector2(710,240),27,Paper);
            againButton=ButtonAt(finish,"Again","再来一场    ENTER",new Vector2(65,130),new Vector2(710,72),Accent,Ink);
            backButton=ButtonAt(finish,"Back","返回主菜单    ESC",new Vector2(65,52),new Vector2(710,56),new Color(.1f,.2f,.23f),Muted);
            racing.SetActive(false);pause.SetActive(false);results.SetActive(false);
        }

        void Awake()
        {
            raceButton.onClick.AddListener(()=>RaceDirector.Instance.StartRace(false));trialButton.onClick.AddListener(()=>RaceDirector.Instance.StartRace(true));
            colorButton.onClick.AddListener(()=>RaceDirector.Instance.CycleColor());quitButton.onClick.AddListener(()=>RaceDirector.Instance.Quit());
            resumeButton.onClick.AddListener(()=>RaceDirector.Instance.TogglePause());restartButton.onClick.AddListener(()=>RaceDirector.Instance.StartRace(RaceDirector.Instance.timeTrial));
            menuButton.onClick.AddListener(()=>RaceDirector.Instance.MainMenu());againButton.onClick.AddListener(()=>RaceDirector.Instance.StartRace(RaceDirector.Instance.timeTrial));backButton.onClick.AddListener(()=>RaceDirector.Instance.MainMenu());
        }
        void Update()
        {
            var r=RaceDirector.Instance;if(!r)return;
            if(previous!=r.State)
            {
                menu.SetActive(r.State==RaceState.MainMenu);racing.SetActive(r.State!=RaceState.MainMenu&&r.State!=RaceState.Finished);
                pause.SetActive(r.State==RaceState.Paused);results.SetActive(r.State==RaceState.Finished);previous=r.State;
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(r.State==RaceState.Paused?resumeButton.gameObject:null);
                if(r.State==RaceState.Finished)
                {
                    resultTitle.text=r.timeTrial?"计时挑战完成":r.finalPlace==1?"冠军，属于你！":$"第 {r.finalPlace} 名 · 顺利完赛";
                    string laps="";for(int i=0;i<r.Player.progress.lapTimes.Count;i++)laps+=$"第 {i+1} 圈     {RaceDirector.FormatTime(r.Player.progress.lapTimes[i])}\n";
                    resultStats.text=$"总用时      {RaceDirector.FormatTime(r.Elapsed)}\n最佳圈      {RaceDirector.FormatTime(r.Player.progress.BestLap)}\n{laps}";
                }
            }
            if(r.State==RaceState.Paused)
            {
                var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                foreach(var button in new[]{resumeButton,restartButton,menuButton})button.GetComponent<Outline>().enabled=selected==button.gameObject;
            }
            if(!racing.activeSelf)return;
            speed.text=Mathf.RoundToInt(r.Player.SpeedKph).ToString("000");position.text=r.timeTrial?"—":$"{r.Position} / 4";
            lap.text=$"{Mathf.Min(r.Player.progress.completedLaps+1,3)} / 3";timer.text=RaceDirector.FormatTime(Mathf.Max(.001f,r.Elapsed));
            best.text="最佳圈  "+RaceDirector.FormatTime(r.Player.progress.BestLap);modeLabel.text=r.timeTrial?"单人计时 · 刷新你的最佳成绩":"海岸巡游赛 · 三圈竞速";
            countdown.text=r.State==RaceState.Countdown?Mathf.CeilToInt(r.Countdown).ToString():"";
            if(r.State==RaceState.Racing && r.Elapsed<1.1f)countdown.text="GO!";
            message.text=Time.unscaledTime<r.messageUntil?r.message:"";
            if(r.State==RaceState.Racing && r.Player.ForwardSpeed>4 && Vector3.Dot(r.Player.transform.forward,r.track.Forward(r.Player.progress.distance))<-.35f)message.text="方向错误，请掉头";
            speedFill.rectTransform.sizeDelta=new Vector2(254*Mathf.Clamp01(r.Player.SpeedKph/190),6);
        }

        GameObject Full(string name)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(transform,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;return go;
        }
        static RectTransform Place(GameObject go,Transform parent,Vector2 pos,Vector2 size,Vector2 anchor)
        {
            go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=anchor;rt.pivot=anchor;rt.anchoredPosition=pos;rt.sizeDelta=size;return rt;
        }
        static RectTransform Panel(Transform parent,string name,Vector2 pos,Vector2 size,Color color,Vector2 anchor)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rt=Place(go,parent,pos,size,anchor);var img=go.GetComponent<Image>();img.color=color;img.raycastTarget=false;return rt;
        }
        TextMeshProUGUI Label(Transform parent,string name,string text,Vector2 pos,Vector2 size,float fontSize,Color color,FontStyles style=FontStyles.Normal,Vector2 anchor=default,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
        {
            var go=new GameObject(name,typeof(RectTransform));Place(go,parent,pos,size,anchor);var label=go.AddComponent<TextMeshProUGUI>();
            label.font=font;label.text=text;label.fontSize=fontSize;label.color=color;label.fontStyle=style;label.alignment=alignment;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.NoWrap;return label;
        }
        Button ButtonAt(Transform parent,string name,string caption,Vector2 pos,Vector2 size,Color background,Color foreground)
        {
            var rt=Panel(parent,name,pos,size,background,Vector2.zero);var img=rt.GetComponent<Image>();img.raycastTarget=true;
            var button=rt.gameObject.AddComponent<Button>();var colors=button.colors;colors.highlightedColor=new Color(1,1,1,.88f);colors.pressedColor=new Color(.7f,.8f,.8f);button.colors=colors;
            var outline=rt.gameObject.AddComponent<Outline>();outline.effectColor=Accent;outline.effectDistance=new Vector2(3,-3);outline.useGraphicAlpha=false;outline.enabled=false;
            var label=Label(rt,"Caption",caption,new Vector2(24,0),new Vector2(size.x-48,size.y),25,foreground,FontStyles.Bold);
            return button;
        }
    }
}
