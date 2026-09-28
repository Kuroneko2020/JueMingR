using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface ICombatObservationControls
    {
        ObservationOptions Options {get;}
        bool CanConfigure {get;}
        string Unavailable(int field);
        void Set(int field,bool value);
        void Radius(int value);
    }
    internal sealed class CombatObservationControls
    {
        internal static readonly string[] Names={"碰撞箱显示","NPC寻路预测"};
        internal static readonly string[] Actions={"combat.collision-display","combat.npc-path"};
        private readonly ICombatObservationControls host;
        internal ObservationOptions Options {get{return host.Options;}}
        internal CombatObservationControls(ICombatObservationControls host){this.host=host;}
        internal static void AddRows(List<F5Element> elements,Func<string,float,F5Size> measure,ObservationOptions options,ref float y)
        {
            int panel=elements.Count;float top=y;
            elements.Add(new F5Element(F5ElementKind.Panel,default(F5Rect),null,default(F5Size),0,F5Command.None));
            var title=measure("辅助瞄准设置",.70f);y+=8;
            float row=Math.Max(30,title.Height+10),x=12+title.Width+12;
            string[] labels={options.ClearLine?"清线优先":"最近优先",options.MouseCenter?"鼠标中心":"玩家中心",options.Dummy?"追踪人偶：开":"追踪人偶：关"};
            var commands=new[]{F5Command.ObservationPolicy,F5Command.ObservationCenter,F5Command.ObservationDummy};
            foreach(var labelText in labels)row=Math.Max(row,measure(labelText,.65f).Height+10);
            elements.Add(new F5Element(F5ElementKind.Text,new F5Rect(12,y+(row-title.Height)/2,title.Width,title.Height),"辅助瞄准设置",title,.70f,F5Command.None,
                description:new F5RowDescription("combat.selection","当前设置用于 NPC寻路预测的选敌；无需持有武器或开始攻击。"),hintRect:new F5Rect(10,y,title.Width+4,row)));
            for(int i=0;i<labels.Length;i++)
            {
                var size=measure(labels[i],.65f);float width=Math.Max(64,size.Width+16);
                elements.Add(new F5Element(F5ElementKind.Button,new F5Rect(x,y,width,row),labels[i],size,.65f,commands[i]));x+=width+8;
            }
            // Keep the remaining first-row width available for the later real
            // marker control; no inactive placeholder enters layout or input.
            y+=row+8;
            var label=measure("鼠标半径：50格",.70f);float fieldHeight=Math.Max(30,label.Height+10);
            elements.Add(new F5Element(F5ElementKind.Field,new F5Rect(12,y,498,fieldHeight),null,label,.70f,F5Command.ObservationRadius));y+=fieldHeight+8;
            elements[panel]=new F5Element(F5ElementKind.Panel,new F5Rect(0,top,522,y-top),null,default(F5Size),0,F5Command.None);y+=6;
            var rows=new F5RowLayout(elements,measure);
            for(int i=0;i<2;i++)
            {
                int field=i;rows.Row(ref y,0,522,Names[i],new[]{"开启","关闭","键"},labelText=>labelText=="开启"?(F5Command)((int)F5Command.ObservationCollisionOn+field*2):labelText=="关闭"?(F5Command)((int)F5Command.ObservationCollisionOn+field*2+1):F5Command.None,
                    new F5RowDescription(Actions[i],i==0?"五色表示当前伤害或受击区域；虚线为近似。显示本机观察，不保证实际扣血。":"只显示共享选中敌怪的一条短期可能路线；断点、虚线与末端提示表示瞬移、近似或未知。"));
                var key=elements[elements.Count-1];elements[elements.Count-1]=new F5Element(key.Kind,key.Rect,key.Text,key.TextSize,key.TextScale,key.Command,Actions[i]);
            }
        }
        internal static bool Owns(F5Command c){return c>=F5Command.ObservationPolicy && c<=F5Command.ObservationPathOff;}
        internal bool Available(F5Command c){return Owns(c) && host.CanConfigure;}
        internal Color? Selected(F5Command c)
        {
            var o=host.Options;if(c==F5Command.ObservationDummy)return o.Dummy?Color.LightGreen:Color.IndianRed;
            bool selected=c==F5Command.ObservationPolicy || c==F5Command.ObservationCenter || (c==F5Command.ObservationCollisionOn?o.Collision:c==F5Command.ObservationCollisionOff?!o.Collision:c==F5Command.ObservationPathOn?o.Path:c==F5Command.ObservationPathOff?!o.Path:false);
            if(c==F5Command.ObservationCollisionOn && host.Unavailable(0)!=null || c==F5Command.ObservationPathOn && host.Unavailable(1)!=null)return selected?(Color?)Color.Goldenrod:null;
            return selected?(Color?)(c==F5Command.ObservationCollisionOff || c==F5Command.ObservationPathOff?Color.IndianRed:Color.LightGreen):null;
        }
        internal void Execute(F5Command c)
        {
            if(!Available(c))return;
            if(c==F5Command.ObservationPolicy)host.Set(2,!host.Options.ClearLine);
            else if(c==F5Command.ObservationCenter)host.Set(3,!host.Options.MouseCenter);
            else if(c==F5Command.ObservationDummy)host.Set(4,!host.Options.Dummy);
            else if(c>=F5Command.ObservationCollisionOn)host.Set(((int)c-(int)F5Command.ObservationCollisionOn)/2,((int)c-(int)F5Command.ObservationCollisionOn)%2==0);
        }
        internal string Hint(F5Command c)
        {
            if(!Owns(c))return null;
            if(c>=F5Command.ObservationCollisionOn){var reason=host.Unavailable(((int)c-(int)F5Command.ObservationCollisionOn)/2);if(reason!=null)return reason;}
            if(c==F5Command.ObservationRadius)return host.Options.MouseCenter?"鼠标范围：0—50 格。0 只选择受击区域触及光标的目标；不关闭显示。":"玩家中心使用屏幕范围；切换鼠标中心后可调半径。";
            if(c==F5Command.ObservationPolicy)return host.Options.ClearLine?"点击切换最近优先。当前先选视线通畅的目标；遮挡只降低排序，不代表武器无法命中。":"点击切换清线优先。当前优先选择距离范围中心最近的可攻击目标。";
            if(c==F5Command.ObservationCenter)return "点击切换玩家中心或真实鼠标中心；玩家中心保留屏幕范围。";
            if(c==F5Command.ObservationDummy)return "允许把训练人偶选为路径目标。";
            return host.CanConfigure?null:"设置正在保存或当前暂不可用。";
        }
    }
}
