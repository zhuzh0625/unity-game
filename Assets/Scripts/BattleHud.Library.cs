using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IronBastion
{
    public sealed partial class BattleHud
    {
        RectTransform library;
        RectTransform[] libraryPages=new RectTransform[2];
        UnityEngine.UI.Button[] libraryTabs=new UnityEngine.UI.Button[2];
        int libraryPage;
        public bool LibraryOpen=>library&&library.gameObject.activeSelf;

        void BuildLibrary()
        {
            library=Panel("FieldLibrary",canvas,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.02f,.04f,.05f,.88f));
            library.offsetMin=library.offsetMax=Vector2.zero;
            library.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var sheet=Panel("LibrarySheet",library,new(.5f,.5f),new(.5f,.5f),Vector2.zero,new(1100,820),ink);
            sheet.pivot=new(.5f,.5f);
            Panel("GoldEdge",sheet,new(0,1),new(0,1),Vector2.zero,new(1100,4),gold);
            Label(sheet,"FIELD MANUAL  /  作战资料库",14,teal,32,25,700,25);
            Label(sheet,"知己知彼，守住防线。",34,cream,30,60,760,53,FontStyles.Bold);
            var back=Button(sheet,"返回主菜单   ESC",new(850,40),new(218,48),CloseLibrary,false);
            back.name="LibraryBack";
            libraryTabs[0]=Button(sheet,"01  道具补给",new(32,129),new(510,49),()=>ShowLibraryPage(0),false);
            libraryTabs[1]=Button(sheet,"02  敌军档案",new(558,129),new(510,49),()=>ShowLibraryPage(1),false);
            libraryTabs[0].name="PickupsTab";libraryTabs[1].name="EnemiesTab";
            libraryPages[0]=Panel("PickupEntries",sheet,new(0,1),new(0,1),new(32,-198),new(1036,550),Color.clear);
            libraryPages[1]=Panel("EnemyEntries",sheet,new(0,1),new(0,1),new(32,-198),new(1036,550),Color.clear);

            LibraryCard(libraryPages[0],0,"火力升级","金黄色 / 最高 2 次升级","首次：射速提升，可同时保有 2 发炮弹。\n再次：获得穿甲能力，可以击破钢墙。",new(1,.75f,.2f),false);
            LibraryCard(libraryPages[0],1,"能量护盾","青绿色 / 持续 10 秒","抵挡敌军炮弹，保护期间不会损失坦克。\n可以正常移动和开火。",new(.2f,.9f,.8f),false);
            LibraryCard(libraryPages[0],2,"全屏炸弹","红色 / 立即生效","清除当前场上所有敌军，包括重型坦克。\n尚未出场的敌军仍会继续增援。",new(1,.3f,.2f),false);
            LibraryCard(libraryPages[0],3,"电磁冻结","蓝色 / 持续 8 秒","敌军暂停移动与开火，为防守争取时间。\n已经发射的炮弹仍会飞行。",new(.35f,.65f,1),false);
            LibraryCard(libraryPages[0],4,"生命增援","浅绿色 / 坦克 +1","增加一次坦克部署机会。\n每次复活均获得 "+BattleGame.RespawnProtectionSeconds.ToString("F1")+" 秒无敌保护。",new(.6f,1,.4f),false);
            Label(libraryPages[0],"首关第一辆普通敌军固定掉落火力升级；其余带灯敌军随机掉落补给。\n补给出现在空地，靠近拾取，18 秒后消失；拾取 +50 分。火力跨关保留，阵亡清零。",16,muted,0,490,1036,60);

            LibraryCard(libraryPages[1],0,"普通型","红色 / 耐久 1 / 击毁 +100","标准移动速度，射击冷却约 1.1 秒。\n利用砖墙掩护，从侧面截击。",new(.78f,.28f,.20f),true);
            LibraryCard(libraryPages[1],1,"快速型","青蓝色 / 耐久 1 / 击毁 +200","移动最快，射击冷却约 1.1 秒。\n及时封锁通道，防止其快速接近基地。",new(.40f,.66f,.65f),true);
            LibraryCard(libraryPages[1],2,"速射型","红色 / 耐久 1 / 击毁 +300","标准移动速度，射击冷却约 0.65 秒。\n外观与普通型相同，留意更短的开火间隔。",new(.78f,.28f,.20f),true);
            LibraryCard(libraryPages[1],3,"重型","紫色 / 耐久 3 / 击毁 +400","移动较慢，需要命中 3 次才能击毁。\n持续压制，或用炸弹直接清除。",new(.58f,.49f,.69f),true);
            Label(libraryPages[1],"首关前 4 辆为普通型，最多同时 3 辆；此后逐步引入快速、速射与重型。\n共同战术：向基地推进，玩家进入无遮挡的横向或纵向射击线时停下瞄准。\n不会主动追击或躲弹。带补给灯的敌军被击毁后掉落道具。",17,muted,0,396,1036,110);
            Rule(sheet,32,762,1036);
            Label(sheet,"TAB / ← →  切换分类       ESC / I  返回主菜单",15,muted,32,778,1000,27);
            ShowLibraryPage(0);
            library.gameObject.SetActive(false);
        }

        void LibraryCard(RectTransform parent,int index,string name,string facts,string description,Color color,bool tank)
        {
            var row=Panel(name,parent,new(0,1),new(0,1),new(0,-index*96),new(1036,86),new Color(.095f,.16f,.18f));
            Panel("Stripe",row,new(0,1),new(0,1),Vector2.zero,new(4,86),color);
            var icon=Panel("Symbol",row,new(0,1),new(0,1),new(25,-20),new(60,50),Color.clear);
            if(tank)
            {
                Panel("LeftTrack",icon,new(0,1),new(0,1),new(0,0),new(13,50),muted);
                Panel("RightTrack",icon,new(0,1),new(0,1),new(47,0),new(13,50),muted);
                Panel("Hull",icon,new(0,1),new(0,1),new(15,-9),new(30,36),color);
                Panel("Barrel",icon,new(0,1),new(0,1),new(27,6),new(6,25),color);
            }
            else
            {
                Panel("Crate",icon,new(0,1),new(0,1),new(7,0),new(46,46),color);
                Panel("CrossV",icon,new(0,1),new(0,1),new(26,-8),new(8,30),cream);
                Panel("CrossH",icon,new(0,1),new(0,1),new(15,-19),new(30,8),cream);
            }
            Label(row,name,25,cream,108,10,225,36,FontStyles.Bold);
            Label(row,facts,14,Color.Lerp(color,cream,.35f),108,51,260,24);
            Label(row,description,20,cream,390,13,620,66);
        }

        public void OpenLibrary()
        {
            if(Game.State!=BattleState.Menu)return;
            ShowLibraryPage(0);
            library.gameObject.SetActive(true);modal.gameObject.SetActive(false);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        }
        public void CloseLibrary()
        {
            library.gameObject.SetActive(false);
            modal.gameObject.SetActive(Game.State!=BattleState.Playing);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        }
        public void ShowLibraryPage(int index)
        {
            libraryPage=Mathf.Clamp(index,0,1);
            for(int i=0;i<2;i++)
            {
                libraryPages[i].gameObject.SetActive(i==libraryPage);
                libraryTabs[i].GetComponent<UnityEngine.UI.Image>().color=i==libraryPage?gold:new Color(.12f,.21f,.23f);
                libraryTabs[i].GetComponentInChildren<TextMeshProUGUI>().color=i==libraryPage?ink:cream;
            }
        }
        public bool HandleLibraryInput(Keyboard keyboard)
        {
            if(Game.State!=BattleState.Menu)return false;
            if(LibraryOpen)
            {
                if(keyboard.escapeKey.wasPressedThisFrame||keyboard.iKey.wasPressedThisFrame)CloseLibrary();
                else if(keyboard.tabKey.wasPressedThisFrame||keyboard.leftArrowKey.wasPressedThisFrame||keyboard.rightArrowKey.wasPressedThisFrame)ShowLibraryPage(1-libraryPage);
                return true;
            }
            if(keyboard.iKey.wasPressedThisFrame){OpenLibrary();return true;}
            return false;
        }
    }
}
