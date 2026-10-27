using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace IronBastion
{
    public sealed partial class BattleHud : MonoBehaviour
    {
        readonly Color ink=new(.055f,.10f,.115f,.97f), cream=new(.92f,.91f,.82f), muted=new(.52f,.65f,.64f), gold=new(.95f,.70f,.27f), teal=new(.38f,.84f,.75f);
        TMP_FontAsset font;
        RectTransform canvas, modal;
        TextMeshProUGUI score,stage,lives,remaining,weapon,notice,best,subtitle,title,body,primaryText,soundText,footer;
        Image[] enemyPips=new Image[20];
        Button primary,secondary,pause;
        BattleState previous=(BattleState)(-1);
        BattleGame Game=>BattleGame.Instance;

        void Awake()
        {
            font=Resources.Load<TMP_FontAsset>("Fonts/IB-Regular");
            var go=new GameObject("Command HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas=go.GetComponent<RectTransform>();go.transform.SetParent(transform);
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1600,1000);scaler.matchWidthOrHeight=1;
            if(!FindFirstObjectByType<EventSystem>()) new GameObject("Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var header=Panel("Header",canvas,new(0,1),new(0,1),new(0,0),new(0,86),ink);
            header.anchorMax=new(1,1);header.offsetMax=new(0,0);header.offsetMin=new(0,-86);
            Label(header,"IRON / BASTION",34,gold,28,12,420,43,FontStyles.Bold);
            Label(header,"铁甲堡垒  ·  3D 坦克大战  v"+Application.version,15,muted,30,54,500,24);
            var topRight=Panel("Top right",header,new(1,1),new(1,1),new(-24,-12),new(420,60),Color.clear);
            topRight.pivot=new(1,1);
            stage=Label(topRight,"",21,cream,0,12,190,35,FontStyles.Bold);
            pause=Button(topRight,"暂停  ESC",new(215,7),new(180,42),()=>Game.TogglePause(),false);
            var side=Panel("Tactical panel",canvas,new(1,1),new(1,1),new(-24,-110),new(258,740),ink);side.pivot=new(1,1);
            Label(side,"作战终端",15,teal,22,20,200,24);
            Rule(side,22,58,214);
            Label(side,"SCORE / 得分",13,muted,22,78,200,24);
            score=Label(side,"000000",40,cream,20,103,220,58,FontStyles.Bold);
            best=Label(side,"",13,muted,22,166,210,25);
            Rule(side,22,206,214);
            Label(side,"剩余敌军",14,muted,22,227,200,23);
            remaining=Label(side,"12",38,gold,20,252,210,55,FontStyles.Bold);
            for(int i=0;i<20;i++)
            {
                var pip=Panel("Enemy indicator "+i,side,new(0,1),new(0,1),new(23+(i%5)*43,-(318+(i/5)*23)),new(28,12),gold);
                enemyPips[i]=pip.GetComponent<Image>();
            }
            Rule(side,22,426,214);
            Label(side,"我方坦克",14,muted,22,448,200,24);
            lives=Label(side,"",29,cream,22,476,210,42,FontStyles.Bold);
            weapon=Label(side,"",16,teal,22,536,210,58);
            Rule(side,22,610,214);
            Label(side,"防守目标",13,muted,22,629,210,23);
            Label(side,"守住南侧指挥部\n清除本关全部敌军",17,cream,22,662,215,60);
            var bottom=Panel("Footer",canvas,new(0,0),new(0,0),new(0,0),new(0,64),ink);
            bottom.anchorMax=new(1,0);bottom.pivot=Vector2.zero;bottom.offsetMin=Vector2.zero;bottom.offsetMax=new(0,64);
            footer=Label(bottom,"W A S D / 方向键  移动      SPACE / J  射击      ESC  暂停",16,cream,28,17,1080,28);
            var sound=Button(canvas,"",new(0,0),new(240,38),()=>Game.ToggleSound(),false);
            var sr=sound.GetComponent<RectTransform>();sr.anchorMin=sr.anchorMax=new(1,0);sr.pivot=new(1,0);sr.anchoredPosition=new(-25,13);soundText=sound.GetComponentInChildren<TextMeshProUGUI>();
            notice=Label(canvas,"",20,gold,40,104,1050,36,FontStyles.Bold);
            BuildModal();
            BuildLibrary();
        }
        void BuildModal()
        {
            modal=Panel("Mission overlay",canvas,new(.40f,.52f),new(.40f,.52f),Vector2.zero,new(590,570),ink);modal.pivot=new(.5f,.5f);
            Panel("Gold edge",modal,new(0,1),new(0,1),Vector2.zero,new(590,4),gold);
            subtitle=Label(modal,"",14,teal,40,35,510,30);
            title=Label(modal,"",53,cream,36,82,520,80,FontStyles.Bold);
            body=Label(modal,"",21,muted,40,189,510,160);
            primary=Button(modal,"",new(40,388),new(510,65),OnPrimary,true);
            primary.name="PrimaryAction";
            primaryText=primary.GetComponentInChildren<TextMeshProUGUI>();
            secondary=Button(modal,"返回主菜单",new(40,469),new(510,50),()=>{if(Game.State==BattleState.Menu)OpenLibrary();else Game.ReturnToMenu();},false);
            secondary.name="SecondaryAction";
            Label(modal,"独立战役  /  3 个战区  /  守护最后一道防线",13,muted,40,537,510,23);
        }
        void OnPrimary()
        {
            if(LibraryOpen)return;
            if(Game.State==BattleState.StageClear) Game.NextStage();
            else if(Game.State==BattleState.Paused) Game.TogglePause();
            else Game.StartCampaign();
        }
        void Update()
        {
            if(!Game)return;
            score.text=Game.Score.ToString("D6");best.text="BEST  "+Game.Best.ToString("D6");
            stage.text="战区 "+Game.Stage.ToString("D2")+" / 03";
            remaining.text=Game.Remaining.ToString("D2")+" <size=18>/ "+Game.Total+"</size>";
            lives.text="× "+Game.Lives+"  <size=15>剩余生命</size>";
            weapon.text=Game.Player?("火力等级  "+(Game.Player.Upgrade+1)+" / 3"+(Game.Player.IsProtected?"\n无敌保护  "+(Game.Player.ShieldUntil-Time.time).ToString("F1")+" 秒":"")):"准备重新部署…";
            notice.text=Time.unscaledTime<Game.NoticeUntil&&Game.State==BattleState.Playing?Game.Notice:"";
            soundText.text=Game.Muted?"M  声音：关":"M  声音：开";
            for(int i=0;i<20;i++) {enemyPips[i].gameObject.SetActive(i<Game.Total);enemyPips[i].color=i<Game.Remaining?gold:new Color(.18f,.26f,.26f);}
            if(previous==Game.State)return;
            previous=Game.State;
            if(Game.State!=BattleState.Menu&&LibraryOpen)CloseLibrary();
            modal.gameObject.SetActive(Game.State!=BattleState.Playing);
            pause.interactable=Game.State==BattleState.Playing||Game.State==BattleState.Paused;
            secondary.gameObject.SetActive(true);
            secondary.GetComponentInChildren<TextMeshProUGUI>().text=Game.State==BattleState.Menu?"资料库    I":"返回主菜单";
            footer.text=Game.State==BattleState.Menu?"ENTER  开始作战      I  资料库      M  声音开关":"W A S D / 方向键  移动      SPACE / J  射击      ESC  暂停";
            switch(Game.State)
            {
                case BattleState.Menu:
                    subtitle.text="SINGLE PLAYER  /  单人战役";title.text="铁甲堡垒";
                    body.text="熟悉的防线，全新的立体战场。\n\n击破砖墙，截击敌军。\n你身后，是必须守住的指挥部。";
                    primaryText.text="开始作战     →";break;
                case BattleState.Paused:
                    subtitle.text="TACTICAL PAUSE";title.text="战斗已暂停";
                    body.text="WASD / 方向键：四方向移动\nSPACE / J：朝车头方向射击\nM：声音开关\nR：重新开始整场战役";
                    primaryText.text="继续作战    ENTER";break;
                case BattleState.StageClear:
                    subtitle.text="SECTOR SECURED";title.text="战区已肃清";
                    body.text="指挥部安全。\n\n本关击毁 "+Game.Kills+" 辆敌军坦克。\n生命与火力升级将带入下一战区。";
                    primaryText.text="进入下一关    →";break;
                case BattleState.Victory:
                    subtitle.text="MISSION ACCOMPLISHED";title.text="防线屹立";
                    body.text=Game.ResultReason+"\n\n最终得分  "+Game.Score.ToString("D6")+"\n你守住了全部三个战区。";
                    primaryText.text="再次出击    ENTER";break;
                case BattleState.GameOver:
                    subtitle.text="MISSION FAILED";title.text="防线失守";
                    body.text=Game.ResultReason+"\n\n最终得分  "+Game.Score.ToString("D6")+"\n利用掩体与补给，再组织一次防守。";
                    primaryText.text="重新出击    ENTER";break;
            }
        }
        void Rule(RectTransform parent,float x,float y,float w)=>Panel("Divider",parent,new(0,1),new(0,1),new(x,-y),new(w,1),new Color(.23f,.33f,.33f));
        RectTransform Panel(string name,Transform parent,Vector2 amin,Vector2 amax,Vector2 pos,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=amin;r.anchorMax=amax;r.pivot=new(0,1);r.anchoredPosition=pos;r.sizeDelta=size;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return r;
        }
        TextMeshProUGUI Label(Transform parent,string value,int size,Color color,float x,float y,float w,float h,FontStyles style=FontStyles.Normal)
        {
            var go=new GameObject("Text",typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new(0,1);r.pivot=new(0,1);r.anchoredPosition=new(x,-y);r.sizeDelta=new(w,h);
            var text=go.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.fontStyle=style;text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;return text;
        }
        Button Button(Transform parent,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,bool accent)
        {
            var r=Panel(text,parent,new(0,1),new(0,1),new(pos.x,-pos.y),size,accent?gold:new Color(.12f,.21f,.23f));
            r.GetComponent<Image>().raycastTarget=true;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.onClick.AddListener(action);
            var colors=b.colors;colors.highlightedColor=new Color(1.15f,1.15f,1.1f);colors.pressedColor=new Color(.7f,.8f,.8f);b.colors=colors;
            var l=Label(r,text,accent?23:16,accent?ink:cream,0,0,size.x,size.y,accent?FontStyles.Bold:FontStyles.Normal);l.alignment=TextAlignmentOptions.Center;
            return b;
        }
    }
}
