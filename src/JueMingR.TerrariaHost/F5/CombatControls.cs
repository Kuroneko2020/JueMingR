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
            "补全原版不支持连点的物品",
            "长按右键触发连击",
            "按住右键快切快捷栏的光剑",
            "按住左键最大程度发挥左轮威力",
            "装备魔法绳后长按左键实现连点效果",
            "固定方向的武器可以随时转头了",
            "boss战结束后自动汇报",
            "rnm 还钱！！"};
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
