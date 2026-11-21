using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Net.Sockets;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Uses an already joined real fixture body. No new world, archive format or
    // fake native item method: this is a narrow leaf of CompanionCpu.
    internal static class NativeCompanionTileContextChecks
    {
        private static Player checkedBody;
        private static int extractCalls;
        private static bool drillTailObserved;
        private static void DrillTail(Projectile __instance)
        {
            if(checkedBody==null || __instance.type!=ProjectileID.LaserDrill || __instance.owner!=checkedBody.whoAmI)return;
            drillTailObserved=Vector2.DistanceSquared(__instance.Center,checkedBody.RotatedRelativePoint(checkedBody.MountedCenter))<0.01f &&
                __instance.timeLeft==2 && checkedBody.heldProj==__instance.whoAmI && checkedBody.itemTime>0;
        }
        private static object Hook(Type type,string name,params object[] args){return type.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);}
        private static void Extract(Player __instance){if(ReferenceEquals(checkedBody,__instance))extractCalls++;}
        internal static void Run(object host,Player body){RunCore(host,body,false);}
        internal static void RunDrill(object host,Player body){RunCore(host,body,true);}
        private static void RunCore(object host,Player body,bool drillOnly)
        {
            var actor=Call(host,"Owned",body);Require(actor!=null,"tile context uses a registered live body");
            var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionHooks");
            var patch=new Harmony("JueMingR.Tests.CompanionTileContext");
            patch.Patch(AccessTools.Method(typeof(Player),"PlaceThing_ItemInExtractinator"),prefix:new HarmonyMethod(typeof(NativeCompanionTileContextChecks),nameof(Extract)));
            patch.Patch(AccessTools.Method(typeof(Projectile),"AI_075"),postfix:new HarmonyMethod(typeof(NativeCompanionTileContextChecks),nameof(DrillTail)));
            int tx=(int)(body.Center.X/16)+1,ty=(int)(body.Center.Y/16);
            var cells=new Dictionary<Point,Tile>();
            foreach(var point in new[]{new Point(tx,ty),new Point(tx+1,ty),new Point(tx,ty+1),new Point(tx+1,ty+1)})cells[point]=Main.tile[point.X,point.Y];
            var actorState=new Dictionary<string,object>();
            foreach(string name in new[]{"Aim","Target","HasAim","WasUsing","AimTimeline","AimTerrain","AimRevision","Profile","Behavior","Status","Dirty"})actorState[name]=GetOptional(actor,name);
            var slot=body.selectedItemState;var item=body.inventory[0];var ammo=body.inventory[54];var visual=body.lastVisualizedSelectedItem;
            int mode=Main.netMode,my=Main.myPlayer,mx=Main.mouseX,myy=Main.mouseY,oldTx=Player.tileTargetX,oldTy=Player.tileTargetY;
            var sections=Main.sectionManager;
            var mainNetworkBuffer = NetMessage.buffer[256];
            var mainSocket = Netplay.Connection.Socket;
            var listener = new TcpListener(IPAddress.Loopback, 0);
            var primary = new TcpClient();
            TcpClient peer = null;
            var priorConnection = GetOptional(actor, "Connection");
            var outputFrames = new List<byte[]>();
            var connectionType = hooks.Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionConnection");
            var testConnection = Activator.CreateInstance(connectionType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { new Action<byte[]>(frame => outputFrames.Add(frame)), new Action(() => {}) }, null);
            int animation=body.itemAnimation,animationMax=body.itemAnimationMax,time=body.itemTime,timeMax=body.itemTimeMax,direction=body.direction;
            bool use=body.controlUseItem,channel=body.channel,release=body.releaseUseItem;
            bool useAttempt=body.lastItemUseAttemptSuccess;
            bool noItems=body.noItems;
            int held=body.heldProj;float rotation=body.itemRotation;uint clock=Main.GameUpdateCount;
            var controls=new Dictionary<string,object>();foreach(string name in new[]{"controlLeft","controlRight","controlUp","controlDown","controlJump","controlUseTile","controlHook","controlThrow"})controls[name]=Get(body,name);
            var position=body.position;var velocity=body.velocity;
            var beforeProjectiles=new bool[Main.projectile.Length];for(int i=0;i<beforeProjectiles.Length;i++)beforeProjectiles[i]=Main.projectile[i].active;
            checkedBody=body;
            try
            {
                // Setting netMode alone bypasses native client initialization.
                // ItemCheck's actual SendData(13) locks this real serializer.
                NetMessage.buffer[256] = new MessageBuffer { whoAmI = 256 };
                listener.Start();
                primary.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                peer = listener.AcceptTcpClient();
                Netplay.Connection.Socket = new TcpSocket(primary);
                Set(actor, "Connection", testConnection);
                Main.netMode=1;Set(actor,"Target",null);Set(actor,"Behavior",Activator.CreateInstance(actorState["Behavior"].GetType(),true));body.selectedItemState.Select(0);body.selectedItemState.Update();
                Main.sectionManager=new WorldSections((Main.maxTilesX+199)/200,(Main.maxTilesY+149)/150);
                Main.sectionManager.SetSectionLoaded(Netplay.GetSectionX(tx),Netplay.GetSectionY(ty));
                var missing=new Vector2(tx*16+8,ty*16+8);
                if(!drillOnly)
                {
                    Set(actor,"Aim",missing);Call(host,"Spawn",actor,PlayerSpawnContext.ReviveFromDeath);
                    Require((Vector2)Get(actor,"Aim")==body.Center && GetOptional(actor,"Target")==null && !(bool)Get(actor,"HasAim"),"native respawn retires pre-spawn/old-target aim");
                }
                // Spawn checks this fixture's nearby support area through
                // native collision. Remove the use target after real Spawn.
                Main.tile[tx,ty]=null;
                if(!drillOnly)
                {
                Set(actor,"Aim",missing);Call(host,"Control",actor);
                Require((Vector2)Get(actor,"Aim")==body.Center,"no-target control uses this body's current center");
                var profile=(CompanionProfile)Get(actor,"Profile");var disabled=profile.Behavior;disabled.AttackEnabled=false;
                Set(actor,"Profile",profile.Revise(profile.NativeData,disabled));Set(actor,"Aim",missing);Call(host,"Control",actor);
                Require((Vector2)Get(actor,"Aim")==body.Center && !body.controlUseItem,"disabled attack also retires old aim without stopping movement");
                Set(actor,"Profile",profile);Set(actor,"Target",null);Set(actor,"Aim",missing);
                body.inventory[0]=new Item();body.inventory[0].SetDefaults(ItemID.CopperShortsword);
                body.itemAnimation=0;body.itemTime=10;body.controlUseItem=false;body.channel=false;
                extractCalls=0;
                using(var scope=(IDisposable)Hook(hooks,"Enter",actor,missing,false))
                {
                    Require(!(bool)Get(scope,"TileInteractionReady") && Main.mouseX==(int)(missing.X-Main.screenPosition.X),"unknown interaction cell retains the exact real mouse aim");
                    body.ItemCheck();
                }
                Require(body.itemTime==9 && extractCalls==0 && Main.tile[tx,ty]==null,"unknown cell defers PlaceThing, not native item timers, and stays unknown");
                body.inventory[0]=new Item();body.inventory[0].SetDefaults(ItemID.Wrench);body.itemTime=0;body.itemAnimation=0;
                using((IDisposable)Hook(hooks,"Enter",actor,missing,false))body.ItemCheck();
                Require(Main.tile[tx,ty]==null,"idle wiring never dereferences/materializes its unknown in-range tile");
                Main.tile[tx,ty]=new Tile();Main.tile[tx+1,ty]=null;Main.tile[tx,ty+1]=null;Main.tile[tx+1,ty+1]=null;
                Main.sectionManager=new WorldSections((Main.maxTilesX+199)/200,(Main.maxTilesY+149)/150);extractCalls=0;
                body.inventory[0]=new Item();body.inventory[0].SetDefaults(ItemID.CopperShortsword);body.itemAnimation=0;body.controlUseItem=false;
                using(var scope=(IDisposable)Hook(hooks,"Enter",actor,missing,false))
                {Require(!(bool)Get(scope,"TileInteractionReady"),"nonnull but unACK is still unknown");body.ItemCheck();}
                Require(extractCalls==0,"materialized but unACK tile cannot authorize native world work");
                Main.sectionManager.SetSectionLoaded(Netplay.GetSectionX(tx),Netplay.GetSectionY(ty));
                body.inventory[0]=new Item();body.inventory[0].SetDefaults(ItemID.WorkBench);
                using((IDisposable)Hook(hooks,"Enter",actor,missing,false))Call(body,"UpdatePlacementPreview",body.inventory[0]);
                Require(Main.tile[tx+1,ty]==null && Main.tile[tx,ty+1]==null && Main.tile[tx+1,ty+1]==null,"AI presentation cannot materialize unknown placement footprint/anchor cells");
                body.inventory[0]=new Item();body.inventory[0].SetDefaults(ItemID.CopperShortsword);body.itemAnimation=0;body.controlUseItem=false;
                Set(actor,"Aim",missing);extractCalls=0;
                using((IDisposable)Hook(hooks,"Enter",actor,missing,false))body.ItemCheck();
                Require(extractCalls==1,"the same now-known tile reaches the real native Extractinator consumer");
                Main.tile[tx,ty]=null;Set(actor,"Aim",missing);
                body.inventory[0]=new Item();body.inventory[0].SetDefaults(ItemID.WoodenBow);
                body.inventory[54]=new Item();body.inventory[54].SetDefaults(ItemID.WoodenArrow);body.inventory[54].stack=2;
                body.itemTime=body.itemAnimation=0;body.releaseUseItem=true;body.controlUseItem=true;body.lastItemUseAttemptSuccess=false;
                using((IDisposable)Hook(hooks,"Enter",actor,missing,true))body.ItemCheck();
                Require(body.inventory[54].stack==1 && body.itemAnimation>0,"real native bow fires/pays ammo while unrelated placement at unknown aim waits");
                Require(outputFrames.Any(packet => packet[2] == 13 && packet[3] == body.whoAmI),
                    "the real native use edge serializes its control frame to the actor byte outlet");
                Require((Vector2)Get(actor,"Aim")==missing && Main.tile[tx,ty]==null,"firing does not redirect the action or invent terrain");
                }
                Set(actor,"Aim",missing);
                body.inventory[0]=new Item();body.inventory[0].SetDefaults(ItemID.LaserDrill);
                body.channel=true;body.noItems=false;
                Projectile drill;
                using((IDisposable)Hook(hooks,"Enter",actor,missing,false))
                {
                    int drillSlot=Projectile.NewProjectile(body.GetProjectileSource_Item(body.HeldItem),body.Center,Vector2.Zero,ProjectileID.LaserDrill,0,0,body.whoAmI);
                    drill=Main.projectile[drillSlot];body.channel=true;
                    Require(ReferenceEquals(Call(host,"ProjectileActor",drill),actor),"real native drill birth belongs to this activation");
                    Require(!(bool)Hook(hooks,"DrillTileWork",drill),"only the held drill's unknown tile work waits");
                    var other=new Projectile();other.SetDefaults(ProjectileID.WoodenArrowFriendly);
                    Require((bool)Hook(hooks,"DrillTileWork",other),"unrelated projectiles are not suspended");
                    var humanDrill=new Projectile();humanDrill.SetDefaults(ProjectileID.LaserDrill);humanDrill.owner=my;body.channel=true;
                    Require((bool)Hook(hooks,"DrillTileWork",humanDrill),"a human/unowned drill nested inside an actor scope is never gated as that actor");
                    Require((bool)Hook(hooks,"PlaceBefore",Main.player[my]) && (bool)Hook(hooks,"PreviewBefore",Main.player[my]),"human placement and preview keep their original permission");
                }
                // Run the real whole projectile update with human identity
                // outside its native AI/Damage scopes. Never fake the held
                // tail, timeLeft renewal or an extra owner-wide Update scope.
                for(int tick=0;tick<6;tick++)
                {
                    body.position.X-=1f;drillTailObserved=false;NativeQuickItemChecks.BeginWorldStep();drill.Update(drill.whoAmI);
                    Require(drillTailObserved && drill.active && drill.timeLeft>0 && body.heldProj==drill.whoAmI && body.itemTime>0,"unknown tile waits beyond native two-tick life while original position/lifetime/held/item-time tail runs");
                    Require(Main.myPlayer==my && Main.tile[tx,ty]==null,"projectile update keeps human identity and unknown terrain");
                }
                Main.tile[tx,ty]=new Tile();Main.tile[tx,ty].active(true);Main.tile[tx,ty].type=TileID.Stone;
                NativeQuickItemChecks.BeginWorldStep();drill.Update(drill.whoAmI);
                Require(drill.active && drill.localAI[1]>0,"same loaded tile resumes native tile-target steering");
                Main.tile[tx,ty]=null;body.channel=false;NativeQuickItemChecks.BeginWorldStep();drill.Update(drill.whoAmI);
                Require(!drill.active,"native release retires the drill even when tile work is unavailable");
                Require(Main.myPlayer==my && Main.mouseX==mx && Main.mouseY==myy && Player.tileTargetX==oldTx && Player.tileTargetY==oldTy,"native tile work returns exact human cursor identity");
                Console.WriteLine(drillOnly?"PASS CompanionTileContext drill leaf: six real Projectile.Update ticks unknown -> known native steering -> release, original held tail and exact actor ownership.":"PASS CompanionTileContext: spawn/no-target/disabled aim; unknown placement/wiring; preview footprint; known native consumer; real bow payment; six-tick drill wait/release; human/other-projectile permission.");
            }
            finally
            {
                patch.UnpatchAll(patch.Id);checkedBody=null;
                NetMessage.buffer[256] = mainNetworkBuffer;
                Netplay.Connection.Socket = mainSocket;
                Set(actor, "Connection", priorConnection);
                ((IDisposable)testConnection).Dispose();
                peer?.Dispose(); primary.Dispose(); listener.Stop();
                foreach(var pair in cells)Main.tile[pair.Key.X,pair.Key.Y]=pair.Value;
                foreach(var pair in actorState)Set(actor,pair.Key,pair.Value);
                body.selectedItemState=slot;body.inventory[0]=item;body.inventory[54]=ammo;body.lastVisualizedSelectedItem=visual;
                body.itemAnimation=animation;body.itemAnimationMax=animationMax;body.itemTime=time;body.itemTimeMax=timeMax;body.direction=direction;
                body.controlUseItem=use;body.channel=channel;body.releaseUseItem=release;body.lastItemUseAttemptSuccess=useAttempt;body.position=position;body.velocity=velocity;
                body.noItems=noItems;foreach(var pair in controls)Set(body,pair.Key,pair.Value);
                body.heldProj=held;body.itemRotation=rotation;AccessTools.Field(typeof(Main),"_gameUpdateCount").SetValue(null,clock);
                for(int i=0;i<beforeProjectiles.Length;i++)if(!beforeProjectiles[i])Main.projectile[i].active=false;
                Main.netMode=mode;Main.myPlayer=my;Main.mouseX=mx;Main.mouseY=myy;Player.tileTargetX=oldTx;Player.tileTargetY=oldTy;
                Main.sectionManager=sections;
            }
        }
    }
}
