using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.Combat;
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
    // JMR_VIEW_CACHE_RANGE_20261018/05-06: downstream checks, using the same
    // isolated native .8 fixture as ViewCombatCpu. A null-device native matrix
    // proves quantization/consumer propagation, not GPU Draw or a network run.
    // The original Combat checks and their expectations remain untouched.
    internal static class NativeViewRangeConsumerChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            var actual=context.GetType().Assembly;
            using(var file=System.IO.File.OpenRead(actual.Location))using(var sha=System.Security.Cryptography.SHA256.Create())
                Console.WriteLine("VIEW RANGE actual Host path="+actual.Location+" MVID="+actual.ManifestModule.ModuleVersionId+" SHA256="+BitConverter.ToString(sha.ComputeHash(file)).Replace("-",""));
            NativeToolExecutionChecks.Initialize(context,null);
            var assembly=context.GetType().Assembly;
            var type=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);
            var selection=Activator.CreateInstance(type,Flags,null,new[]{Get(context,"nativeNpcs")},null);
            var host=Get(context,"CombatObservation");
            var oldOptions=(ObservationOptions)Get(host,"Options");
            var player=Main.LocalPlayer;
            var oldMatrix=Main.GameViewMatrix;var oldPosition=Main.screenPosition;var oldCenter=player.Center;
            int oldWidth=Main.screenWidth,oldHeight=Main.screenHeight,oldMaxX=Main.maxTilesX,oldMaxY=Main.maxTilesY;
            int originalWidth=(int)typeof(PlayerInput).GetField("_originalScreenWidth",Flags).GetValue(null);
            int originalHeight=(int)typeof(PlayerInput).GetField("_originalScreenHeight",Flags).GetValue(null);
            var oldTiles=Main.tile;float oldGravity=player.gravDir;bool oldDetector=player.accOreFinder;
            var oldNpc=Main.npc[4];var active=Main.npc.Select(n=>n.active).ToArray();
            var oldWeapon=player.inventory[0];var oldAmmo=player.inventory[54];int oldSelected=player.selectedItem;
            int oldMouseX=Main.mouseX,oldMouseY=Main.mouseY,oldTileX=Player.tileTargetX,oldTileY=Player.tileTargetY;
            try
            {
                // Real array with unknown cells beyond the fixture's small spawn
                // area: geometry must not clip at 120 tiles. No world generation,
                // loaded-section flags, network state or owner data is involved.
                Main.maxTilesX=Main.maxTilesY=1024;Main.tile=new Tile[1024,1024];
                for(int x=0;x<oldTiles.GetLength(0);x++)for(int y=0;y<oldTiles.GetLength(1);y++)Main.tile[x,y]=oldTiles[x,y];
                foreach(var n in Main.npc)n.active=false;
                var npc=new NPC();Main.npc[4]=npc;npc.SetDefaults(3);npc.whoAmI=4;npc.active=true;
                npc.width=npc.height=10;npc.netOffset=Vector2.Zero;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,false,25,true,true));
                player.inventory[0]=new Item();player.inventory[0].SetDefaults(ItemID.FlintlockPistol);player.selectedItemState.Select(0);player.selectedItemState.Update();
                player.inventory[54]=new Item();player.inventory[54].SetDefaults(ItemID.MusketBall);player.inventory[54].stack=999;
                foreach(float raw in new[]{.3f,10f})foreach(float gravity in new[]{1f,-1f})
                {
                    Camera(1920,1080,raw);player.gravDir=gravity;
                    Main.screenPosition=new Vector2(300,400);player.Center=Main.screenPosition+new Vector2(960,540);
                    float final=Main.GameViewMatrix.RenderZoom.X;
                    Require(final==(raw<1?.296875f:10f),"native final zoom is 38/128 or 10, rather than the requested .3");
                    Pointer(context,selection,npc);
                    PlayerSelection(context,selection,type,npc,final);
                    Marker(context,host,npc);
                    Shoot(assembly,player,npc);
                    Geometry(assembly,gravity,final);
                    Console.WriteLine("PASS VIEW RANGE consumers: physical1920x1080 raw="+raw+" final="+final+" gravity="+gravity+"; native pointer/Shoot, player circle, marker preparation, TryView92/Object480 and section geometry.");
                }
                Guard(assembly);
            }
            finally
            {
                try
                {
                    Call(selection,"Clear");Call(Get(host,"Selection"),"Clear");
                    NativeCombatObservationChecks.Save(host,oldOptions);
                }
                finally
                {
                    Main.npc[4]=oldNpc;for(int i=0;i<active.Length;i++)Main.npc[i].active=active[i];
                    foreach(var shot in Main.projectile)shot.active=false;
                    Main.tile=oldTiles;Main.maxTilesX=oldMaxX;Main.maxTilesY=oldMaxY;
                    Main.GameViewMatrix=oldMatrix;Main.screenWidth=oldWidth;Main.screenHeight=oldHeight;Main.screenPosition=oldPosition;
                    player.Center=oldCenter;player.gravDir=oldGravity;player.accOreFinder=oldDetector;
                    player.inventory[0]=oldWeapon;player.inventory[54]=oldAmmo;player.selectedItemState.Select(oldSelected);player.selectedItemState.Update();
                    Main.mouseX=oldMouseX;Main.mouseY=oldMouseY;Player.tileTargetX=oldTileX;Player.tileTargetY=oldTileY;
                    typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,originalWidth);
                    typeof(PlayerInput).GetField("_originalScreenHeight",Flags).SetValue(null,originalHeight);
                }
            }
        }
        private static void Pointer(object context,object selection,NPC npc)
        {
            var input=Get(context,"Input");Call(input,"BeginUpdate");
            PlayerInput.MouseInfo=new MouseState(1800,465,0,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released,ButtonState.Released);
            var tokens=new List<string>();PlayerInput.Triggers.Reset();Call(input,"AfterNativeMouse",tokens);PlayerInput.Triggers.Update();
            PlayerInput.MouseX=1800;PlayerInput.MouseY=465;PlayerInput.UpdateMainMouse();PlayerInput.CacheZoomableValues();
            Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState();Call(input,"AfterMapping");Call(input,"AfterKeyboardRefresh");
            PlayerInput.SetZoom_World();Call(selection,"SampleMouse",input);
            Require((bool)Get(selection,"HasMouse") && (Vector2)Get(selection,"RealMouse")==Main.MouseWorld,"sample agrees with original SetZoom_World and gravity-aware MouseWorld");
            var sample=(Vector2)Get(selection,"RealMouse");
            npc.netOffset=Vector2.Zero;npc.Center=sample+new Vector2(41,0);
            Call(Get(context,"nativeNpcs"),"BeginTick");Call(selection,"Update",new ObservationOptions(path:true,mouseCenter:true,radius:2),1L,true,null);
            Require((int)Get(selection,"CandidateCount")==0,"unchanged two-tile mouse radius excludes a hitbox 36 world pixels away");
            npc.Center=sample+new Vector2(35,0);
            Call(Get(context,"nativeNpcs"),"BeginTick");Call(selection,"Update",new ObservationOptions(path:true,mouseCenter:true,radius:2),1L,true,null);
            Require((int)Get(selection,"CandidateCount")==1,"unchanged mouse radius includes a hitbox 30 world pixels away at either range endpoint");
        }
        private static void PlayerSelection(object context,object selection,Type type,NPC npc,float final)
        {
            var player=Main.LocalPlayer;
            npc.netOffset=Vector2.Zero;npc.Center=World(new Vector2(1800,465));
            var pixel=Pixel(npc.Center);
            Require(pixel.X>0 && pixel.X<1920 && pixel.Y>0 && pixel.Y<1080,"ordinary receiver lies inside final physical camera");
            float radius=(float)type.GetMethod("PlayerRadius",Flags).Invoke(null,new object[]{player.Center});
            double far=0;
            for(int i=0;i<4;i++)far=Math.Max(far,Vector2.Distance(player.Center,World(new Vector2((i&1)==0?0:1920,(i&2)==0?0:1080))));
            float expected=(float)(Math.Max(50,Math.Ceiling(far/16)+10)*16);
            Require(radius==expected,"player radius retains fifty-tile minimum and ten-tile final-camera padding");
            if(final<1)Require(Vector2.Distance(npc.Center,player.Center)>140*16 && radius>140*16,"new low endpoint exercises a visible receiver beyond historical140");
            else Require(radius==50*16,"10x does not remove original fifty-tile player selection minimum");
            Call(Get(context,"nativeNpcs"),"BeginTick");Call(selection,"Update",new ObservationOptions(path:true),1L,true,null);
            Require((int)Get(selection,"CandidateCount")==1 && ((NpcIdentity)Get(selection,"Target")).Slot==4,"player-centered real observation selects the peripheral receiver");
            // The minimum is a selection contract, not a claim that the NPC is
            // visible or that a particular weapon can reach it.
            if(final==10)
            {
                var visible=npc.Center;npc.Center=player.Center+new Vector2(700,0);
                Require(Pixel(npc.Center).X>1920,"fifty-tile positive is deliberately outside the narrow camera");
                Call(Get(context,"nativeNpcs"),"BeginTick");Call(selection,"Update",new ObservationOptions(path:true),1L,true,null);
                Require((int)Get(selection,"CandidateCount")==1,"player minimum still admits a receiver seven hundred world pixels away");
                npc.Center=visible;
            }
        }
        private static void Marker(object context,object host,NPC npc)
        {
            npc.position=new Vector2((float)Math.Floor(npc.position.X),(float)Math.Floor(npc.position.Y));npc.netOffset=new Vector2(6,4);
            Call(Get(host,"Selection"),"Choose",npc,Get(host,"Session"),false);
            var marker=Activator.CreateInstance(context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatTargetMarker",true),Flags,null,new[]{host},null);
            var zoom=Main.GameViewMatrix.ZoomMatrix;Call(marker,"Prepare",zoom,Matrix.Invert(zoom));
            Require((bool)Get(marker,"Visible"),"production marker retains the peripheral selected body at both endpoints");
            var pieces=(Array)Get(marker,"Pieces");Require(pieces.Length==6,"marker remains the constant six-sprite consumer");
            var mean=Vector2.Zero;foreach(var piece in pieces)mean+=Vector2.Transform((Vector2)Get(piece,"Position"),zoom);mean/=pieces.Length;
            var body=new Vector2((int)(npc.position.X+npc.netOffset.X)+npc.width/2,(int)(npc.position.Y+npc.netOffset.Y)+npc.height/2);
            Require(Vector2.Distance(mean,Pixel(body))<.1f,"prepared sprites surround native interpolated receiving body after the final matrix exactly once");
        }
        private static void Shoot(Assembly assembly,Player player,NPC npc)
        {
            var aim=npc.Center+npc.netOffset;int x=Main.mouseX,y=Main.mouseY,tx=Player.tileTargetX,ty=Player.tileTargetY;
            var scope=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true);
            var receipt=scope.GetMethod("BeginTileTarget",Flags).Invoke(null,new object[]{aim,true});
            try
            {
                // The lease intentionally casts native logical coordinates to
                // integers. Compare to that native precision, not ideal floats.
                Require(Vector2.Distance(Main.MouseWorld,aim)<1.5f,"cursor lease preserves world aim without a second zoom multiplication");
                if(Main.GameViewMatrix.RenderZoom.X<1)Require(Main.mouseX>1920,"valid wide logical pointer is not clipped to physical screen");
                Require(Player.tileTargetX==Math.Max(5,Math.Min(Main.maxTilesX-5,(int)(Main.MouseWorld.X/16))) && Player.tileTargetY==Math.Max(5,Math.Min(Main.maxTilesY-5,(int)(Main.MouseWorld.Y/16))),"tile target follows the same point with native world edge limits");
                foreach(var shot in Main.projectile)shot.active=false;
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(player,new object[]{0,player.HeldItem,50,false});
                var bullet=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==14);
                Require(Vector2.Dot(Vector2.Normalize(bullet.velocity),Vector2.Normalize(aim-player.MountedCenter))>.995f,"actual native pistol Shoot consumes selected world direction in either gravity");
            }
            finally{Call(receipt,"End");foreach(var shot in Main.projectile)shot.active=false;}
            Require(Main.mouseX==x && Main.mouseY==y && Player.tileTargetX==tx && Player.tileTargetY==ty,"native consumer lease returns both cursor and tile target");
        }
        private static void Geometry(Assembly assembly,float gravity,float final)
        {
            Main.screenPosition=new Vector2(8000.25f,8000.25f);Main.LocalPlayer.Center=Main.screenPosition+new Vector2(960,540);
            var tiles=NewTiles(assembly);var visible=View(tiles,0);var padded=View(tiles,92);var discovery=View(tiles,480);
            CheckBounds(visible,0);CheckBounds(padded,92);CheckBounds(discovery,480);
            Require(padded.Width<=512 && padded.Height<=512,"legal1920 low/high final camera plus real92 padding fits existing target guard");
            Require(discovery.Width<=1024 && discovery.Height<=1024,"Object480 discovery keeps its separate1024 guard");
            Main.LocalPlayer.accOreFinder=true;
            var target=(IWorldTargetSource)Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.WorldTargets.WorldTargetHostObservation",true),Flags,null,new object[]{(Func<bool>)(()=>true),tiles},null);
            WorldTargetView targetView;Require(target.TryBegin(out targetView) && Same(targetView,padded),"actual target adapter forwards92 world-pixel padding");
            var objects=(IWorldObjectSource)Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.WorldObjectText.WorldObjectHostObservation",true),Flags,null,new[]{tiles},null);
            WorldObjectView objectView;Require(objects.TryBegin(false,false,out objectView) && Same(objectView.Visible,visible) && Same(objectView.Discovery,discovery),"actual object adapter keeps unpadded visibility separate from480 discovery");
            Require(!((WorldTargetTile)Call(tiles,"Read",900,900)).Readable,"unknown native tile remains unknown rather than synthesized air");
            Sections();
            Console.WriteLine("VIEW RANGE geometry final="+final+" gravity="+gravity+" visible="+visible.Width+"x"+visible.Height+" +92="+padded.Width+"x"+padded.Height+" +480="+discovery.Width+"x"+discovery.Height);
        }
        private static void Guard(Assembly assembly)
        {
            var zoom=assembly.GetType("JueMingR.TerrariaHost.View.ViewZoom",true);
            float minimum=(float)zoom.GetMethod("MinimumTarget",Flags).Invoke(null,new object[]{3840,2160,1f});
            Require(minimum>.3f && minimum<=1,"actual larger physical span raises the target minimum instead of enlarging the512 limit");
            Main.LocalPlayer.gravDir=1;Main.screenPosition=new Vector2(8000.25f,8000.25f);
            Camera(3840,2160,minimum);var guarded=View(NewTiles(assembly),92);
            Require(guarded.Width<=512 && guarded.Height<=512,"production MinimumTarget -> native quantized matrix -> actual TryView92 respects512");
            // Search only native geometry near the exact guard boundary. These
            // artificial widths test a component limit, not device support or a
            // product minimum. Reads still go through the real unknown tile array.
            Main.LocalPlayer.gravDir=1;Main.screenPosition=new Vector2(8000.25f,8000.25f);
            bool accepted=false,rejected=false;
            for(int width=2300;width<=2450 && !(accepted && rejected);width++)
            {
                Camera(width,1080,.3f);var tiles=NewTiles(assembly);
                var source=new CountedSource((IWorldTargetSource)Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.WorldTargets.WorldTargetHostObservation",true),Flags,null,new object[]{(Func<bool>)(()=>true),tiles},null));
                WorldTargetView view;Require(source.TryBegin(out view),"native guard fixture has a valid view");
                if(view.Width!=512 && view.Width!=513)continue;
                var feature=new WorldTargetFeature(source);feature.Configure(WorldTargetSettings.Default.WithEnabled(WorldTargetKind.LifeCrystal,true));feature.OnSessionStarted();feature.Update(0);
                if(view.Width==512){Require(source.Reads>0,"actual512-wide native view is admitted by production feature");accepted=true;}
                else{Require(source.Reads==0 && feature.Targets.Count==0,"actual513-wide native view fails closed before tile reads");rejected=true;}
            }
            Require(accepted && rejected,"native inverse geometry exercised both exact512 and513 target boundaries");
            Console.WriteLine("PASS VIEW RANGE guard: real native TryView92 -> production512 accept /513 zero-read rejection; no guard or request budget enlarged.");
        }
        private static void Sections()
        {
            // Same public Camera and native section lookup consumed by the
            // requester. This is geometry-only: no fake client state, SendData,
            // loaded flag, ACK or topology claim. Original TCP evidence is separate.
            var p=Main.Camera.ScaledPosition;var size=Main.Camera.ScaledSize;
            int left=Netplay.GetSectionX(Math.Max(0,(int)Math.Floor(p.X/16)-2));
            int top=Netplay.GetSectionY(Math.Max(0,(int)Math.Floor(p.Y/16)-2));
            int right=Netplay.GetSectionX(Math.Min(Main.maxTilesX-1,(int)Math.Ceiling((p.X+size.X)/16)+2));
            int bottom=Netplay.GetSectionY(Math.Min(Main.maxTilesY-1,(int)Math.Ceiling((p.Y+size.Y)/16)+2));
            for(int i=0;i<4;i++)
            {
                var corner=World(new Vector2((i&1)==0?0:1920,(i&2)==0?0:1080));
                int sx=Netplay.GetSectionX((int)Math.Floor(corner.X/16)),sy=Netplay.GetSectionY((int)Math.Floor(corner.Y/16));
                Require(sx>=left && sx<=right && sy>=top && sy<=bottom,"camera plus two-tile request halo covers independently inverted physical corner");
            }
            Require(Netplay.GetSectionX(199)==0 && Netplay.GetSectionX(200)==1 && Netplay.GetSectionY(149)==0 && Netplay.GetSectionY(150)==1,"locked native section edge units remain200x150 tiles");
        }
        private static object NewTiles(Assembly assembly)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.World.WorldTileObservation",true),Flags,null,new object[]{(Func<bool>)(()=>true)},null);}
        private static WorldTargetView View(object tiles,int padding)
        {object[] args={padding,default(WorldTargetView)};Require((bool)Call(tiles,"TryView",args),"real inverse camera supplies tile view");return (WorldTargetView)args[1];}
        private static bool Same(WorldTargetView a,WorldTargetView b)
        {return a.X==b.X && a.Y==b.Y && a.Width==b.Width && a.Height==b.Height;}
        private static void CheckBounds(WorldTargetView view,int padding)
        {
            var a=World(Vector2.Zero);var b=World(new Vector2(1920,1080));
            int left=Math.Max(0,(int)Math.Floor((Math.Min(a.X,b.X)-padding)/16));
            int top=Math.Max(0,(int)Math.Floor((Math.Min(a.Y,b.Y)-padding)/16));
            int right=Math.Min(Main.maxTilesX,(int)Math.Ceiling((Math.Max(a.X,b.X)+padding)/16));
            int bottom=Math.Min(Main.maxTilesY,(int)Math.Ceiling((Math.Max(a.Y,b.Y)+padding)/16));
            Require(view.X==left && view.Y==top && view.Right==right && view.Bottom==bottom,"actual tile view equals independent axis-wise native matrix inversion plus world padding");
        }
        private static Vector2 World(Vector2 physical)
        {
            var m=Main.GameViewMatrix.ZoomMatrix;
            var local=new Vector2((physical.X-m.M41)/m.M11,(physical.Y-m.M42)/m.M22);
            if(Main.LocalPlayer.gravDir<0)local.Y=Main.screenHeight-local.Y;
            return Main.screenPosition+local;
        }
        private static Vector2 Pixel(Vector2 world)
        {
            var p=world-Main.screenPosition;if(Main.LocalPlayer.gravDir<0)p.Y=Main.screenHeight-p.Y;
            var m=Main.GameViewMatrix.ZoomMatrix;return new Vector2(p.X*m.M11+m.M41,p.Y*m.M22+m.M42);
        }
        private static void Camera(int width,int height,float raw)
        {
            Main.screenWidth=width;Main.screenHeight=height;
            Main.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(null);Main.GameViewMatrix.SetViewportOverride(new Viewport(0,0,width,height));Main.GameViewMatrix.Zoom=new Vector2(raw);
            typeof(PlayerInput).GetField("_originalScreenWidth",Flags).SetValue(null,width);typeof(PlayerInput).GetField("_originalScreenHeight",Flags).SetValue(null,height);
        }
        private sealed class CountedSource : IWorldTargetSource
        {
            private readonly IWorldTargetSource source;internal int Reads;
            internal CountedSource(IWorldTargetSource source){this.source=source;}
            public bool TryBegin(out WorldTargetView view){return source.TryBegin(out view);}
            public WorldTargetTile Read(int x,int y){Reads++;return source.Read(x,y);}
        }
    }
}
