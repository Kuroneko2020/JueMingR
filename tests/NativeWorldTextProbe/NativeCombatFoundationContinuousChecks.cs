using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // C uses the existing locked-original Update loop. Every frozen trajectory
    // is captured BEFORE the following player/NPC actions; refreshed windows
    // cannot retrospectively supply a better prediction for that action.
    internal static class NativeCombatFoundationContinuousChecks
    {
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output)
        {
            NativeCombatLiveContextChecks.FlightWorld();foreach(var npc in Main.npc)npc.active=false;foreach(var projectile in Main.projectile)projectile.active=false;
            Main.tileSolid[TileID.Stone]=true;Main.tileRope[TileID.Rope]=true;Main.worldSurface=140;Main.dayTime=false;
            Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            var host=Get(context,"CombatObservation");Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;
            p.position=new Vector2(900,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.controlLeft=p.controlRight=p.controlUp=p.controlDown=p.controlJump=false;
            p.armor[3].SetDefaults(54);p.armor[4].SetDefaults(4404);p.statLife=p.statLifeMax=p.statLifeMax2=400;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),650,2100,2,Start:16,Target:p.whoAmI);var n=Main.npc[slot];
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true,mouseCenter:true,clearLine:false,radius:25));
            if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_SINGLE")=="1")
            {
                p.controlRight=true;
                void State(string label){Console.WriteLine("C SINGLE "+label+" player="+p.whoAmI+" active="+p.active+" dead="+p.dead+" film="+p.isControlledByFilm+" right="+p.controlRight+" life="+p.statLife+" pos="+p.position+" vel="+p.velocity+" npcSlot="+n.whoAmI+" npcType="+n.type+" npcStyle="+n.aiStyle+" npcActive="+n.active+" npcFriendly="+n.friendly+" npcImmortal="+n.immortal+" dontTake="+n.dontTakeDamage+" npcLife="+n.life+" npcPos="+n.position+" npcV="+n.velocity+" npcTarget="+n.target+" required="+cache.Required+" selected="+Get(Get(host,"Selection"),"HasTarget")+" runtime="+Get(Get(context,"Runtime"),"SharedRuntime")+" menu="+Main.gameMenu+" pause="+Main.gamePaused+" net="+Main.netMode);}
                State("before");NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);Console.WriteLine("C SINGLE mouse="+Get(Get(host,"Selection"),"HasMouse")+" point="+Get(Get(host,"Selection"),"RealMouse")+" focused="+Get(Get(context,"Input"),"SampleFocused")+" options="+Get(host,"Options"));step();State("after");Console.WriteLine("C SINGLE afterMouse="+Get(Get(host,"Selection"),"HasMouse"));NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();State("after-next-sample");return;
            }
            NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
            var rows=new List<string>{"phase,frame,tick,selected,published,count,stop,assumptions,grapCount,pulley,wet,down,playerX,playerY,npcX,npcY,npcType,npcVy,npcCollideY,frozenTick,frozenHorizon,frozenX,frozenY,errorX,errorY,frozenError"};
            var summary=new List<string>{"phase,frames,selected,published,current120,complexObserved,maxFrozenError,lastPlayerX,lastPlayerY"};
            int actions=0;
            void Window(string name,int frames)
            {
                // Refresh the completed observation after a scripted input or
                // legal mechanism transition, before executing another action.
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);
                Require(frozen!=null && frozen.Count>frames && ReferenceEquals(frozen.Identity.Token,n),"C a real current-instance future must be frozen before the action window.");
                int selected=0,published=0,full=0,complex=0;double maxError=0;
                for(int frame=1;frame<=frames;frame++)
                {
                    NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();actions++;var path=cache.Read(0);bool has=(bool)Get(Get(host,"Selection"),"HasTarget");if(has)selected++;if(path!=null){published++;if(path.Count==121)full++;if((path.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0)complex++;}
                    double error=double.NaN,fx=double.NaN,fy=double.NaN,ex=double.NaN,ey=double.NaN;
                    if(frozen!=null && frame<frozen.Count && frozen.Identity.Token==n)
                    {var predicted=frozen[frame].Bounds;fx=predicted.X;fy=predicted.Y;ex=fx-n.position.X;ey=fy-n.position.Y;error=Math.Sqrt(ex*ex+ey*ey);maxError=Math.Max(maxError,error);}
                    rows.Add(string.Join(",",name,frame,Main.GameUpdateCount,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",(int)(path?.Assumptions??PredictionAssumption.None),p.grapCount,p.pulley,p.wet,p.controlDown,F(p.position.X),F(p.position.Y),F(n.position.X),F(n.position.Y),n.type,F(n.velocity.Y),n.collideY,frozen?.CaptureTick??-1,frozen?.Count-1??0,F(fx),F(fy),F(ex),F(ey),F(error)));
                }
                summary.Add(string.Join(",",name,frames,selected,published,full,complex,F(maxError),F(p.position.X),F(p.position.Y)));
                Console.WriteLine("C FROZEN "+summary[summary.Count-1]);
                Require(published>0,"C actual continuous Source must publish a future for "+name);
            }
            try
            {
                bool fighterOnly=Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_FIGHTER_ONLY")=="1";
                if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_GRAVITY_ONLY")!="1" && !fighterOnly)
                {
                p.controlRight=true;Window("dry-float-boots-fast-run",120);Require(p.canFloatInWater && !p.wet && p.velocity.X>3,"C original full Player.Update supplies equipped dry fast-running state.");
                p.controlRight=false;p.controlLeft=true;Window("reverse",60);
                p.controlLeft=false;Window("release-friction",30);
                var hook=Main.projectile[4];hook.SetDefaults(13);hook.whoAmI=4;hook.active=true;hook.owner=p.whoAmI;hook.ai[0]=2;hook.position=new Vector2(p.Center.X+100,2400);hook.velocity=Vector2.Zero;p.grappling[0]=4;p.grapCount=1;
                Window("grapple-pull-attached",24);Require(p.grapCount>0,"C original attached hook remains owned and active.");p.controlJump=true;p.releaseJump=true;Window("grapple-release",12);Require(p.grapCount==0,"C original jump releases grapple.");p.controlJump=false;
                for(int y=120;y<150;y++){Main.tile[60,y].active(true);Main.tile[60,y].type=TileID.Rope;}
                p.position=new Vector2(60*16+8-p.width/2,2240);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.pulley=true;p.pulleyDir=2;p.controlLeft=p.controlRight=false;
                Window("rope-idle",12);Require(p.pulley,"C original rope remains attached while idle.");float ropeY=p.position.Y;p.controlUp=true;Window("rope-move",24);Require(p.pulley && p.position.Y<ropeY,"C original rope movement changes position without ordinary horizontal controls.");p.controlUp=false;p.controlJump=true;p.releaseJump=true;Window("rope-exit",12);Require(!p.pulley,"C original jump exits rope.");p.controlJump=false;
                for(int x=50;x<75;x++)for(int y=146;y<150;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                p.pulley=false;p.position=new Vector2(900,2300);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.controlDown=false;Window("float-enter-surface",36);Require(p.wet && p.canFloatInWater,"C original equipped player really reaches water.");
                p.controlDown=true;Window("float-down",18);p.controlDown=false;p.position=new Vector2(1300,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;Window("float-exit",18);
                }
                else{p.position=new Vector2(1300,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;}
                if(!fighterOnly)
                {
                n.SetDefaults(258);n.whoAmI=slot;n.active=true;n.target=p.whoAmI;n.position=new Vector2(1600,2310);n.velocity=new Vector2(-1,9);n.ai[3]=1;
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);NativeCombatObservationChecks.Fresh(context,host);var falling=cache.Read(0);
                Require(falling!=null && falling.Count==121 && Math.Abs(falling[1].Vy-3.1f)<.0001f,"C actual Source frozen258 first step preserves pre-AI public clip and terminal gravity.");
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();Require(Math.Abs(n.velocity.Y-3.1f)<.0001f,"C original UpdateNPC confirms the same clipped first fall step.");
                Console.WriteLine("C GRAVITY FIRST sourceCapture="+falling.CaptureTick+" frozenVy1="+falling[1].Vy+" actualVy1="+n.velocity.Y+" phase="+n.ai[2]);
                Window("gravity258-fall-land",48);Require(n.collideY && n.velocity.Y==0,"C original special-gravity actor actually reaches its ground phase.");
                n.position.Y=2320;n.velocity.Y=-2;n.oldVelocity=n.velocity;Window("gravity258-rise-fall",48);
                }
                else
                {
                    n.SetDefaults(104);n.whoAmI=slot;n.active=true;n.target=p.whoAmI;n.position=new Vector2(700,2400-n.height);n.oldPosition=n.position-new Vector2(2,0);n.velocity=new Vector2(2,0);n.direction=n.spriteDirection=1;n.ai[3]=0;
                    Window("fighter104-speed2",120);Require(n.velocity.X==2,"C original ordinary fighter retains its legal speed above old invented1.5.");
                    p.position.X=400;Window("fighter104-reverse",30);Require(n.direction==-1 && n.velocity.X<0,"C original fighter reverses under the new actual numbered-player position.");
                    n.velocity=new Vector2(-7,0);n.oldVelocity=n.velocity;Window("fighter104-overspeed-brake",30);Require(n.velocity.X==-2,"C original true ground overspeed brake converges to the real speed2 threshold.");
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions());step();Require(cache.Required==0 && cache.Read(0)==null,"C OFF retires publication after actual native actions.");
                Console.WriteLine("PASS C actual full Player.Update / NPC.UpdateNPC / shared Host frozen-action denominator="+actions+". Errors are conditional model limits, not an exact 120-tick physics claim.");
            }
            finally{File.WriteAllLines(Path.Combine(output,"foundation-continuous.csv"),rows);File.WriteAllLines(Path.Combine(output,"foundation-continuous-summary.csv"),summary);}
        }
        private static string F(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
    }
}
