using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CloudSprout
{
    public sealed class GameHud:MonoBehaviour
    {
        public TMP_FontAsset font;
        public Material sparkMaterial;
        Canvas canvas;
        TextMeshProUGUI coins,health,zone,time,seeds,toast,title,description,eyebrow,primaryText;
        RectTransform progress;
        GameObject modal,secondary,quit,controls;
        Button primary;
        GameState last=(GameState)(-1);
        Color ink=new Color(.065f,.15f,.18f),cream=new Color(1,.96f,.83f),gold=new Color(1,.73f,.22f);
        Sprite rounded;
        void Awake(){Build();}
        Sprite RoundSprite()
        {
            var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);tex.filterMode=FilterMode.Bilinear;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=Mathf.Max(Mathf.Abs(x-31.5f)-19.5f,0),dy=Mathf.Max(Mathf.Abs(y-31.5f)-19.5f,0);tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(12-Mathf.Sqrt(dx*dx+dy*dy))));}tex.Apply();
            return Sprite.Create(tex,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
        }
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.anchoredPosition=pos;rt.sizeDelta=size;
            var img=go.GetComponent<Image>();img.color=color;img.sprite=rounded;img.type=Image.Type.Sliced;img.raycastTarget=false;return rt;
        }
        TextMeshProUGUI Label(string name,Transform parent,string text,Vector2 pos,Vector2 size,float fontSize,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=pos;rt.sizeDelta=size;
            var tmp=go.AddComponent<TextMeshProUGUI>();tmp.font=font;tmp.text=text;tmp.fontSize=fontSize;tmp.color=color;tmp.alignment=align;tmp.raycastTarget=false;tmp.textWrappingMode=TextWrappingModes.Normal;return tmp;
        }
        Button Button(string name,Transform parent,string text,Vector2 pos,Vector2 size,Color color,UnityEngine.Events.UnityAction action)
        {
            var rt=Box(name,parent,new Vector2(0,1),new Vector2(0,1),pos,size,color);var img=rt.GetComponent<Image>();img.raycastTarget=true;var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=img;var colors=b.colors;colors.highlightedColor=new Color(1,.93f,.73f);colors.pressedColor=new Color(.78f,.82f,.68f);b.colors=colors;b.onClick.AddListener(action);
            Label("Label",rt,text,Vector2.zero,size,21,ink,TextAlignmentOptions.Center);return b;
        }
        void Build()
        {
            rounded=RoundSprite();var go=new GameObject("Interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            if(!FindFirstObjectByType<EventSystem>()){var e=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));e.transform.SetParent(transform);}
            var brand=Box("Brand",go.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(30,-28),new Vector2(370,92),ink);
            Label("Logo",brand,"CLOUDSPROUT",new Vector2(20,-10),new Vector2(330,35),25,cream).fontStyle=FontStyles.Bold;
            zone=Label("Zone",brand,"",new Vector2(21,-49),new Vector2(340,24),14,gold);zone.characterSpacing=3;
            var stat=Box("Counters",go.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-30,-28),new Vector2(520,92),ink);
            coins=Label("Coins",stat,"",new Vector2(20,-10),new Vector2(210,33),24,gold);
            seeds=Label("Seeds",stat,"",new Vector2(255,-10),new Vector2(240,33),24,cream);
            health=Label("Health",stat,"",new Vector2(21,-53),new Vector2(230,23),15,cream);
            time=Label("Time",stat,"",new Vector2(255,-53),new Vector2(230,23),15,cream);
            var pause=Button("Pause",go.transform,"II",new Vector2(0,0),new Vector2(48,40),gold,()=>GameSession.Instance.TogglePause());var pr=pause.GetComponent<RectTransform>();pr.anchorMin=pr.anchorMax=new Vector2(1,1);pr.pivot=new Vector2(1,1);pr.anchoredPosition=new Vector2(-32,-134);
            var bar=Box("ProgressTrack",go.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,28),new Vector2(620,6),new Color(.06f,.15f,.17f,.4f));
            progress=Box("Progress",bar,new Vector2(0,.5f),new Vector2(0,.5f),Vector2.zero,new Vector2(0,6),gold);
            var hint=Box("Controls",go.transform,new Vector2(0,0),new Vector2(0,0),new Vector2(30,25),new Vector2(360,47),new Color(.065f,.15f,.18f,.9f));controls=hint.gameObject;
            Label("Hint",hint,"A / D  MOVE     SPACE  JUMP     SHIFT  RUN",new Vector2(15,-3),new Vector2(340,40),13,cream);
            var toastRoot=Box("Toast",go.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-145),new Vector2(720,50),new Color(0,0,0,0));toast=Label("ToastText",toastRoot,"",Vector2.zero,new Vector2(720,50),21,ink,TextAlignmentOptions.Center);toast.fontStyle=FontStyles.Bold;
            var backdrop=Box("ModalShade",go.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1600,1600),new Color(.025f,.10f,.13f,.35f));modal=backdrop.gameObject;
            var panel=Box("Card",backdrop,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-310,0),new Vector2(590,600),new Color(.05f,.13f,.15f,.97f));
            eyebrow=Label("Eyebrow",panel,"A LITTLE ADVENTURE",new Vector2(42,-36),new Vector2(500,34),15,gold);eyebrow.characterSpacing=5;
            title=Label("Title",panel,"CLOUD\nSPROUT",new Vector2(38,-85),new Vector2(520,165),65,cream);title.fontStyle=FontStyles.Bold;title.lineSpacing=-12;
            description=Label("Description",panel,"Follow the golden trail across the meadow,\nride the wind, and find the ancient sun gate.\n\n3 hidden sun seeds. One brave little explorer.",new Vector2(43,-273),new Vector2(505,145),20,new Color(.75f,.84f,.76f));
            primary=Button("Begin",panel,"LET'S GO   >",new Vector2(42,-445),new Vector2(505,62),gold,()=>{var g=GameSession.Instance;if(g.State==GameState.Menu)g.Begin();else if(g.State==GameState.Paused)g.TogglePause();else g.Restart();});primaryText=primary.GetComponentInChildren<TextMeshProUGUI>();
            secondary=Button("Restart",panel,"RESTART",new Vector2(42,-523),new Vector2(240,45),cream,()=>GameSession.Instance.Restart()).gameObject;
            quit=Button("Quit",panel,"QUIT",new Vector2(307,-523),new Vector2(240,45),cream,()=>GameSession.Instance.Quit()).gameObject;
        }
        public void ShowToast(string value){toast.text=value;}
        public void Refresh()
        {
            var g=GameSession.Instance;if(!g||!coins)return;
            coins.text="COINS  "+g.Coins.ToString("000");seeds.text="SUN SEEDS  "+g.Seeds+" / 3";health.text="HEARTS  "+g.Health+" / 3";
            time.text="TIME  "+((int)g.Elapsed/60).ToString("00")+":"+((int)g.Elapsed%60).ToString("00");zone.text=g.Zone;
            progress.sizeDelta=new Vector2(Mathf.Clamp01(g.player.transform.position.x/282)*620,6);
            if(g.State==last)return;last=g.State;
            bool shown=last==GameState.Menu||last==GameState.Paused||last==GameState.Won;modal.SetActive(shown);controls.SetActive(!shown);
            secondary.SetActive(last==GameState.Paused);quit.SetActive(shown);
            if(last==GameState.Menu){eyebrow.text="A LITTLE ADVENTURE";primaryText.text="LET'S GO   >";}
            if(last==GameState.Paused){eyebrow.text="TAKE A BREATHER";title.text="TRAIL\nPAUSED";description.text="Your adventure is right here waiting.\n\nSPACE  Jump      SHIFT  Run\nESC  Resume       M  Toggle sound";primaryText.text="KEEP EXPLORING   >";}
            if(last==GameState.Won){eyebrow.text="THE SUN GATE IS OPEN";title.text="YOU MADE\nIT HOME!";description.text=$"Coins collected    {g.Coins} / {g.TotalCoins}\nSun seeds             {g.Seeds} / 3\nTime                       {(int)g.Elapsed/60:00}:{(int)g.Elapsed%60:00}\nFalls & retries        {g.Deaths}";primaryText.text="PLAY AGAIN   >";}
        }
    }
}
