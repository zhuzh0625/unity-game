using TMPro;
using System.Text;
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
        UnityEngine.UI.Image itemIcon;
        sealed class SkillSlot { public UnityEngine.UI.Image icon,mask; public TMP_Text text; public int tenths=-1; }
        readonly SkillSlot[] slots=new SkillSlot[5];
        readonly StringBuilder bagRows=new StringBuilder(2048);
        int lastZone=-1,lastQuest=-1,lastHealth=-1,lastMana=-1,lastGold=-1,lastPotions=-1,lastAttack=-1,lastArmor=-1,lastMax=-1;
        int bagRevision=-1,bagSelection=-1,bagCount=-1,bagGold=-1,lastMode=-1,targetHealth=-1;
        Gear bagWeapon,bagArmor;
        Adventure.Foe shownTarget;
        Adventure.InteractionTarget shownInteraction;
        string lastMessage;
        float nextRefresh;
        public int InventoryRefreshCount { get; private set; }
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
            skills=Label("Skills",canvas.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,8),new Vector2(900,20),14,ink);skills.rectTransform.pivot=new Vector2(1,0);skills.alignment=TextAlignmentOptions.BottomRight;
            skills.text="WASD 移动 · Shift 奔跑 · E 交互 · B 行囊 · F1 手册 · Esc 暂停 · M 静音";
            BuildSkills();
            notice=Label("Notice",canvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,143),new Vector2(1180,40),18,ink);notice.alignment=TextAlignmentOptions.Center;
            context=Label("Interact",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-135),new Vector2(900,45),21,gold);context.alignment=TextAlignmentOptions.Center;
            BuildInventory();BuildPauseAndHelp();Refresh();
        }
        void BuildInventory()
        {
            pack=Panel("Inventory",canvas.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(1040,600),new Color(.055f,.047f,.045f,.99f));
            packTitle=Label("Pack title",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-18),new Vector2(950,36),27,gold);
            pack.AddComponent<Canvas>();
            pack.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Label("Pack keys",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-61),new Vector2(980,28),16,ink).text="W/S 选择 · Enter 装备 · Delete 丢下 · 营地内 V 出售 / R 强化 · B 返回";
            var list=Panel("Item list",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-100),new Vector2(464,420),new Color(.025f,.033f,.033f,.9f));
            selectionBar=Panel("Selection",list.transform,new Vector2(0,1),new Vector2(1,1),Vector2.zero,new Vector2(0,26),new Color(.48f,.32f,.13f,.55f)).GetComponent<RectTransform>();
            packText=Label("Contents",list.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(10,0),new Vector2(450,420),17,ink);
            Panel("Divider",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(508,-100),new Vector2(1,420),new Color(.4f,.3f,.17f,.65f));
            var detail=Panel("Item detail",pack.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(530,-100),new Vector2(486,420),new Color(.065f,.059f,.05f,.8f));
            selectedName=Label("Selected name",detail.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-10),new Vector2(360,42),24,gold);
            itemIcon=Panel("Item icon",detail.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-10,-4),new Vector2(62,62),gold).GetComponent<UnityEngine.UI.Image>();itemIcon.sprite=WorldArt.GearIcon(0);itemIcon.preserveAspect=true;
            selectedInfo=Label("Selected comparison",detail.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-65),new Vector2(460,236),17,ink);
            Panel("Equipped rule",detail.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-308),new Vector2(460,1),new Color(.4f,.3f,.17f,.65f));
            equippedText=Label("Equipped gear",detail.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-320),new Vector2(460,96),14,ink);
            Button("装备 [Enter]",pack.transform,new Vector2(-400,-260),()=>game.EquipSelected(),180);
            Button("丢下 [Delete]",pack.transform,new Vector2(-200,-260),()=>game.DiscardSelected(),180);
            Button("出售 [V]",pack.transform,new Vector2(0,-260),()=>game.SellSelected(),180);
            Button("强化 [R]",pack.transform,new Vector2(200,-260),()=>game.UpgradeSelected(),180);
            Button("返回 [B]",pack.transform,new Vector2(400,-260),()=>game.inventory=false,180);
        }
        void BuildPauseAndHelp()
        {
            pause=Panel("Pause",canvas.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(780,400),new Color(.035f,.045f,.045f,.99f));
            pauseText=Label("Pause text",pause.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-35),new Vector2(710,230),24,ink);pauseText.alignment=TextAlignmentOptions.Center;
            Button("继续 [Enter]",pause.transform,new Vector2(-150,-140),()=>{if(game.dead)game.Respawn();else game.paused=false;});
            Button("保存 [F5]",pause.transform,new Vector2(150,-140),()=>game.Save());
            helpPanel=Panel("Help",canvas.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(880,570),new Color(.045f,.05f,.045f,.99f));
            Label("Help title",helpPanel.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(32,-24),new Vector2(800,45),28,gold).text="旅途手册 · 纯键盘操作";
            Label("Help controls",helpPanel.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(32,-88),new Vector2(810,350),18,ink).text="WASD / 方向键：移动    左 Shift：奔跑    K：闪避，冷却 2.2 秒\nJ / 空格：挥剑，可按住连续攻击    E：拾取、石门与篝火交互\nU 顺劈：22 专注，冷却 3.5 秒    I 冰霜：28 专注，冷却 6 秒\nO 余烬：18 专注，冷却 2 秒    H 血瓶：恢复 55 生命\nB 行囊：W/S 选择，Enter 装备，Delete 丢下\n营地行囊：V 出售，R 强化选中装备；每次主属性与生命 +2，最多三级\n疾速加快挥剑；汲取将伤害转为生命；凝神加速回蓝并缩短技能冷却\nEsc 暂停或返回    F5 保存    F1 手册    M 静音\n\n攻击需要命中动作，墙体阻挡索敌与伤害；K 闪避期间短暂无敌。\n守墓者半血狂怒，注意更快的预警与六向弹体。通关后敌人逐步变强。\n打开行囊或手册会暂停战斗。篝火补给并开启下一次远征。";
            Button("返回 [F1 / Esc]",helpPanel.transform,new Vector2(0,-230),()=>game.help=false);
        }
        UnityEngine.UI.Image Bar(string name,Vector2 pos,Color color)
        {
            var bg=Panel(name,canvas.transform,Vector2.zero,Vector2.zero,pos,new Vector2(340,14),new Color(.12f,.14f,.15f));bg.GetComponent<RectTransform>().pivot=new Vector2(0,1);
            bg.AddComponent<Canvas>();
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
        void Button(string text,Transform parent,Vector2 pos,UnityEngine.Events.UnityAction action,float width=240)
        {
            var go=Panel(text,parent,Vector2.one*.5f,Vector2.one*.5f,pos,new Vector2(width,46),new Color(.18f,.24f,.26f));go.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var b=go.AddComponent<UnityEngine.UI.Button>();b.onClick.AddListener(action);b.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var label=Label("Label",go.transform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(width-5,40),18,ink);label.alignment=TextAlignmentOptions.Center;label.text=text;
        }
        void BuildSkills()
        {
            var root=Panel("Skill cards",canvas.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-24,26),new Vector2(530,76),Color.clear);
            root.AddComponent<Canvas>();
            for(int i=0;i<5;i++)
            {
                var card=Panel("Skill "+i,root.transform,Vector2.zero,Vector2.zero,new Vector2(i*106,0),new Vector2(98,76),new Color(.12f,.15f,.15f));
                var icon=Panel("Icon",card.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,8),new Vector2(36,36),i==2?new Color(.5f,.85f,1):gold).GetComponent<UnityEngine.UI.Image>();icon.sprite=WorldArt.Icon(i);
                var mask=Panel("Cooldown",card.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0,0,0,.72f)).GetComponent<UnityEngine.UI.Image>();
                mask.sprite=WorldArt.Square;mask.type=UnityEngine.UI.Image.Type.Filled;mask.fillMethod=UnityEngine.UI.Image.FillMethod.Vertical;mask.fillAmount=0;
                var text=Label("Cooldown text",card.transform,Vector2.one,Vector2.one,new Vector2(-5,-3),new Vector2(88,22),15,Color.white);text.alignment=TextAlignmentOptions.TopRight;
                var key=Label("Skill name",card.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,4),new Vector2(92,22),15,ink);key.rectTransform.pivot=new Vector2(.5f,0);key.alignment=TextAlignmentOptions.Center;
                key.text=i<4?AdventureRules.Keys[i]+" "+AdventureRules.Names[i]:"K 闪避";
                slots[i]=new SkillSlot{icon=icon,mask=mask,text=text};
            }
        }
        string Change(int value)=>value>0?$"<color=#83CFA6>提升 {value}</color>":value<0?$"<color=#E49A87>降低 {-value}</color>":"<color=#A9ADA5>无变化</color>";
        void RefreshInventory()
        {
            InventoryRefreshCount++;
            var d=game.data;packTitle.text=$"行囊  {d.bag.Count}/16   ·   金币 {d.gold}";bagRows.Clear();bagRows.Append("<line-height=26px>");
            for(int i=0;i<16;i++)
            {
                if(i<d.bag.Count){string color=i==game.selection?"F5D28A":ColorUtility.ToHtmlStringRGB(Adventure.GearColor(d.bag[i]));bagRows.Append($"<color=#{color}>{(i==game.selection?">":" ")} {i+1:00}   {d.bag[i].DisplayName}</color>");}
                else bagRows.Append($"<color=#58615B>  {i+1:00}   空位</color>");
                if(i<15)bagRows.Append('\n');
            }
            packText.text=bagRows.Append("</line-height>").ToString();selectionBar.gameObject.SetActive(d.bag.Count>0);selectionBar.anchoredPosition=new Vector2(0,-game.selection*26);
            equippedText.text=$"<color=#E4B468>当前已装备</color>\n{d.weapon.Description}\n{d.armor.Description}\nEnter 换装，原装备会放回行囊。";
            itemIcon.enabled=d.bag.Count>0;
            if(d.bag.Count==0){selectedName.text="尚未选中装备";selectedName.color=gold;selectedInfo.text="行囊还没有装备。\n\n击败敌人后，靠近战利品按 E 拾取。\n\n使用 W/S 选择，Enter 换装。";return;}
            var gear=d.bag[Mathf.Clamp(game.selection,0,d.bag.Count-1)];var old=gear.slot==0?d.weapon:d.armor;
            string attribute=gear.slot==0?"攻击":"护甲",slot=gear.slot==0?"长剑":"皮甲",quality=new[]{"普通","精制","稀有"}[Mathf.Clamp(gear.rarity,0,2)];
            selectedName.text=gear.DisplayName;selectedName.color=Adventure.GearColor(gear);itemIcon.sprite=WorldArt.GearIcon(gear.slot);itemIcon.color=Adventure.GearColor(gear);
            string trade=game.zone==0?$"出售 {gear.SellValue} 金币 · "+(gear.upgradeLevel>=3?"强化已满级":$"强化 {gear.UpgradeCost} 金币"):"返回营地后可出售或强化";
            selectedInfo.text=$"{slot} · {quality} · 强化 {gear.upgradeLevel}/3\n{attribute}  +{gear.power}\n对比已装备：{Change(gear.power-old.power)}（当前 +{old.power}）\n生命  +{gear.vitality}\n对比已装备：{Change(gear.vitality-old.vitality)}（当前 +{old.vitality}）\n\n{gear.AffixName} · {gear.AffixDescription}\n\n{trade}";
        }
        public void Refresh()
        {
            if(!canvas||game.data==null)return;var d=game.data;
            int mode=(game.inventory?1:0)+(game.paused?2:0)+(game.dead?4:0)+(game.help?8:0);
            bool bagDirty=bagRevision!=game.InventoryRevision||bagSelection!=game.selection||bagCount!=d.bag.Count||bagWeapon!=d.weapon||bagArmor!=d.armor||bagGold!=d.gold;
            bool modeChanged=mode!=lastMode;
            if(!modeChanged&&!bagDirty&&Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.05f;
            int questState=game.zone+(d.returned?10:0)+(d.bossDefeated?20:0)+d.ExpeditionRank*100;
            if(lastZone!=game.zone){lastZone=game.zone;title.text="余烬行者  /  "+AdventureRules.Zones[game.zone];}
            if(lastQuest!=questState)
            {
                lastQuest=questState;
                quest.text=d.returned?"委托完成 · 在篝火旁休整，准备下一次远征":d.bossDefeated?(game.zone==2?"归途 · 从西侧石门返回灰烬荒野":game.zone==1?"归途 · 从西侧石门返回余烬营地":"守墓者已败 · 前往篝火休整"):game.zone==0?"委托 · 从东侧石门前往灰烬荒野":game.zone==1?"委托 · 沿古道向东，进入幽寂古墓":"委托 · 穿过中间通道，击败幽寂守墓者";
                if(d.ExpeditionRank>0)quest.text+=$"  ·  远征 {d.ExpeditionRank+1}";
            }
            hp.rectTransform.sizeDelta=new Vector2(340*Mathf.Clamp01(game.health/d.MaxHealth),14);focus.rectTransform.sizeDelta=new Vector2(340*game.mana/100,14);
            int hpValue=Mathf.CeilToInt(game.health),manaValue=Mathf.FloorToInt(game.mana);
            if(lastHealth!=hpValue||lastMana!=manaValue||lastGold!=d.gold||lastPotions!=d.potions||lastAttack!=d.Attack||lastArmor!=d.armor.power||lastMax!=d.MaxHealth)
            {
                lastHealth=hpValue;lastMana=manaValue;lastGold=d.gold;lastPotions=d.potions;lastAttack=d.Attack;lastArmor=d.armor.power;lastMax=d.MaxHealth;
                status.text=$"生命 {hpValue}/{d.MaxHealth}   专注 {manaValue}/100\n攻击 {d.Attack}  护甲 {d.armor.power}  金币 {d.gold}  血瓶 {d.potions} [H]";
            }
            for(int i=0;i<5;i++)
            {
                float remaining=i<4?game.cooldown[i]:game.DodgeRemaining;
                float total=i==4?AdventureRules.DodgeCooldown:i==0?d.AttackInterval:AdventureRules.Cooldown[i]*d.SkillCooldownFactor;
                bool enough=i==4||game.mana>=AdventureRules.Cost[i];
                int tenths=remaining>.05f?Mathf.CeilToInt(remaining*10):enough?0:-1;
                if(slots[i].tenths!=tenths){slots[i].tenths=tenths;slots[i].text.text=tenths>0?$"{remaining:0.0}":tenths<0?"专注不足":"";}
                slots[i].mask.fillAmount=Mathf.Clamp01(remaining/total);
                slots[i].icon.color=enough?(i==2?new Color(.5f,.85f,1):gold):new Color(.32f,.34f,.34f);
            }
            if(lastMessage!=game.message){lastMessage=game.message;notice.text=Chinese.Text(game.message);}
            var interaction=game.ResolveInteraction();
            if(interaction.kind!=shownInteraction.kind||interaction.drop!=shownInteraction.drop||interaction.destination!=shownInteraction.destination||bagDirty)
            {shownInteraction=interaction;context.text=Chinese.Text(game.Context());}
            var current=game.CurrentTarget;int targetValue=current==null?-1:Mathf.CeilToInt(current.hp);
            if(shownTarget!=current||targetHealth!=targetValue){shownTarget=current;targetHealth=targetValue;target.text=current==null?"":Chinese.Enemy(current.go.name)+$"   {targetValue} / {Mathf.CeilToInt(current.max)}"+(current.enraged?"  ·  狂怒":"");}
            if(modeChanged)
            {
                pack.SetActive(game.inventory&&!game.help);helpPanel.SetActive(game.help);pause.SetActive((game.paused||game.dead)&&!game.help);
                pauseText.text=game.dead?"你倒下了\n\n按 Enter 返回营地\n保留全部装备，损失最多 10 枚金币。":"稍作休息\n\nEnter / Esc 继续    F5 保存    F1 手册\n保存装备、血瓶与委托进度。\n下次继续时将回到安全的营地。";
            }
            if(game.inventory&&!game.help&&(bagDirty||modeChanged))RefreshInventory();
            bagRevision=game.InventoryRevision;bagSelection=game.selection;bagCount=d.bag.Count;bagWeapon=d.weapon;bagArmor=d.armor;bagGold=d.gold;lastMode=mode;
        }
    }
}
