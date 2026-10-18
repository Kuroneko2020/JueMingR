using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Features.WorldObjectText;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.Combat;
using JueMingR.Platform.Entities;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Narrow consumer checks use the real .8 types and native cursor/Shoot/AI
    // leaves. The View scope separately owns native zoom input and GPU Draw;
    // setting a matrix here proves only the downstream coordinate contract.
    internal static class NativeViewCombatChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            Discovery();
            NativeToolExecutionChecks.Initialize(context,null);
            var assembly=context.GetType().Assembly;
            var type=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);
            var selection=Activator.CreateInstance(type,Flags,null,new[]{Get(context,"nativeNpcs")},null);
            var player=Main.LocalPlayer;
            foreach(var n in Main.npc)n.active=false;
            Camera(1920,1080,.3125f);Main.screenPosition=new Vector2(3000,3000);player.Center=Main.screenPosition+new Vector2(960,540);
            var farther=Main.npc[2];farther.SetDefaults(3);farther.whoAmI=2;farther.active=true;farther.width=200;farther.height=80;farther.Center=player.Center+new Vector2(2500,100);
            var nearer=Main.npc[3];nearer.SetDefaults(3);nearer.whoAmI=3;nearer.active=true;nearer.width=20;nearer.height=20;nearer.Center=player.Center+new Vector2(2450,100);
            foreach(float gravity in new[]{1f,-1f})
            {
                player.gravDir=gravity;
                var pixel=Vector2.Transform(Main.ReverseGravitySupport(farther.Center-Main.screenPosition),Main.GameViewMatrix.ZoomMatrix);
                Require(pixel.X>0 && pixel.X<Main.screenWidth && pixel.Y>0 && pixel.Y<Main.screenHeight,"receiver beyond old140 is actually in the final physical viewport");
                float radius=(float)type.GetMethod("PlayerRadius",Flags).Invoke(null,new object[]{player.Center});
                Require(radius>140*16 && !float.IsInfinity(radius),"player radius covers current wide camera with finite padding in either gravity");
                Call(Get(context,"nativeNpcs"),"BeginTick");Call(selection,"Update",new ObservationOptions(path:true),1L,true,null);
                Require((int)Get(selection,"CandidateCount")==2 && ((NpcIdentity)Get(selection,"Target")).Slot==2,"current active receivers beyond140 remain ordered by nearest hitbox rather than center");
            }
            Require((float)type.GetMethod("PlayerRadius",Flags).Invoke(null,new object[]{new Vector2(float.NaN,0)})==800,"invalid center cannot authorize an infinite candidate circle");
            Main.GameViewMatrix.Zoom=Vector2.Zero;
            Require((float)type.GetMethod("PlayerRadius",Flags).Invoke(null,new object[]{player.Center})==800,"degenerate camera stays finite");
            Adapters(context);
            PointerConsumers(context,selection);
            Console.WriteLine("PASS VIEW combat: visible beyond140, hitbox ordering, finite geometry, native physical mouse/Shoot/channel consumers and cursor return.");
        }
        private static void Adapters(object context)
        {
            var assembly=context.GetType().Assembly;Camera(800,600,.3125f);Main.screenPosition=new Vector2(500,500);
            Main.LocalPlayer.Center=new Vector2(900,800);
            var tiles=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.World.WorldTileObservation",true),Flags,null,new object[]{(Func<bool>)(()=>true)},null);
            object[] args={0,default(WorldTargetView)};
            Require((bool)Call(tiles,"TryView",args),"existing tile observer admits actual wide camera");var view=(WorldTargetView)args[1];
            Require(view.X<=90 && view.Right>90 && view.Y<=70 && view.Bottom>70,"existing inverse matrix includes a tile beyond ordinary physical rectangle");
            var source=(IEntityObservationSource)Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.EntityLabels.EntityHostObservation",true),Flags,null,new object[]{(Func<bool>)(()=>true),null},null);
            var npc=Main.npc[4];npc.SetDefaults(3);npc.whoAmI=4;npc.active=true;npc.position=new Vector2(1450,1150);npc.netOffset=new Vector2(7,9);npc.gfxOffY=5;
            EntityObservation observation;
            Require(source.TryObserve(EntityObservationDemand.Enemy,out observation) && observation[4].Visible && observation[4].X==npc.Center.X+7 && observation[4].Y==npc.position.Y+14,"existing entity prefilter includes wide peripheral body and preserves native interpolation");
            var host=Get(context,"CombatObservation");var selected=Get(host,"Selection");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,false,25,true,true));
            Call(selected,"Choose",npc,Get(host,"Session"),false);
            var marker=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatTargetMarker",true),Flags,null,new[]{host},null);
            foreach(float gravity in new[]{1f,-1f})
            {
                Main.LocalPlayer.gravDir=gravity;var zoom=Main.GameViewMatrix.ZoomMatrix;Call(marker,"Prepare",zoom,Matrix.Invert(zoom));
                Require((bool)Get(marker,"Visible"),"existing marker retains peripheral selected body in either gravity");
                var pieces=(Array)Get(marker,"Pieces");var mean=Vector2.Zero;
                foreach(var piece in pieces)mean+=Vector2.Transform((Vector2)Get(piece,"Position"),zoom);mean/=pieces.Length;
                var expected=Vector2.Transform(Main.ReverseGravitySupport(npc.Center+npc.netOffset-Main.screenPosition),zoom);
                Require(Vector2.Distance(mean,expected)<2,"marker surrounds interpolated native body under final world matrix");
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());npc.active=false;Main.LocalPlayer.gravDir=1;
            Console.WriteLine("PASS VIEW existing adapters: actual tile inverse, entity interpolation and final-camera peripheral marker in either gravity; no duplicate camera.");
        }
        private static void Camera(int width,int height,float zoom)
        {
            Main.screenWidth=width;Main.screenHeight=height;
            Main.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(null);
            Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,width,height));Main.GameViewMatrix.Zoom=new Vector2(zoom);
            typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,width);
            typeof(PlayerInput).GetField("_originalScreenHeight",Flags).SetValue(null,height);
        }
        private static void Physical(object context,int x,int y)
        {
            var input=Get(context,"Input");Call(input,"BeginUpdate");
            PlayerInput.MouseInfo=new MouseState(x,y,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            var tokens=new List<string>();PlayerInput.Triggers.Reset();Call(input,"AfterNativeMouse",tokens);PlayerInput.Triggers.Update();
            PlayerInput.MouseX=x;PlayerInput.MouseY=y;PlayerInput.UpdateMainMouse();PlayerInput.CacheZoomableValues();
            Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState();Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
            PlayerInput.SetZoom_World();
        }
        private static void PointerConsumers(object context,object selection)
        {
            var player=Main.LocalPlayer;var assembly=context.GetType().Assembly;
            var scope=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true);
            foreach(var n in Main.npc)n.active=false;
            player.position=new Vector2(640,640);Main.screenPosition=new Vector2(300,400);
            player.inventory[0].SetDefaults(ItemID.FlintlockPistol);player.selectedItemState.Select(0);player.selectedItemState.Update();
            player.inventory[54].SetDefaults(ItemID.MusketBall);player.inventory[54].stack=999;
            foreach(float gravity in new[]{1f,-1f})foreach(float zoom in new[]{.5f,.3125f})
            {
                Camera(800,600,zoom);player.gravDir=gravity;Physical(context,720,265);
                Call(selection,"SampleMouse",Get(context,"Input"));
                Require((bool)Get(selection,"HasMouse") && (Vector2)Get(selection,"RealMouse")==Main.MouseWorld,"physical sample equals real native SetZoom_World/MouseWorld in either gravity");
                // Changing zoom under the same physical pointer must refresh its
                // world point; the user radius remains measured in world tiles.
                var sample=(Vector2)Get(selection,"RealMouse");
                var npc=Main.npc[4];npc.SetDefaults(3);npc.whoAmI=4;npc.active=true;npc.netOffset=Vector2.Zero;npc.width=npc.height=10;npc.Center=sample+new Vector2(41,0);
                Call(Get(context,"nativeNpcs"),"BeginTick");Call(selection,"Update",new ObservationOptions(path:true,mouseCenter:true,radius:2),1L,true,null);
                Require((int)Get(selection,"CandidateCount")==0,"mouse radius32 excludes hitbox36 away regardless of zoom");
                npc.Center=sample+new Vector2(35,0);Call(Get(context,"nativeNpcs"),"BeginTick");Call(selection,"Update",new ObservationOptions(path:true,mouseCenter:true,radius:2),1L,true,null);
                Require((int)Get(selection,"CandidateCount")==1,"mouse radius32 includes hitbox30 away regardless of zoom; count="+Get(selection,"CandidateCount")+" gravity="+gravity+" zoom="+zoom+" sample="+sample+" currentMouse="+Get(selection,"RealMouse")+" bounds="+npc.Hitbox+" active="+npc.active+" life="+npc.life+" friendly="+npc.friendly+" immune="+npc.dontTakeDamage+" immortal="+npc.immortal);npc.active=false;
                int oldX=Main.mouseX,oldY=Main.mouseY,oldTileX=Player.tileTargetX,oldTileY=Player.tileTargetY;
                var aim=new Vector2(1800,720);
                var receipt=scope.GetMethod("BeginTileTarget",Flags).Invoke(null,new object[]{aim,true});
                try
                {
                    Require(Main.mouseX>Main.screenWidth && Main.MouseWorld==aim,"native cursor is inverse-zoom consumer coordinates, legally outside physical width");
                    Require(Player.tileTargetX==(int)(aim.X/16) && Player.tileTargetY==(int)(aim.Y/16),"paired native tile target uses the same assisted world point");
                    foreach(var shot in Main.projectile)shot.active=false;
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(player,new object[]{0,player.HeldItem,50,false});
                    var bullet=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==14);
                    Require(Vector2.Dot(Vector2.Normalize(bullet.velocity),Vector2.Normalize(aim-player.MountedCenter))>.995f,"actual native Shoot aims toward borrowed world point at wide zoom");
                    foreach(var shot in Main.projectile)shot.active=false;
                    var missile=Main.projectile[8];missile.SetDefaults(16);missile.whoAmI=8;missile.owner=0;missile.active=true;missile.Center=player.Center;missile.velocity=Vector2.Zero;player.channel=true;
                    player.inventory[0].SetDefaults(ItemID.MagicMissile);
                    try
                    {
                        var limited=aim;player.LimitPointToPlayerReachableArea(ref limited);missile.AI();
                        Require(missile.velocity.X>0 && Vector2.Dot(Vector2.Normalize(missile.velocity),Vector2.Normalize(limited-player.Center))>.99f,"actual native channel AI consumes the same world point and retains its native reach limit");
                    }
                    finally{missile.active=false;player.channel=false;player.inventory[0].SetDefaults(ItemID.FlintlockPistol);}
                }
                finally{Call(receipt,"End");}
                Require(Main.mouseX==oldX && Main.mouseY==oldY && Player.tileTargetX==oldTileX && Player.tileTargetY==oldTileY,"both cursor and tile target return after native consumers");
            }
            player.gravDir=1;
        }
        internal static void Discovery()
        {
            var targetSource=new ChangingView(12);var targets=new WorldTargetFeature(targetSource);
            targets.Configure(WorldTargetSettings.Default.WithEnabled(WorldTargetKind.LifeCrystal,true));targets.OnSessionStarted();
            var objectSource=new ChangingView(55);var objects=new WorldObjectDiscovery(objectSource);
            var settings=WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign,WorldObjectMode.All);
            int maxTargetReads=0,maxObjectReads=0;
            for(int tick=0;tick<16;tick++)
            {
                targetSource.Change(tick);objectSource.Change(tick);
                int before=targetSource.Reads,otherBefore=objectSource.Reads;targets.Update((ulong)tick);objects.Update(settings,null);
                maxTargetReads=Math.Max(maxTargetReads,targetSource.Reads-before);maxObjectReads=Math.Max(maxObjectReads,objectSource.Reads-otherBefore);
                Require(maxTargetReads<=4200 && maxObjectReads<=4200,"continuous zoom discovery retains bounded existing per-update work");
            }
            Require(targets.Targets.Any(t=>t.TileY==90) && objects.Candidates.Any(t=>t.Value.TileY==90),"continuous geometry changes reach the late intersecting rows instead of restarting forever");
            for(int tick=16;tick<32;tick++)
            {targetSource.Width=objectSource.Width=220;targets.Update((ulong)tick);objects.Update(settings,null);}
            Require(targets.Targets.Count==3 && objects.SelectedCount==3,"stable expanded view promptly fills its newly visible edge");
            var rejectedSource=new ChangingView(55){Width=40};var rejectedObjects=new WorldObjectDiscovery(rejectedSource);
            rejectedObjects.Update(settings,null);rejectedObjects.Reject(rejectedObjects.Candidates[0]);rejectedObjects.Update(settings,null);
            Require(rejectedObjects.Candidates.Count==0,"same geometry preserves text rejection");
            rejectedSource.Geometry++;rejectedObjects.Update(settings,null);
            Require(rejectedObjects.SelectedCount==1,"geometry change retries layout rejection without resetting discovery progress");
            targetSource.Unknown=objectSource.Unknown=true;targets.Update(33);objects.Update(settings,null);
            Require(targets.Targets.Count==1 && objects.SelectedCount==1,"unknown late cells immediately withdraw existing objects");
            targetSource.Unknown=objectSource.Unknown=false;targetSource.Revision++;objectSource.Revision++;targets.Update(34);objects.Update(settings,null);
            Require(targets.Targets.All(t=>t.TileY<90) && objects.Candidates.All(t=>t.Value.TileY<90),"read identity change really clears late prior-world observations");
            targetSource.X=objectSource.X=1000;targets.Update(35);objects.Update(settings,null);
            Require(targets.Targets.Count==0 && objects.SelectedCount==0,"nonintersecting camera instantly withdraws old objects");
            targets.Configure(WorldTargetSettings.Default);objects.Update(WorldObjectSettings.Default,null);
            int reads=targetSource.Reads+objectSource.Reads,begins=targetSource.Begins+objectSource.Begins;
            for(int tick=0;tick<100;tick++){targets.Update((ulong)tick);objects.Update(WorldObjectSettings.Default,null);}
            Require(reads==targetSource.Reads+objectSource.Reads && begins==targetSource.Begins+objectSource.Begins,"all-off discovery has no source/tile work");
            Console.WriteLine("PASS VIEW discovery: 16 changing geometries, stable edge complete within16 updates, unknown/world/teleport withdrawal; max tile reads/update="+maxTargetReads+"/"+maxObjectReads+", 100 all-OFF updates perform zero source/tile work.");
        }
        private sealed class ChangingView : IWorldTargetSource,IWorldObjectSource
        {
            private static readonly Point[] Positions={new Point(4,4),new Point(180,90),new Point(210,90)};
            private readonly int type;
            internal int Reads,Begins,X,Width=200;internal long Revision=1,Geometry;internal bool Unknown;
            internal ChangingView(int type){this.type=type;}
            internal void Change(int tick){Width=200+tick%8;Geometry++;}
            public bool HasDetector{get{return true;}}
            public bool TryBegin(out WorldTargetView view){Begins++;view=new WorldTargetView(X,0,Width,100,Revision,Geometry);return true;}
            public bool TryBegin(bool chestNames,bool signText,out WorldObjectView view)
            {WorldTargetView area;TryBegin(out area);view=new WorldObjectView{Visible=area,Discovery=area,PlayerX=1600,PlayerY=800};return true;}
            public WorldTargetTile Read(int x,int y)
            {
                Reads++;
                foreach(var point in Positions)
                    if(x>=point.X && x<point.X+2 && y>=point.Y && y<point.Y+2)
                        return Unknown && y>=90?default(WorldTargetTile):new WorldTargetTile{Readable=true,Active=true,Type=type,FrameX=(x-point.X)*18,FrameY=(y-point.Y)*18};
                return default(WorldTargetTile);
            }
            public bool TryText(WorldObject value,out string text){text="view fixture";return true;}
        }
    }
}
