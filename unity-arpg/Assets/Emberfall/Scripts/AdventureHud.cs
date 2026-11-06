using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
namespace Emberfall
{
    public class AdventureHud:MonoBehaviour
    {
        public const float HeaderHeight=70,FooterHeight=116;
        public Adventure game;
        Canvas canvas;TMP_FontAsset font;
        TMP_Text title,quest,status,notice,context,skills,target,packTitle,packText,selectedName,selectedInfo,equippedText,pauseText;
        GameObject pack,pause,helpPanel;RectTransform selectionBar;
        UnityEngine.UI.Image hp,focus;
        readonly Color ink=new Color(.87f,.86f,.78f),gold=new Color(.92f,.69f,.35f);
        void Start()
        {
            font=Resources.Load<TMP_FontAsset>("Fonts/EmberfallChinese");
            if(!font)throw new System.InvalidOperationException("Chinese font asset missing.");
            canvas=new GameObject("Adventure HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=.5f;
            if(!FindFirstObjectByType<EventSystem>())new GameObject("Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
            Panel("Header",canvas.transform,new Vector2(0,1),new Vector2(1,1),Vector2.zero,new Vector2(0,HeaderHeight),new Color(.035f,.045f,.045f,.98f));
            Panel("Header rule",canvas.transform,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-HeaderHeight+1),new Vector2(0,1),new Color(.5f,.35f,.17f,.8f));
            title=Label("Title",canvas.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-17),new Vector2(500,36),26,gold);
            quest=Label("Objective",canvas.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-18),new Vector2(760,42),17,ink);quest.alignment=TextAlignmentOptions.TopRight;
            target=Label("Target",canvas.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-82),new Vector2(600,40),19,gold);target.alignment=TextAlignmentOptions.Center;
            Panel("Footer",canvas.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,FooterHeight),new Color(.035f,.045f,.045f,.98f));
            Panel("Footer rule",canvas.transform,Vector2.zero,new Vector2(1,0),new Vector2(0,FooterHeight-1),new Vector2(0,1),new Color(.5f,.35f,.17f,.8f));
            hp=Bar("Health",new Vector2(24,92),new Color(.66f,.23f,.18f));focus=Bar("Focus",new Vector2(24,68),new Color(.22f,.46f,.62f));
            status=Label("Stats",canvas.transform,Vector2.zero,Vector2.zero,new Vector2(24,45),new Vector2(440,40),16,ink);status.rectTransform.pivot=new Vector2(0,1);
            skills=Label("Skills",canvas.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,94),new Vector2(900,70),17,ink);skills.rectTransform.pivot=new Vector2(1,1);skills.alignment=TextAlignmentOptions.TopRight;
            notice=Label("Notice",canvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,143),new Vector2(1180,40),18,ink);notice.alignment=TextAlignmentOptions.Center;
            context=Label("Interact",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-135),new Vector2(900,45),21,gold);context.alignment=TextAlignmentOptions.Center;
            BuildInventory();BuildPauseAndHelp();Refresh();
        }
        void BuildInventory()
        {
            pack=Panel("Inventory",canvas.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(1040,600),new Color(.055f,.047f,.045f,.99f));
            packTitle=Label("Pack title",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-18),new Vector2(950,36),27,gold);
            Label("Pack keys",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-61),new Vector2(980,28),16,ink).text="W/S 或方向键选择 · Enter 装备 · Delete 丢下 · B / Esc 返回";
            var list=Panel("Item list",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-100),new Vector2(464,420),new Color(.025f,.033f,.033f,.9f));
            selectionBar=Panel("Selection",list.transform,new Vector2(0,1),new Vector2(1,1),Vector2.zero,new Vector2(0,26),new Color(.48f,.32f,.13f,.55f)).GetComponent<RectTransform>();
            packText=Label("Contents",list.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(10,0),new Vector2(450,420),17,ink);
            Panel("Divider",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(508,-100),new Vector2(1,420),new Color(.4f,.3f,.17f,.65f));
            var detail=Panel("Item detail",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(530,-100),new Vector2(486,420),new Color(.065f,.059f,.05f,.8f));
            selectedName=Label("Selected name",detail.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-10),new Vector2(360,42),24,gold);
            var icon=Panel("Item icon",detail.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-10,-4),new Vector2(70,70),Color.white).GetComponent<UnityEngine.UI.Image>();icon.sprite=WorldArt.Asset(12);icon.preserveAspect=true;
            selectedInfo=Label("Selected comparison",detail.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-65),new Vector2(460,194),19,ink);
            Panel("Equipped rule",detail.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-270),new Vector2(460,1),new Color(.4f,.3f,.17f,.65f));
            equippedText=Label("Equipped gear",detail.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-282),new Vector2(460,128),16,ink);
            Button("装备 [Enter]",pack.transform,new Vector2(-260,-260),()=>game.EquipSelected());
            Button("丢下 [Delete]",pack.transform,new Vector2(0,-260),()=>game.DiscardSelected());
            Button("返回 [B / Esc]",pack.transform,new Vector2(260,-260),()=>game.inventory=false);
        }
        void BuildPauseAndHelp()
        {
            pause=Panel("Pause",canvas.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(780,400),new Color(.035f,.045f,.045f,.99f));
            pauseText=Label("Pause text",pause.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-35),new Vector2(710,230),24,ink);pauseText.alignment=TextAlignmentOptions.Center;
            Button("继续 [Enter]",pause.transform,new Vector2(-150,-140),()=>{if(game.dead)game.Respawn();else game.paused=false;});
            Button("保存 [F5]",pause.transform,new Vector2(150,-140),()=>game.Save());
            helpPanel=Panel("Help",canvas.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(880,570),new Color(.045f,.05f,.045f,.99f));
            Label("Help title",helpPanel.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(32,-24),new Vector2(800,45),28,gold).text="旅途手册 · 纯键盘操作";
            Label("Help controls",helpPanel.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(32,-88),new Vector2(810,350),20,ink).text="WASD / 方向键：移动    左 Shift：奔跑\nJ / 空格：挥剑，可按住连续攻击    E：拾取、石门与篝火交互\nU 顺劈：范围伤害，消耗 22 专注，冷却 3.5 秒\nI 冰霜：范围伤害并减速，消耗 28 专注，冷却 6 秒\nO 余烬：远程弹体，消耗 18 专注，冷却 2 秒\nH：使用血瓶，恢复 55 生命；篝火可恢复生命和血瓶\nB：行囊    W/S：选择    Enter：装备    Delete：丢下\nEsc：暂停或返回    F5：保存    F1：打开或关闭手册\n\n靠近目标即可自动朝向攻击。打开行囊或手册时，战斗暂停。";
            Button("返回 [F1 / Esc]",helpPanel.transform,new Vector2(0,-230),()=>game.help=false);
        }
        UnityEngine.UI.Image Bar(string name,Vector2 pos,Color color)
        {
            var bg=Panel(name,canvas.transform,Vector2.zero,Vector2.zero,pos,new Vector2(340,14),new Color(.12f,.14f,.15f));bg.GetComponent<RectTransform>().pivot=new Vector2(0,1);
            var fill=Panel(name+" Fill",bg.transform,new Vector2(0,.5f),new Vector2(0,.5f),Vector2.zero,new Vector2(340,14),color).GetComponent<UnityEngine.UI.Image>();fill.rectTransform.pivot=new Vector2(0,.5f);return fill;
        }
        GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 pos,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.pivot=(min+max)*.5f;rt.anchoredPosition=pos;rt.sizeDelta=size;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;
            if(name=="Inventory"||name=="Pause"||name=="Help"){var edge=go.AddComponent<UnityEngine.UI.Outline>();edge.effectColor=new Color(.65f,.44f,.2f,.85f);edge.effectDistance=new Vector2(2,-2);}return go;
        }
        TMP_Text Label(string name,Transform parent,Vector2 min,Vector2 max,Vector2 pos,Vector2 size,float fontSize,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.pivot=(min+max)*.5f;rt.anchoredPosition=pos;rt.sizeDelta=size;
            var text=go.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=fontSize;text.color=color;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
        }
        void Button(string text,Transform parent,Vector2 pos,UnityEngine.Events.UnityAction action)
        {
            var go=Panel(text,parent,Vector2.one*.5f,Vector2.one*.5f,pos,new Vector2(240,46),new Color(.18f,.24f,.26f));go.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var b=go.AddComponent<UnityEngine.UI.Button>();b.onClick.AddListener(action);b.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var label=Label("Label",go.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(235,40),19,ink);label.alignment=TextAlignmentOptions.Center;label.text=text;
        }
        string Change(int value)=>value>0?$"<color=#83CFA6>提升 {value}</color>":value<0?$"<color=#E49A87>降低 {-value}</color>":"<color=#A9ADA5>无变化</color>";
        void RefreshInventory()
        {
            var d=game.data;packTitle.text=$"行囊  {d.bag.Count}/16";string rows="<line-height=26px>";
            for(int i=0;i<16;i++)
            {
                if(i<d.bag.Count){string color=i==game.selection?"F5D28A":ColorUtility.ToHtmlStringRGB(Adventure.GearColor(d.bag[i]));rows+=$"<color=#{color}>{(i==game.selection?">":" ")} {i+1:00}   {Chinese.ItemName(d.bag[i].name)}</color>";}
                else rows+=$"<color=#58615B>  {i+1:00}   空位</color>";
                if(i<15)rows+="\n";
            }
            packText.text=rows+"</line-height>";selectionBar.gameObject.SetActive(d.bag.Count>0);selectionBar.anchoredPosition=new Vector2(0,-game.selection*26);
            equippedText.text=$"<color=#E4B468>当前已装备</color>\n{d.weapon.Description}\n{d.armor.Description}\nEnter 换装，原装备会放回行囊。";
            if(d.bag.Count==0){selectedName.text="尚未选中装备";selectedName.color=gold;selectedInfo.text="行囊还没有装备。\n\n击败敌人后，靠近战利品按 E 拾取。\n\n使用 W/S 选择，Enter 换装。";return;}
            var gear=d.bag[Mathf.Clamp(game.selection,0,d.bag.Count-1)];var old=gear.slot==0?d.weapon:d.armor;
            string attribute=gear.slot==0?"攻击":"护甲",slot=gear.slot==0?"长剑":"皮甲",quality=new[]{"普通","精制","稀有"}[Mathf.Clamp(gear.rarity,0,2)];
            selectedName.text=Chinese.ItemName(gear.name);selectedName.color=Adventure.GearColor(gear);
            selectedInfo.text=$"{slot} · {quality}\n\n{attribute}  +{gear.power}\n对比已装备：{Change(gear.power-old.power)}（当前 +{old.power}）\n\n生命  +{gear.vitality}\n对比已装备：{Change(gear.vitality-old.vitality)}（当前 +{old.vitality}）";
        }
        public void Refresh()
        {
            if(!canvas||game.data==null)return;var d=game.data;
            title.text="余烬行者  /  "+new[]{"余烬营地","灰烬荒野","幽寂古墓"}[game.zone];
            quest.text=d.returned?"委托完成 · 在篝火旁休整，准备下一次远征":d.bossDefeated?(game.zone==2?"归途 · 从西侧石门返回灰烬荒野":game.zone==1?"归途 · 从西侧石门返回余烬营地":"守墓者已败 · 前往篝火休整"):new[]{"委托 · 从东侧石门前往灰烬荒野","委托 · 沿古道向东，进入幽寂古墓","委托 · 穿过中间通道，击败幽寂守墓者"}[game.zone];
            hp.rectTransform.sizeDelta=new Vector2(340*Mathf.Clamp01(game.health/d.MaxHealth),14);focus.rectTransform.sizeDelta=new Vector2(340*game.mana/100,14);
            status.text=$"生命 {Mathf.CeilToInt(game.health)}/{d.MaxHealth}   专注 {Mathf.FloorToInt(game.mana)}/100\n攻击 {d.Attack}  护甲 {d.armor.power}  金币 {d.gold}  血瓶 {d.potions} [H]";
            string Cool(int i)=>game.cooldown[i]>.05f?$"{game.cooldown[i]:0.0}秒":"就绪";
            skills.text=$"[J / 空格] 挥剑   [U] 顺劈 {Cool(1)}   [I] 冰霜 {Cool(2)}   [O] 余烬 {Cool(3)}\nWASD 移动 · Shift 奔跑 · E 交互 · B 行囊 · F1 手册 · Esc 暂停";
            notice.text=Chinese.Text(game.message);context.text=Chinese.Text(game.Context());
            Adventure.Foe nearest=null;float best=8;foreach(var f in game.enemies){float distance=Vector2.Distance(game.Position,f.go.transform.position);if(distance<best){nearest=f;best=distance;}}
            target.text=nearest==null?"":Chinese.Enemy(nearest.go.name)+$"   {Mathf.CeilToInt(nearest.hp)} / {nearest.max}";
            pack.SetActive(game.inventory&&!game.help);if(game.inventory&&!game.help)RefreshInventory();helpPanel.SetActive(game.help);
            pause.SetActive((game.paused||game.dead)&&!game.help);
            pauseText.text=game.dead?"你倒下了\n\n按 Enter 返回营地\n保留全部装备，损失最多 10 枚金币。":"稍作休息\n\nEnter / Esc 继续    F5 保存    F1 手册\n保存装备、血瓶与委托进度。\n下次继续时将回到安全的营地。";
        }
    }
}
