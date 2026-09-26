using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCaptureChecks
    {
        internal static void Run(object context,ProbeGraphics graphics)
        {
            int[] nets={1991,3183,4821};graphics.LoadItemTextures(nets);graphics.LoadItemTextures(new[]{0,2289});
            // G05's neutral 24x24 visual substitute is NOT a capture oracle.
            // This scope restores the exact original method and real XNBs.
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(typeof(Item).GetMethod("GetDrawHitbox"),HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            object host=Get(context,"Tools");var geometry=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Tools.NetGeometry");
            var p=Main.LocalPlayer;p.position=new Vector2(640,640);
            int cases=0;
            foreach(int id in nets)foreach(int direction in new[]{-1,1})foreach(float gravity in new[]{-1f,1f})foreach(bool glove in new[]{false,true})
            {
                p.inventory[0].SetDefaults(id);p.selectedItemState.Select(0);p.selectedItemState.Update();p.direction=direction;p.gravDir=gravity;p.meleeScaleGlove=glove;
                Item net=p.HeldItem;Rectangle frame=Item.GetDrawHitbox(id,p);
                Require(frame.Width!=24 || frame.Height!=28,"actual net texture frame is not inventory geometry: "+id);
                var oracle=new List<Rectangle>();p.itemAnimationMax=net.useAnimation;
                for(int animation=net.useAnimation-1;animation>0;animation--){p.itemAnimation=animation;p.ItemCheck_ApplyUseStyle(p.HeightOffsetHitboxCenter,net,frame);bool skip;Rectangle hit;p.ItemCheck_GetMeleeHitbox(net,frame,out skip,out hit);if(!skip)oracle.Add(hit);}
                object subject=Activator.CreateInstance(geometry,true);
                for(int x=570;x<=740;x+=10)for(int y=560;y<=760;y+=10)
                {
                    var target=new Rectangle(x,y,6,8);bool expected=oracle.Exists(r=>r.Intersects(target));
                    bool actual=(bool)Call(subject,"Hits",p,net,target,100L);
                    Require(expected==actual,"net actual facing/gravity geometry id="+id+" direction="+direction+" gravity="+gravity+" x="+x+" y="+y);cases++;
                }
            }
            p.itemAnimation=p.itemTime=0;p.gravDir=1;p.meleeScaleGlove=false;
            Console.WriteLine("PASS G09 real XNB/original melee geometry: "+cases+" symmetric facing/gravity/glove samples for all three nets.");
            ActualCapture(context,nets);
            NativeToolsIntegrationChecks.Visual(context,graphics);
        }
        private static void ActualCapture(object context,int[] nets)
        {
            object host=Get(context,"Tools"),input=Get(context,"Input"),capture=Get(host,"Capture");var p=Main.LocalPlayer;
            foreach(int id in nets)
            {
                NativeToolsChecks.SetMode(host,0,0);for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);
                foreach(var item in p.inventory)item.TurnToAir();p.inventory[12].SetDefaults(id);p.selectedItemState.Select(0);p.selectedItemState.Update();p.direction=1;p.position=new Vector2(640,640);p.itemAnimation=p.itemTime=0;
                var n=Main.npc[0];n.SetDefaults(46);n.whoAmI=0;n.position=new Vector2(677,642);n.active=true;n.life=n.lifeMax;
                Call(Get(host,"Npcs"),"BeginTick");NativeToolsChecks.SetMode(host,0,1);
                for(int f=0;f<90 && n.active;f++)NativeToolsChecks.Frame(context,input);
                Require(!n.active,"real native ItemCheck/CatchCritters captured bunny with net="+id+" error="+GetOptional(host,"Error"));
                for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);Require(p.selectedItem==0,"capture returns native selection after actual use");
            }
            NativeToolsChecks.SetMode(host,0,0);p.inventory[0].SetDefaults(1991);p.selectedItemState.Select(0);p.selectedItemState.Update();
            var target=Main.npc[0];target.SetDefaults(46);target.whoAmI=0;target.position=new Vector2(677,642);target.active=true;
            var boss=Main.npc[1];boss.SetDefaults(4);boss.active=true;boss.life=100;Call(Get(host,"Npcs"),"BeginTick");NativeToolsChecks.SetMode(host,0,2);
            Require(Call(capture,"Choose",p)==null,"Boss pauses held automatic capture without changing mode");
            for(int i=0;i<40 && target.active;i++)
            {
                NativeQuickItemChecks.Sample(input,new Microsoft.Xna.Framework.Input.Keys[0]);Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=true;Main.mouseLeft=true;
                NativeQuickItemChecks.NativeFrame(p);Call(context,"UpdateRuntime");
            }
            Require(!target.active,"Boss pause still permits actual manual native catching");boss.active=false;Call(Get(host,"Npcs"),"BeginTick");NativeToolsChecks.SetMode(host,0,0);
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft=false;Main.mouseLeft=false;for(int f=0;f<40;f++)NativeToolsChecks.Frame(context,input);
            Console.WriteLine("PASS G09 actual native capture with all three nets and manual capture during Boss pause.");
        }
    }
}
