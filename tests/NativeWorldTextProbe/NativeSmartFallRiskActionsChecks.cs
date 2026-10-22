using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Exact .8 stage/geometry checks in the existing isolated silent receiver.
    // This leaf does not claim complete Main, owner input, FPS or multiplayer.
    internal static class NativeSmartFallRiskActionsChecks
    {
        private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static IDisposable UseNativeHookFacts() { return new HookFacts(); }
        private sealed class HookFacts : IDisposable
        {
            private readonly int[] types=new int[7];
            private readonly bool[] previous=new bool[7];
            internal HookFacts()
            {
                int index=0;
                foreach(int itemType in new[]{84,1800,3623,2800,4759,4980,3572})
                {
                    var item=new Item();item.SetDefaults(itemType);var projectile=new Projectile();projectile.SetDefaults(item.shoot);
                    Require(projectile.aiStyle==7,"Original Main.Initialize representative hook fact item="+itemType);
                    types[index]=item.shoot;previous[index]=Main.projHook[item.shoot];Main.projHook[item.shoot]=true;index++;
                }
            }
            public void Dispose(){for(int i=0;i<types.Length;i++)Main.projHook[types[i]]=previous[i];}
        }
        internal static void Run(object context)
        {
            var assembly=context.GetType().Assembly;
            var capabilities=assembly.GetType("JueMingR.TerrariaHost.Movement.SmartFallCapabilities",true);
            var geometry=assembly.GetType("JueMingR.TerrariaHost.Movement.SmartFallGeometry",true);
            var movement=Get(context,"Movement");var input=Get(context,"Input");var jump=Get(movement,"JumpHost");
            Player previous=Main.LocalPlayer;double surface=Main.worldSurface;bool solid=Main.tileSolid[1],platform=Main.tileSolidTop[19],platformSolid=Main.tileSolid[19];
            bool up=Collision.up,down=Collision.down,stair=Collision.stair,stairFall=Collision.stairFall,sloping=Collision.sloping;
            var previousProjectile=Main.projectile[0];
            var previousTiles=Main.tile;int previousHeight=Main.maxTilesY;
            var saved=new Dictionary<Point,Tile>();
            try
            {
                NativeCombatLiveContextChecks.InitializeMount();NativeCombatWorkerAssetChecks.Initialize();
                // Keep the long ordinary-hook fall below the original space
                // reset band. Only this finite original-tile patch is added;
                // the existing world references and dimensions are restored.
                var finite=new Tile[Main.maxTilesX,200];
                for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<previousHeight;y++)finite[x,y]=previousTiles[x,y];
                Main.tile=finite;Main.maxTilesY=200;
                Main.worldSurface=10;Main.tileSolid[1]=true;Main.tileSolidTop[19]=Main.tileSolid[19]=true; // Original Main initialization 8073-8074.
                for(int x=30;x<70;x++)for(int y=40;y<145;y++) { var at=new Point(x,y);saved.Add(at,Main.tile[x,y]);Main.tile[x,y]=new Tile(); }
                Static(capabilities,"Bind");
                var p=Local();p.position=new Vector2(700,1000);p.fallStart=0;p.velocity.Y=10;
                Require((bool)Static(capabilities,"Risk",p),"Long native fall is dangerous.");
                p.hasWings=true;Require(!(bool)Static(capabilities,"Risk",p),"Retained native hasWings after unloading wings is already safe.");p.hasWings=false;
                p.wet=true;Require(!(bool)Static(capabilities,"Risk",p),"Wet update has a natural fallStart reset.");p.wet=false;
                p.slowFall=true;Require(!(bool)Static(capabilities,"Risk",p),"Djinn/slowFall's actual native reset requires no rescue.");p.slowFall=false;
                p.noFallDmg=true;Require(!(bool)Static(capabilities,"Risk",p),"Existing horseshoe does not trigger preparation.");p.noFallDmg=false;
                foreach(int item in new[]{53,128,934,158,946,84,3770,1326,1131})
                {
                    var real=new Item();real.SetDefaults(item);var family=item==53?SmartFallFamily.DoubleJump:item==128?SmartFallFamily.RocketBoots:item==934?SmartFallFamily.Carpet:item==158?SmartFallFamily.Horseshoe:item==946?SmartFallFamily.Umbrella:item==84?SmartFallFamily.Grapple:item==3770?SmartFallFamily.DjinnLegs:item==1326?SmartFallFamily.TeleportRod:SmartFallFamily.GravityGlobe;
                    Require((bool)Static(capabilities,"Matches",real,family),"Current original item identifies its actual family "+item);
                }
                Geometry(geometry);
                GrappleTakeover(movement,input,jump);
                Exception actionError=null;
                try { NativeSmartFallActionsChecks.Run(context); }
                catch(Exception error) { actionError=error; }
                // Stable-path costs have independent fresh local players and
                // must remain observable even when one native action fails.
                Workload(context);
                if(actionError!=null)throw new InvalidOperationException("Native action representatives failed; stable-path workload still executed.",actionError);
                Console.WriteLine("PASS SMART-FALL native-risk, original-geometry representatives, grapple/manual-Jump boundary; scope=original-components not full-Main/network/FPS");
            }
            finally
            {
                Call(jump,"ClearSession");Main.player[Main.myPlayer]=previous;Main.worldSurface=surface;Main.tileSolid[1]=solid;Main.tileSolidTop[19]=platform;Main.tileSolid[19]=platformSolid;
                Main.projectile[0]=previousProjectile;Collision.up=up;Collision.down=down;Collision.stair=stair;Collision.stairFall=stairFall;Collision.sloping=sloping;
                foreach(var pair in saved)Main.tile[pair.Key.X,pair.Key.Y]=pair.Value;
                Main.tile=previousTiles;Main.maxTilesY=previousHeight;
            }
        }
        private static Player Local()
        { var p=new Player{whoAmI=Main.myPlayer,active=true,position=new Vector2(700,1000),gravity=.4f,maxFallSpeed=10,gravDir=1};Main.player[Main.myPlayer]=p;return p; }
        private static void Geometry(Type geometry)
        {
            ShortOppositeBoundary(geometry);
            foreach(byte slope in new byte[]{0,1,2})foreach(float vx in new[]{-6f,6f})
            {
                for(int x=35;x<65;x++)for(int y=55;y<85;y++)Main.tile[x,y]=new Tile();
                for(int x=35;x<65;x++){var tile=Main.tile[x,75];tile.active(true);tile.type=1;tile.slope(slope);}
                var p=Local();p.position=new Vector2(700,75*16-p.height-48);p.velocity=new Vector2(vx,10);
                object[] args={p,0L};var predicted=(SmartFallContact)geometry.GetMethod("Contact",F,null,new[]{typeof(Player),typeof(long).MakeByRefType()},null).Invoke(null,args);
                Require(predicted.Found,"Finite band finds native floor/slope "+slope+" vx="+vx);
                int hit=-1;
                for(int tick=0;tick<20;tick++)
                {
                    p.velocity.Y=Math.Min(10,p.velocity.Y+.4f);float before=p.velocity.Y;
                    p.DryCollision(false,false);p.SlopingCollision(false,false);
                    if(p.velocity.Y==0 || Math.Abs(p.velocity.Y)<.011f && before>1){hit=tick+1;break;}
                }
                Require(hit>0 && Math.Abs(hit-predicted.Ticks)<=1.01f,"Bounded contact agrees with original DryCollision+SlopingCollision slope="+slope+" vx="+vx+" predicted="+predicted.Ticks+" native="+hit);
            }
            foreach(byte slope in new byte[]{3,4})foreach(float vx in new[]{-6f,6f})
            {
                for(int x=35;x<65;x++)for(int y=55;y<85;y++)Main.tile[x,y]=new Tile();
                for(int x=35;x<65;x++){var tile=Main.tile[x,65];tile.active(true);tile.type=1;tile.slope(slope);}
                var p=Local();p.position=new Vector2(700,65*16+16+48);p.gravDir=-1;p.velocity=new Vector2(vx,-10);
                object[] args={p,0L};var predicted=(SmartFallContact)geometry.GetMethod("Contact",F,null,new[]{typeof(Player),typeof(long).MakeByRefType()},null).Invoke(null,args);
                int hit=-1;for(int tick=0;tick<20;tick++){p.velocity.Y=-Math.Min(10,-p.velocity.Y+.4f);p.DryCollision(false,true);p.SlopingCollision(false,true);if(Math.Abs(p.velocity.Y)<.011f){hit=tick+1;break;}}
                Require(predicted.Found && hit>0 && Math.Abs(hit-predicted.Ticks)<=1.01f,"Reverse-gravity ceiling slope agrees with original full-width collision slope="+slope+" vx="+vx);
            }
            for(int x=35;x<65;x++)for(int y=55;y<85;y++)Main.tile[x,y]=new Tile();
            for(int x=35;x<65;x++){Main.tile[x,75].active(true);Main.tile[x,75].type=1;}
            for(int x=35;x<65;x++){var t=Main.tile[x,75];t.slope(0);t.halfBrick(true);}
            var half=Local();half.position.Y=75*16-half.height-24;half.velocity.Y=10;
            object[] halfArgs={half,0L};var h=(SmartFallContact)geometry.GetMethod("Contact",F,null,new[]{typeof(Player),typeof(long).MakeByRefType()},null).Invoke(null,halfArgs);
            Require(h.Found && Math.Abs(h.Y-(75*16+8-half.height))<.01f,"Half brick retains exact native top.");
            for(int x=35;x<65;x++){var t=Main.tile[x,75];t.type=19;t.halfBrick(false);t.frameY=0;}
            half.controlDown=true;object[] downArgs={half,0L};var d=(SmartFallContact)geometry.GetMethod("Contact",F,null,new[]{typeof(Player),typeof(long).MakeByRefType()},null).Invoke(null,downArgs);
            Require(!d.Found,"Native Down excludes the platform from the rescue contact.");
            for(int x=35;x<65;x++)for(int y=55;y<85;y++)Main.tile[x,y]=new Tile();
            for(int x=35;x<46;x++){Main.tile[x,75].active(true);Main.tile[x,75].type=1;}
            for(int y=60;y<75;y++){Main.tile[46,y].active(true);Main.tile[46,y].type=1;}
            var wall=Local();wall.position=new Vector2(700,75*16-wall.height-80);wall.velocity=new Vector2(6,10);
            object[] wallArgs={wall,0L};var w=(SmartFallContact)geometry.GetMethod("Contact",F,null,new[]{typeof(Player),typeof(long).MakeByRefType()},null).Invoke(null,wallArgs);
            Require(w.Found && w.X<=46*16-wall.width+.01f,"Full-height side contact stops the near descent corridor before a false beyond-wall landing.");
            for(int tick=0;tick<15 && wall.velocity.Y!=0;tick++){wall.velocity.Y=Math.Min(10,wall.velocity.Y+.4f);wall.DryCollision(false,false);wall.SlopingCollision(false,false);}
            Require(wall.velocity.Y==0 && Math.Abs(wall.position.X-w.X)<.01f,"Independent original side wall and floor reach the same finite contact band.");
        }
        private static void ShortOppositeBoundary(Type geometry)
        {
            MethodInfo contact=null;
            foreach(var method in geometry.GetMethods(F))if(method.Name=="Contact" && method.GetParameters().Length==11)contact=method;
            Require(contact!=null && (int)contact.GetParameters()[10].DefaultValue==48,"Main contact retains its optional 48-tick default.");
            foreach(float direction in new[]{-1f,1f})foreach(int ticks in new[]{1,2,3,20,49})
            {
                for(int x=35;x<65;x++)for(int y=50;y<90;y++)Main.tile[x,y]=new Tile();
                int row=direction<0?60:80;
                if(ticks<49)for(int x=35;x<65;x++){Main.tile[x,row].active(true);Main.tile[x,row].type=1;}
                float surface=direction<0?row*16+16:row*16;
                var position=new Vector2(700,direction<0?surface+ticks*8:surface-42-ticks*8);
                object[] full={position,new Vector2(0,direction*8),20,42,0f,8f,direction,false,0L,null,Type.Missing};
                object[] shortArgs=(object[])full.Clone();shortArgs[10]=2;
                var old=(SmartFallContact)contact.Invoke(null,full);var two=(SmartFallContact)contact.Invoke(null,shortArgs);
                bool oldAllowed=!old.Found || old.Ticks>2,newAllowed=!two.Found || two.Ticks>2;
                Require(oldAllowed==newAllowed && newAllowed==(ticks>2),"Two-tick query preserves original allow boundary direction="+direction+" ticks="+ticks);
                if(ticks<=2)Require(two.Found && two.Ticks==ticks,"Exact contact at tick 2 remains included, with unchanged substeps.");
                if(ticks==3 || ticks==20)Require(old.Found && old.Ticks==ticks && !two.Found,"Third/later tick remains visible to default main contact and is irrelevant to short rejection.");
                if(ticks==49)Require(!old.Found && !two.Found,"Empty full 48-tick horizon remains allowed.");
            }
        }
        private static void GrappleTakeover(object movement,object input,object jump)
        {
            NativeQuickItemChecks.Until(()=>(bool)Get(movement,"ControlsEnabled"));
            bool was=(bool)Call(movement,"IsEnabled",0);Call(movement,"SetEnabled",0,true);
            try
            {
                var p=Local();p.controlJump=true;p.releaseJump=false;p.rocketRelease=false;p.canJumpAgain_Cloud=p.hasJumpOption_Cloud=true;
                var hook=new Projectile();hook.SetDefaults(13);hook.owner=p.whoAmI;hook.active=true;hook.ai[0]=2;hook.position=p.position+new Vector2(50,-80);Main.projectile[0]=hook;p.grappling[0]=0;p.grapCount=1;
                NativeQuickItemChecks.Sample(input,new Keys[0]);
                Require((bool)Call(movement,"BeginUpdate",p),"Exact local scope admits the native grapple boundary.");
                try
                {
                    Call(movement,"CaptureInput",p);Call(movement,"BeforeJump",p);
                    Require(!p.releaseJump && !p.rocketRelease && !p.autoJump,"Held simulated Jump cannot manufacture a grapple release even with an available extra jump.");
                    p.GrappleMovement();
                    Require(hook.active && p.grappling[0]==0,"Original GrappleMovement retains an established hook under simulated holding.");
                }
                finally{Call(movement,"EndUpdate",p);}
                p.controlJump=true;p.releaseJump=true;p.GrappleMovement();
                Require(!hook.active,"Actual manual Jump still immediately removes its native hook.");
                Console.WriteLine("SMART-FALL GRAPPLE established native hook: simulated-hold retained; physical/manual edge native released");
            }
            finally{Call(movement,"SetEnabled",0,was);Call(jump,"ClearSession");}
        }
        private static void Workload(object context)
        {
            var movement=Get(context,"Movement");var host=Get(movement,"FallHost");
            var type=host.GetType();var begin=(Action<Player,long,long>)Delegate.CreateDelegate(typeof(Action<Player,long,long>),host,type.GetMethod("BeginUpdate",F));
            var capture=(Action<Player,bool>)Delegate.CreateDelegate(typeof(Action<Player,bool>),host,type.GetMethod("Capture",F));
            var end=(Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>),host,type.GetMethod("EndUpdate",F));
            var bytes=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",F));
            bool enabled=(bool)Call(movement,"IsEnabled",3);long sequence=0;
            try
            {
                foreach(string path in new[]{"OFF","ON-idle","ON-safe","ON-danger-no-capability"})
                {
                    Call(movement,"SetEnabled",3,path!="OFF");
                    var p=Local();p.velocity.Y=path=="ON-idle"?0:10;p.fallStart=0;p.noFallDmg=path=="ON-safe";
                    for(int i=0;i<256;i++){begin(p,1,++sequence);capture(p,true);end(p);}
                    long scans=(long)Get(host,"SourceScans"),plans=(long)Get(host,"Plans"),reads=(long)Get(host,"TileReads"),queries=(long)Get(host,"DismountObservations"),actions=(long)Get(host,"NativeActions");
                    long allocated=bytes();var time=Stopwatch.StartNew();
                    for(int i=0;i<20000;i++){begin(p,1,++sequence);capture(p,true);end(p);}
                    time.Stop();long delta=bytes()-allocated;
                    Require((long)Get(host,"SourceScans")==scans && (long)Get(host,"Plans")==plans && (long)Get(host,"TileReads")==reads,"Stable "+path+" has no repeated scans/plans/geometry.");
                    Require((long)Get(host,"DismountObservations")==queries && (long)Get(host,"NativeActions")==actions,"No owned mount/pending means no extra dismount query or action path="+path);
                    // Stopwatch itself is measured, so zero is checked in a
                    // separate strong-delegate window without reflection.
                    allocated=bytes();for(int i=0;i<20000;i++){begin(p,1,++sequence);capture(p,true);end(p);}long steady=bytes()-allocated;
                    Require(steady==0,"Steady helper thread allocates no new objects path="+path);
                    Console.WriteLine("SMART-FALL COST path="+path+" iterations=20000 ms="+time.Elapsed.TotalMilliseconds+" warm-bytes="+delta+" steady-bytes="+steady+" new-scans=0 new-plans=0 new-tiles=0 scope=helper-thread not FPS");
                    Call(host,"ClearSession");
                }
            }
            finally{Call(host,"ClearSession");Call(movement,"SetEnabled",3,enabled);}
        }
        private static void Require(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
        private static object Static(Type type,string method,params object[] args){return type.GetMethod(method,F).Invoke(null,args);}
    }
}
