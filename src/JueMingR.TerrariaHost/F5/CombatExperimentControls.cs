using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
namespace JueMingR.TerrariaHost.F5
{
    internal interface ICombatExperimentControls
    {
        bool CanConfigure(int field);
        bool IsEnabled(int field);
        void Set(int field,bool enabled);
        string Unavailable(int field);
    }
    internal sealed class CombatExperimentControls
    {
        internal static readonly string[] Actions={"combat.hard-lock","combat.fast-fire","combat.projectile-through-wall"};
        internal static readonly string[] Names={"强锁模式","加快射速","子弹穿墙"};
        private static readonly string[] Help={"引导本地合适的飞行攻击；可独立于辅助瞄准使用", "按海豚枪节拍使用武器，保留原有资源消耗与持续机制", "本地自有攻击忽略地形阻挡；多人仍由原生同步与服务器裁决"};
        private readonly ICombatExperimentControls host;
        internal CombatExperimentControls(ICombatExperimentControls host){this.host=host;}
        internal static void AddRows(List<F5Element> elements,Func<string,float,F5Size> measure,ref float y,bool hidden)
        {
            if(!hidden)return;
            var rows=new F5RowLayout(elements,measure);
            for(int i=0;i<3;i++)
            {
                int field=i;
                rows.Row(ref y,0,522,Names[i],new[]{"开启","关闭","键"},
                    text=>text=="键"?F5Command.None:(F5Command)((int)F5Command.ExperimentLockOn+field*2+(text=="关闭"?1:0)),
                    new F5RowDescription(Actions[i],Help[i],F5NameStyle.Hidden));
                var key=elements[elements.Count-1];elements[elements.Count-1]=new F5Element(key.Kind,key.Rect,key.Text,key.TextSize,key.TextScale,F5Command.None,Actions[i]);
            }
        }
        internal static bool Owns(F5Command command){return command>=F5Command.ExperimentLockOn && command<=F5Command.ExperimentWallOff;}
        private static int Field(F5Command command){return ((int)command-(int)F5Command.ExperimentLockOn)/2;}
        private static bool On(F5Command command){return ((int)command-(int)F5Command.ExperimentLockOn)%2==0;}
        internal bool Available(F5Command command){return Owns(command)&&host.CanConfigure(Field(command));}
        internal Color? Selected(F5Command command){return Owns(command)&&(host.CanConfigure(Field(command)) || host.Unavailable(Field(command))==null)&&host.IsEnabled(Field(command))==On(command)?(Color?)(On(command)?Color.LightGreen:Color.IndianRed):null;}
        internal void Execute(F5Command command){if(Available(command))host.Set(Field(command),On(command));}
        internal string Hint(F5Command command){return Owns(command)?host.Unavailable(Field(command)):null;}
    }
}
