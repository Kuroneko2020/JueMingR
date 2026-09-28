using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface ICombatControls
    {
        bool CanConfigure(int feature);
        bool IsEnabled(int feature);
        void Set(int feature,bool enabled);
        int SwitchInterval {get;}
        void Interval(int value);
        string Unavailable(int feature);
    }
    internal sealed class CombatControls
    {
        internal static readonly string[] Names={"自动连点","链球连击","光剑快切","完美左轮","省力魔法绳","自动转向","自动汇报","哥布林必死"};
        internal static readonly string[] Actions={"combat.auto-click","combat.flail","combat.quick-switch","combat.revolver","combat.magic-string","combat.facing","combat.report","combat.goblin"};
        private static readonly string[] Help={
            "按住使用键，补全原版未提供的连续使用。支持快捷栏和在游戏区域使用的鼠标物品；蓄力武器、工具等不适用。",
            "长按右键连续发射、释放和收回链球，左键使用优先。保留原版碰撞、返回阶段及右键交互。",
            "长按右键，依次使用并切换快捷栏中的释放发射武器。至少需要两格；支持光剑、钥匙剑、颌骨剑、时尚剪刀、燧石和冰川之牙。松开后停留在最后一格。",
            "按住使用键时，协调原版左轮的按下与释放，让原版积累暴击机会；不保证每枪暴击。",
            "具备魔法绳效果时，按住使用键自动释放并再次使用悠悠球。仍需实际装备效果与原版生成条件。",
            "使用武器时自动朝向附近敌人；没有合格目标时朝向光标。实际左右移动输入优先，无需辅助瞄准。",
            "自动请求原版战斗统计，包含有伤害的 Boss 击败、逃离与六类事件。每次输出全部近期记录，最多三条；联机向所有活动玩家广播，建议由一人开启，避免重复。",
            "允许玩家武器和投射物正常击中哥布林工匠。保留原版伤害、碰撞与免疫；不瞬杀，不影响被捆住的哥布林或其它城镇 NPC。"};
        private static readonly F5RowDescription[] descriptions=Descriptions();
        private readonly ICombatControls host;
        internal CombatControls(ICombatControls host){this.host=host;}
        private static F5RowDescription[] Descriptions()
        {var result=new F5RowDescription[8];for(int i=0;i<8;i++)result[i]=new F5RowDescription(Actions[i],Help[i]);return result;}
        internal static void AddRows(List<F5Element> elements,Func<string,float,F5Size> measure,ref float y,int first,int count)
        {
            var rows=new F5RowLayout(elements,measure);
            for(int i=first;i<first+count;i++)
            {
                int feature=i;
                rows.Row(ref y,0,522,Names[i],i==2?new[]{null,"开启","关闭","键"}:new[]{"开启","关闭","键"},
                    label=>label==null?F5Command.CombatInterval:label=="开启"?(F5Command)((int)F5Command.CombatAutoClickOn+feature*2):label=="关闭"?(F5Command)((int)F5Command.CombatAutoClickOn+feature*2+1):F5Command.None,descriptions[i],fieldWidth:174);
                var key=elements[elements.Count-1];elements[elements.Count-1]=new F5Element(key.Kind,key.Rect,key.Text,key.TextSize,key.TextScale,F5Command.None,Actions[i]);
            }
        }
        internal static bool Owns(F5Command command){return command>=F5Command.CombatAutoClickOn && command<=F5Command.CombatGoblinOff;}
        private static int Feature(F5Command command){return ((int)command-(int)F5Command.CombatAutoClickOn)/2;}
        private static bool On(F5Command command){return ((int)command-(int)F5Command.CombatAutoClickOn)%2==0;}
        internal bool Available(F5Command command){return Owns(command) && host.CanConfigure(Feature(command));}
        internal Color? Selected(F5Command command)
        {return Owns(command) && host.IsEnabled(Feature(command))==On(command)?(Color?)(On(command)?Color.LightGreen:Color.IndianRed):null;}
        internal void Execute(F5Command command){if(Available(command))host.Set(Feature(command),On(command));}
        internal string Hint(F5Command command)
        {return command==F5Command.CombatInterval?"额外间隔：0—30 tick。0 不增加等待，仍遵守武器自身释放和使用时机。":Owns(command)?host.Unavailable(Feature(command)):null;}
    }
}
