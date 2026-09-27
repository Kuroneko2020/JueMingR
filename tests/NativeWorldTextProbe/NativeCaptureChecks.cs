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
                    bool actual=(bool)Call(subject,"Hits",p,net,target,(long)cases+100);
                    Require(expected==actual,"net actual facing/gravity geometry id="+id+" direction="+direction+" gravity="+gravity+" x="+x+" y="+y);cases++;
                }
                Require((long)Get(subject,"ShapeBuilds")==1 && (long)Get(subject,"ApplyUseStyleCalls")==3 && (long)Get(subject,"GetMeleeHitboxCalls")==3 && (long)Get(subject,"FrameReads")==1,"static geometry is prepared only three native phases across 378 updates/candidates");
            }
            p.itemAnimation=p.itemTime=0;p.gravDir=1;p.meleeScaleGlove=false;
            Console.WriteLine("PASS G09 real XNB/original melee geometry: "+cases+" symmetric facing/gravity/glove samples for all three nets.");
            GeometryChanges(context,graphics);
            ActualCapture(context,nets);
            NativeToolsIntegrationChecks.Visual(context,graphics);
        }
        private static void GeometryChanges(object context,ProbeGraphics graphics)
        {
            var p=Main.LocalPlayer;var type=Get(context,"Tools").GetType().Assembly.GetType("JueMingR.TerrariaHost.Tools.NetGeometry");var subject=Activator.CreateInstance(type,true);
            var net=new Item();net.SetDefaults(1991);int width=p.width,height=p.height;float speed=p.meleeSpeed;
            p.position=new Vector2(640,640);p.direction=1;p.gravDir=1;p.meleeScaleGlove=false;
            CheckChanged(subject,p,net,"initial");
            p.position+=new Vector2(.7f,.3f);CheckChanged(subject,p,net,"fractional world position");
            p.width+=2;p.height+=4;CheckChanged(subject,p,net,"hitbox dimensions");
            p.direction=-1;CheckChanged(subject,p,net,"direction");p.gravDir=-1;CheckChanged(subject,p,net,"gravity");
            // Native ordinary nets are not melee items, so the glove alone is
            // not an effective scale dependency. Exercise that no-op first,
            // then an explicitly melee synthetic tool for real scale change.
            p.meleeScaleGlove=true;CheckChanged(subject,p,net,"non-melee glove has no geometric effect",false);
            net.melee=true;CheckChanged(subject,p,net,"effective glove scale");net.scale*=1.2f;CheckChanged(subject,p,net,"item scale");
            net.useAnimation=21;CheckChanged(subject,p,net,"use duration");net.melee=true;p.meleeSpeed=.75f;CheckChanged(subject,p,net,"effective duration");
            p.portableStoolInfo.SetStats(26,13,26);p.portableStoolInfo.IsInUse=true;CheckChanged(subject,p,net,"native hitbox center offset");p.portableStoolInfo.Reset();
            graphics.LoadTexture("Item","Images/Item_1991",1991);CheckChanged(subject,p,net,"replacement texture value");
            var asset=Terraria.GameContent.TextureAssets.Item[1991];Terraria.GameContent.TextureAssets.Item[1991]=null;
            Require(!(bool)Call(subject,"Hits",p,net,new Rectangle(640,640,20,20),1L),"missing asset fails safely");Terraria.GameContent.TextureAssets.Item[1991]=asset;CheckChanged(subject,p,net,"asset becomes ready again");
            var animation=Main.itemAnimations[1991];Main.itemAnimations[1991]=new Terraria.DataStructures.DrawAnimationVertical(1,2);CheckChanged(subject,p,net,"animated texture first frame");Main.itemAnimations[1991].Update();CheckChanged(subject,p,net,"animated texture next frame");Main.itemAnimations[1991]=animation;
            p.width=width;p.height=height;p.meleeSpeed=speed;p.gravDir=1;p.meleeScaleGlove=false;p.itemAnimation=p.itemTime=0;
            Console.WriteLine("PASS G09 geometry invalidation: fractional position, dimensions, facing, gravity, scale/glove, duration, native center offset, texture value/frame, unavailable-to-ready resource.");
        }
        private static void CheckChanged(object subject,Player p,Item net,string label,bool rebuild=true)
        {
            long builds=(long)Get(subject,"ShapeBuilds"),styles=(long)Get(subject,"ApplyUseStyleCalls"),hitboxes=(long)Get(subject,"GetMeleeHitboxCalls");
            Rectangle frame=Item.GetDrawHitbox(net.type,p);int frames=Math.Max(1,(int)(net.useAnimation*(net.melee && !Terraria.ID.ItemID.Sets.NoMeleeSpeedBonus[net.type]?p.meleeSpeed:1f)));
            var expected=new List<Rectangle>();p.itemAnimationMax=frames;
            for(int f=frames-1;f>0;f--){p.itemAnimation=f;p.ItemCheck_ApplyUseStyle(p.HeightOffsetHitboxCenter,net,frame);bool inactive;Rectangle box;p.ItemCheck_GetMeleeHitbox(net,frame,out inactive,out box);if(!inactive)expected.Add(box);}
            long update=1000;
            for(int x=560;x<750;x+=7)for(int y=530;y<770;y+=7)
            {var target=new Rectangle(x,y,3,5);Require((bool)Call(subject,"Hits",p,net,target,update++)==expected.Exists(r=>r.Intersects(target)),"changed geometry independently matches native: "+label);}
            Require((long)Get(subject,"ShapeBuilds")-builds==(rebuild?1:0) && (long)Get(subject,"ApplyUseStyleCalls")-styles==(rebuild?3:0) && (long)Get(subject,"GetMeleeHitboxCalls")-hitboxes==(rebuild?3:0),
                "changed geometry rebuilds once then reuses across real update generations: "+label);
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
