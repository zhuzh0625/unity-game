using System.Collections.Generic;
namespace Emberfall
{
    // Translation is presentation-only; saved identifiers and the version-1 schema are preserved.
    public static class Chinese
    {
        static readonly Dictionary<string,string> lines=new Dictionary<string,string>
        {
            {"Welcome, wanderer. E at the eastern gate to begin.","旅人，欢迎来到余烬营地。前往东侧石门，按 E 出发。"},
            {"Contract complete. E at the fire to prepare another expedition.","委托已完成。靠近篝火按 E 休整，准备下一次远征。"},
            {"E by the fire: heal & refill. East gate: wilderness [E].","靠近篝火按 E 恢复生命、补充血瓶；东侧石门通往灰烬荒野。"},
            {"ASHEN ROAD / Follow the eastern trail to the crypt. J to strike; E to collect.","灰烬荒野：沿道路向东寻找古墓。按 J 挥剑，靠近战利品按 E 拾取。"},
            {"HOLLOW CRYPT / The Warden waits in the eastern chamber. Avoid the red warning circles.","幽寂古墓：守墓者盘踞在东侧墓室。避开红色预警区域。"},
            {"Not enough focus.","专注不足，稍候片刻再施放技能。"},
            {"WARDEN DEFEATED / Collect the relic [E], then return to camp through the western gate.","守墓者已被击败！按 E 拾取战利品，再从西侧出口返回营地。"},
            {"You fell. Enter to return to camp (lose 10 gold).","你倒下了。按 Enter 返回营地，损失最多 10 枚金币。"},
            {"Restored 55 health.","饮下血瓶，恢复了 55 点生命。"},
            {"Pack full. B to equip or discard gear.","背包已满。按 B 打开背包，换装或丢下装备后再拾取。"},
            {"Rested. Potions refilled. Ready for the eastern gate.","休整完毕，生命与血瓶已补满。向东出发吧。"},
            {"Move near the campfire or eastern gate and press E.","请靠近篝火或东侧石门，再按 E 交互。"},
            {"Nothing nearby. E collects loot or uses a gateway.","附近没有可交互物品。靠近战利品或入口后按 E。"},
            {"Saved. Continue resumes safely at camp.","进度已保存。下次继续游戏时将回到安全的营地。"},
            {"[E] REST / refill potions","[E] 篝火休整 · 恢复生命与血瓶"},
            {"[E] LEAVE CAMP","[E] 离开营地"},{"[E] RETURN","[E] 返回上一区域"},{"[E] ENTER CRYPT","[E] 进入幽寂古墓"}
        };
        public static string ItemName(string name)
        {
            if(name=="Camp Sword")return "营地长剑";
            if(name=="Travel Coat")return "旅人皮甲";
            if(name=="Verification Sword")return "测试长剑";
            return (name??"").Replace("Emberbound","余烬淬炼").Replace("Tempered","精制").Replace("Worn","磨损").Replace(" Sword","长剑").Replace(" Coat","皮甲").Replace("Sword","长剑").Replace("Coat","皮甲").Trim();
        }
        public static string Enemy(string name)
        {
            switch(name){case "Ravager":return "腐化劫掠者";case "Bone Archer":return "骸骨弓手";case "Mire Hexer":return "沼泽咒师";case "The Warden":return "幽寂守墓者";default:return name;}
        }
        public static string Text(string value)
        {
            if(string.IsNullOrEmpty(value))return "";
            if(lines.TryGetValue(value,out var translated))return translated;
            if(value.StartsWith("Collected "))return "拾取了 "+ItemName(value.Substring(10));
            if(value.StartsWith("Equipped "))return "已装备 "+ItemName(value.Substring(9));
            if(value.StartsWith("Dropped "))return "已丢下 "+ItemName(value.Substring(8));
            if(value.StartsWith("Save failed:"))return "存档失败，请检查存储空间和目录权限。";
            return value;
        }
    }
}
