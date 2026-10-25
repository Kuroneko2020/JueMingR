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
        bool PathAccess {get;}
        string Unavailable(int field);
        void Set(int field,bool value);
        void Radius(int value);
    }
    internal sealed class CombatObservationControls
    {
        internal static readonly string[] Names={"碰撞箱显示","寻路预测"};
        internal static readonly string[] Actions={"combat.collision-display","combat.npc-path"};
        private readonly ICombatObservationControls host;
        internal ObservationOptions Options {get{return host.Options;}}
        internal CombatObservationControls(ICombatObservationControls host){this.host=host;}
        internal static void AddRows(List<F5Element> elements,Func<string,float,F5Size> measure,ObservationOptions options,ref float y)
        {
            var aimRows=new F5RowLayout(elements,measure);
            aimRows.Row(ref y,0,522,"辅助瞄准",new[]{"开启","关闭","键"},label=>label=="开启"?F5Command.ObservationAimOn:label=="关闭"?F5Command.ObservationAimOff:F5Command.None);
            var aimKey=elements[elements.Count-1];elements[elements.Count-1]=new F5Element(aimKey.Kind,aimKey.Rect,aimKey.Text,aimKey.TextSize,aimKey.TextScale,aimKey.Command,"combat.aim");
            if(options.Aim)
            {
            int panel=elements.Count;float top=y;
            elements.Add(new F5Element(F5ElementKind.Panel,default(F5Rect),null,default(F5Size),0,F5Command.None));
            y+=8;float row=30,x=12,titleScale=.70f,buttonScale=.65f;
            string[] labels={options.ClearLine?"清线优先":"最近优先",options.MouseCenter?"鼠标中心":"玩家中心",options.Dummy?"追踪人偶：开":"追踪人偶：关",options.Marker?"目标标记：开":"目标标记：关"};
            var commands=new[]{F5Command.ObservationPolicy,F5Command.ObservationCenter,F5Command.ObservationDummy,F5Command.ObservationMarker};
            var title=measure("辅助瞄准设置",titleScale);var sizes=new F5Size[labels.Length];
            // The actual native font fits heading plus four options using the
            // ordinary button scale/padding/gap. Keep those normal dimensions;
            // neither hide the heading nor shrink controls preemptively.
            for(int i=0;i<labels.Length;i++)sizes[i]=measure(labels[i],buttonScale);
            row=Math.Max(row,title.Height+8);foreach(var size in sizes)row=Math.Max(row,size.Height+10);
            float titleY=y+(row-title.Height)*.5f;
            elements.Add(new F5Element(F5ElementKind.Text,new F5Rect(12,titleY,title.Width,title.Height),"辅助瞄准设置",title,titleScale,F5Command.None));
            x+=title.Width+8;
            for(int i=0;i<labels.Length;i++)
            {
                var size=sizes[i];float width=Math.Max(64,size.Width+16);
                if(x+width>510)throw new InvalidOperationException("Combat selection settings exceed their measured row.");
                elements.Add(new F5Element(F5ElementKind.Button,new F5Rect(x,y,width,row),labels[i],size,buttonScale,commands[i]));x+=width+8;
            }
            y+=row+8;
            var label=measure("鼠标半径：50格",.70f);float fieldHeight=Math.Max(30,label.Height+10);
            elements.Add(new F5Element(F5ElementKind.Field,new F5Rect(12,y,498,fieldHeight),null,label,.70f,F5Command.ObservationRadius));y+=fieldHeight+8;
            elements[panel]=new F5Element(F5ElementKind.Panel,new F5Rect(0,top,522,y-top),null,default(F5Size),0,F5Command.None);y+=6;
            }
            AddDisplayRow(elements,measure,ref y,0);
        }
        internal static void AddHiddenPathRow(List<F5Element> elements,Func<string,float,F5Size> measure,ref float y,bool visible)
        {if(visible)AddDisplayRow(elements,measure,ref y,1);}
        private static void AddDisplayRow(List<F5Element> elements,Func<string,float,F5Size> measure,ref float y,int field)
        {
            // Stable command/field identity is independent of its page position.
            new F5RowLayout(elements,measure).Row(ref y,0,522,Names[field],new[]{"开启","关闭","键"},label=>label=="开启"?(F5Command)((int)F5Command.ObservationCollisionOn+field*2):label=="关闭"?(F5Command)((int)F5Command.ObservationCollisionOn+field*2+1):F5Command.None,
                new F5RowDescription(Actions[field],field==0?"↓":"显示NPC可能的未来路径",field==1?F5NameStyle.Hidden:F5NameStyle.Standard));
            var key=elements[elements.Count-1];elements[elements.Count-1]=new F5Element(key.Kind,key.Rect,key.Text,key.TextSize,key.TextScale,key.Command,Actions[field]);
        }
        internal static bool Owns(F5Command c){return c>=F5Command.ObservationPolicy && c<=F5Command.ObservationAimOff;}
        private static bool Detail(F5Command c){return c>=F5Command.ObservationPolicy && c<=F5Command.ObservationRadius || c==F5Command.ObservationMarker;}
        internal bool Available(F5Command c){return Owns(c) && host.CanConfigure && ((c!=F5Command.ObservationPathOn && c!=F5Command.ObservationPathOff) || host.PathAccess) && (!Detail(c) || host.Options.Aim);}
        internal Color? Selected(F5Command c)
        {
            var o=host.Options;if(c==F5Command.ObservationDummy)return o.Dummy?Color.LightGreen:Color.IndianRed;
            if(c==F5Command.ObservationAimOn)return o.Aim?Color.LightGreen:(Color?)null;
            if(c==F5Command.ObservationAimOff)return !o.Aim?Color.IndianRed:(Color?)null;
            if(c==F5Command.ObservationMarker)return o.Marker?Color.LightGreen:Color.IndianRed;
            bool selected=c==F5Command.ObservationPolicy || c==F5Command.ObservationCenter || (c==F5Command.ObservationCollisionOn?o.Collision:c==F5Command.ObservationCollisionOff?!o.Collision:c==F5Command.ObservationPathOn?o.Path:c==F5Command.ObservationPathOff?!o.Path:false);
            if(c==F5Command.ObservationCollisionOn && host.Unavailable(0)!=null || c==F5Command.ObservationPathOn && host.Unavailable(1)!=null)return selected?(Color?)Color.Goldenrod:null;
            return selected?(Color?)(c==F5Command.ObservationCollisionOff || c==F5Command.ObservationPathOff?Color.IndianRed:Color.LightGreen):null;
        }
        internal void Execute(F5Command c)
        {
            if(!Available(c))return;
            if(c==F5Command.ObservationAimOn || c==F5Command.ObservationAimOff){host.Set(6,c==F5Command.ObservationAimOn);return;}
            if(c==F5Command.ObservationPolicy)host.Set(2,!host.Options.ClearLine);
            else if(c==F5Command.ObservationCenter)host.Set(3,!host.Options.MouseCenter);
            else if(c==F5Command.ObservationDummy)host.Set(4,!host.Options.Dummy);
            else if(c==F5Command.ObservationMarker)host.Set(5,!host.Options.Marker);
            else if(c>=F5Command.ObservationCollisionOn)host.Set(((int)c-(int)F5Command.ObservationCollisionOn)/2,((int)c-(int)F5Command.ObservationCollisionOn)%2==0);
        }
        internal string Hint(F5Command c)
        {
            if(!Owns(c))return null;
            if(c==F5Command.ObservationAimOn || c==F5Command.ObservationAimOff)return host.Unavailable(6);
            // These surfaces intentionally have no hint, including while saving;
            // return before the generic status fallback so an old box is hidden.
            if(c==F5Command.ObservationDummy || c==F5Command.ObservationMarker || c==F5Command.ObservationRadius)return null;
            if(c>=F5Command.ObservationCollisionOn){var reason=host.Unavailable(((int)c-(int)F5Command.ObservationCollisionOn)/2);if(reason!=null)return reason;}
            if(c==F5Command.ObservationPolicy)return host.Options.ClearLine?"考虑攻击可达性，适合复杂地形":"忽略遮挡，瞄准最新的";
            if(c==F5Command.ObservationCenter)return host.Options.MouseCenter?"瞄准距离光标最近的敌怪，下面滑条改半径":"瞄准距离玩家最近的敌怪";
            return host.CanConfigure?null:"设置正在保存或当前暂不可用。";
        }
    }
}
