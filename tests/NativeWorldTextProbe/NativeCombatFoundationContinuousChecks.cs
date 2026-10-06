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
            var host=Get(context,"CombatObservation");Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;
            p.position=new Vector2(900,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.controlLeft=p.controlRight=p.controlUp=p.controlDown=p.controlJump=false;
            p.armor[3].SetDefaults(54);p.armor[4].SetDefaults(4404);p.statLife=p.statLifeMax=p.statLifeMax2=400;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),650,2100,2,Start:16,Target:p.whoAmI);var n=Main.npc[slot];
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,marker:true,mouseCenter:true,clearLine:false,radius:25));
            if(Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_SINGLE")=="1")
            {
                p.controlRight=true;
                void State(string label){Console.WriteLine("C SINGLE "+label+" player="+p.whoAmI+" active="+p.active+" dead="+p.dead+" film="+p.isControlledByFilm+" right="+p.controlRight+" life="+p.statLife+" pos="+p.position+" vel="+p.velocity+" npcSlot="+n.whoAmI+" npcType="+n.type+" npcStyle="+n.aiStyle+" npcActive="+n.active+" npcFriendly="+n.friendly+" npcImmortal="+n.immortal+" dontTake="+n.dontTakeDamage+" npcLife="+n.life+" npcPos="+n.position+" npcV="+n.velocity+" npcTarget="+n.target+" required="+cache.Required+" selected="+Get(Get(host,"Selection"),"HasTarget")+" runtime="+Get(Get(context,"Runtime"),"SharedRuntime")+" menu="+Main.gameMenu+" pause="+Main.gamePaused+" net="+Main.netMode);}
                State("before");NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();State("after");return;
            }
            NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
            var rows=new List<string>{"phase,frame,tick,selected,published,count,stop,assumptions,grapCount,pulley,wet,down,playerX,playerY,npcX,npcY,frozenTick,frozenHorizon,frozenError"};
            var summary=new List<string>{"phase,frames,selected,published,current120,complexObserved,maxFrozenError,lastPlayerX,lastPlayerY"};
            int actions=0;
            void Window(string name,int frames)
            {
                // Refresh the completed observation after a scripted input or
                // legal mechanism transition, before executing another action.
                NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);NativeCombatObservationChecks.Fresh(context,host);var frozen=cache.Read(0);
                int selected=0,published=0,full=0,complex=0;double maxError=0;
                for(int frame=1;frame<=frames;frame++)
                {
                    NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();actions++;var path=cache.Read(0);bool has=(bool)Get(Get(host,"Selection"),"HasTarget");if(has)selected++;if(path!=null){published++;if(path.Count==121)full++;if((path.Assumptions&PredictionAssumption.CurrentPlayerObservation)!=0)complex++;}
                    double error=double.NaN;
                    if(frozen!=null && frame<frozen.Count && frozen.Identity.Token==n)
                    {var predicted=frozen[frame].Bounds;error=Math.Sqrt(Math.Pow(predicted.X-n.position.X,2)+Math.Pow(predicted.Y-n.position.Y,2));maxError=Math.Max(maxError,error);}
                    rows.Add(string.Join(",",name,frame,Main.GameUpdateCount,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",(int)(path?.Assumptions??PredictionAssumption.None),p.grapCount,p.pulley,p.wet,p.controlDown,F(p.position.X),F(p.position.Y),F(n.position.X),F(n.position.Y),frozen?.CaptureTick??-1,frozen?.Count-1??0,F(error)));
                }
                summary.Add(string.Join(",",name,frames,selected,published,full,complex,F(maxError),F(p.position.X),F(p.position.Y)));
                Console.WriteLine("C FROZEN "+summary[summary.Count-1]);
                Require(published>0,"C actual continuous Source must publish a future for "+name);
            }
            try
            {
                p.controlRight=true;Window("dry-float-boots-fast-run",120);Require(p.canFloatInWater && !p.wet && p.velocity.X>3,"C original full Player.Update supplies equipped dry fast-running state.");
                p.controlRight=false;p.controlLeft=true;Window("reverse",60);
                p.controlLeft=false;Window("release-friction",30);
                var hook=Main.projectile[4];hook.SetDefaults(13);hook.whoAmI=4;hook.active=true;hook.owner=p.whoAmI;hook.ai[0]=2;hook.position=new Vector2(p.Center.X+100,2400);hook.velocity=Vector2.Zero;p.grappling[0]=4;p.grapCount=1;
                Window("grapple-pull-attached",24);p.controlJump=true;p.releaseJump=true;Window("grapple-release",12);p.controlJump=false;
                for(int y=120;y<150;y++){Main.tile[60,y].active(true);Main.tile[60,y].type=TileID.Rope;}
                p.position=new Vector2(60*16+8-p.width/2,2240);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.pulley=true;p.pulleyDir=2;p.controlLeft=p.controlRight=false;
                Window("rope-idle",12);p.controlUp=true;Window("rope-move",24);p.controlUp=false;p.controlJump=true;p.releaseJump=true;Window("rope-exit",12);p.controlJump=false;
                for(int x=50;x<75;x++)for(int y=146;y<150;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                p.pulley=false;p.position=new Vector2(900,2300);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;p.controlDown=false;Window("float-enter-surface",36);
                p.controlDown=true;Window("float-down",18);p.controlDown=false;p.position=new Vector2(1300,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.velocity=Vector2.Zero;Window("float-exit",18);
                NativeCombatObservationChecks.Save(host,new ObservationOptions());step();Require(cache.Required==0 && cache.Read(0)==null,"C OFF retires publication after actual native actions.");
                Console.WriteLine("PASS C actual full Player.Update / NPC.UpdateNPC / shared Host frozen-action denominator="+actions+". Errors are conditional model limits, not an exact 120-tick physics claim.");
            }
            finally{File.WriteAllLines(Path.Combine(output,"foundation-continuous.csv"),rows);File.WriteAllLines(Path.Combine(output,"foundation-continuous-summary.csv"),summary);}
        }
        private static string F(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
    }
}
