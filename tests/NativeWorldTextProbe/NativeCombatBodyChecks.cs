using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using JueMingR.Features.Combat;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBodyChecks
    {
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));var geometry=Get(host,"Geometry");
            var local=Main.player[0];var remote=Main.player[1];var originalNpc=Main.npc[0];Main.npc[0]=new NPC();bool server=Main.dedServ;Main.dedServ=true;int mode=Main.netMode;Main.netMode=2;try{Mount.Initialize();}finally{Main.netMode=mode;}
            try
            {
                foreach(var n in Main.npc)n.active=false;
                for(int i=0;i<Main.gore.Length;i++)if(Main.gore[i]==null)Main.gore[i]=new Gore();
                Lighting.Mode=Terraria.Graphics.Light.LightMode.Color;
                foreach(int owner in new[]{0,1})
                {
                    foreach(int mount in new[]{10,47,40,41,42,44,45,14,17})foreach(int direction in new[]{-1,1})
                    {
                        var p=Player(owner);p.mount.SetMount(mount,p);p.direction=direction;p.velocity=new Vector2(direction*35,0);Call(geometry,"Clear");p.HorizontalMovement();
                        var expected=p.getRect();if(direction==1)expected.Offset(p.width-1,0);expected.Width=2;expected.Inflate(6,12);
                        Box(geometry,owner,expected,"natural horizontal mount="+mount+" owner="+owner);
                    }
                    foreach(int mount in new[]{3,50,17,-1})
                    {
                        var p=Player(owner);if(mount>=0)p.mount.SetMount(mount,p);else p.isPerformingJump_DownDash=true;p.velocity=new Vector2(2,5);Call(geometry,"Clear");
                        var expected=p.getRect();expected.Offset(0,p.height-1);expected.Height=2;expected.Inflate(12,6);p.JumpMovement();Box(geometry,owner,expected,"natural downward strip mount="+mount);
                        p.velocity.Y=-1;Call(geometry,"Clear");p.JumpMovement();Require(Count(geometry,owner)==0,"ascending player has no stomp region");
                    }
                    foreach(int dash in new[]{2,3,6})foreach(int direction in new[]{-1,1})
                    {
                        var p=Player(owner);p.direction=direction;p.dashType=p.dash=dash;p.dashDelay=-1;p.eocDash=12;p.eocHit=-1;p.velocity=new Vector2(direction*12,2);
                        if(dash==6)p.mount.SetMount(62,p);p.velocity=new Vector2(direction*12,2);Call(geometry,"Clear");
                        var expected=new Rectangle((int)(p.position.X+p.velocity.X*.5-4),(int)(p.position.Y+p.velocity.Y*.5-4),p.width+8,p.height+8);if(dash==6){expected.Width+=60;if(direction<0)expected.X-=60;}
                        p.DashMovement();if(owner==0 || dash==2)Box(geometry,owner,expected,"natural dash="+dash+" owner="+owner);else Require(Count(geometry,owner)==0,"owner-only solar/mount dash is not fabricated remotely");
                        if(dash==2){Call(geometry,"Clear");p.eocHit=2;p.DashMovement();Require(Count(geometry,owner)==0,"shield recoil phase has no fresh attack");}
                    }
                }
                foreach(int time in new[]{9,10})
                {
                    var p=Player(0);p.downDashTime=time;Call(geometry,"Clear");Call(p,"DoDeadCellsGroundPoundEffect");
                    Circle(geometry,true,time==9?128:176,11);var eventSample=((Array)Get(geometry,"Events")).GetValue(0);var center=(Vector2)Get(((Array)Get(eventSample,"Shapes")).GetValue(0),"A");
                    Require(center==p.Center,"ground pound geometry follows actual player center, not lower visual origin");
                }
                var inferno=Player(0);inferno.buffType[0]=116;inferno.buffTime[0]=1;inferno.infernoCounter=1;Call(geometry,"Clear");inferno.UpdateBuffs(0);Circle(geometry,false,200,12);Require(inferno.inferno,"native buff branch actually applied between damage pulses");
                Call(geometry,"Clear");inferno.buffTime[0]=0;inferno.UpdateBuffs(0);Require(Count(geometry,0)==0,"expired Inferno cannot retain a region");
                var cart=Player(0);cart.mount.SetMount(6,cart);cart.velocity=new Vector2(-12,3);Call(geometry,"Clear");var cartBox=cart.getRect();cartBox.X-=25;cartBox.Height+=10;cart.Update_NPCCollision();Box(geometry,0,cartBox,"native collision boundary after cart window");
                cart.velocity=Vector2.Zero;Call(geometry,"Clear");cart.Update_NPCCollision();Require(Count(geometry,0)==0,"stationary cart has no attack");
                // Selected-target reactions have no damaging line or disk.
                var player=Player(0);var victim=Main.npc[0];victim.SetDefaults(1);victim.active=true;victim.life=10000;victim.position=new Vector2(700,700);Call(geometry,"Clear");
                player.TryHittingNPC(victim,20,0,null,5478,0,0);Require((int)Get(geometry,"EventCount")==1 && victim.life<10000,"electric-eel native selected hit produces only a victim event");
                var sample=((Array)Get(geometry,"Events")).GetValue(0);Require((int)Get(sample,"Count")==1 && (int)Get(((Array)Get(sample,"Shapes")).GetValue(0),"Condition")==13,"target selection condition remains explicit");
                Call(geometry,"Clear");player.ApplyDamageToNPC(victim,10,0,1,false,PlayerNPCHitSources.PlayerThorns);Require((int)Get(geometry,"EventCount")==1,"native conditional retaliation uses the observed victim event");victim.active=false;
                NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(geometry,"Clear");player.dash=player.dashType=2;player.dashDelay=-1;player.eocDash=10;player.eocHit=-1;player.DashMovement();Require(Count(geometry,0)==0,"OFF native body callback does no sampling");
                Console.WriteLine("PASS player attack windows: 36 natural mount directions, local/remote stomp/dash gates, ground-pound circles, Inferno, cart boundary and selected reactions; empty victims still produce domains.");
            }
            finally{Main.player[0]=local;Main.player[1]=remote;Main.npc[0]=originalNpc;Main.dedServ=server;foreach(var n in Main.npc)n.active=false;NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true));Call(geometry,"Clear");}
        }
        private static Player Player(int owner)
        {var p=new Player{whoAmI=owner,active=true,position=new Vector2(640,640),direction=1,gravDir=1};Main.player[owner]=p;return p;}
        private static int Count(object geometry,int owner)
        {var sample=((Array)Get(geometry,"Bodies")).GetValue(owner);return sample==null?0:(int)Get(sample,"Count");}
        private static void Box(object geometry,int owner,Rectangle expected,string name)
        {
            var sample=((Array)Get(geometry,"Bodies")).GetValue(owner);Require(sample!=null && Count(geometry,owner)==1,name+" has exactly one sample without victims");var shape=((Array)Get(sample,"Shapes")).GetValue(0);var a=(Vector2)Get(shape,"A");var b=(Vector2)Get(shape,"B");
            Require(new Rectangle((int)a.X,(int)a.Y,(int)(b.X-a.X),(int)(b.Y-a.Y))==expected,name+" exact region");Require((int)Get(shape,"Condition")==10,name+" keeps per-victim LOS/immune condition");
        }
        private static void Circle(object geometry,bool transient,int radius,int condition)
        {var array=(Array)Get(geometry,transient?"Events":"Bodies");var sample=array.GetValue(0);Require(sample!=null && (int)Get(sample,"Count")==1,"one native circular window");var shape=((Array)Get(sample,"Shapes")).GetValue(0);Require((int)Get(shape,"Kind")==6 && (float)Get(shape,"Width")==radius && (int)Get(shape,"Condition")==condition,"native radius with target-center condition");}
    }
}
