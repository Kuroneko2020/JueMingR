using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Utilities;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // A mechanism reconstruction, not a replay of the field log. The ordinary
    // parent, Capture, worker and cache continue at the existing paced seam.
    // Default mode does not seed dependencies. The separate prefetch mode is
    // explicitly a test-only upper bound, never an automatic-discovery PASS.
    // No mode waits for a particular reply or freezes the parent world.
    internal static class NativeCombatQueryRepairChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output,int minimumFuture=120)
        {
            if(Environment.GetEnvironmentVariable("JUEMINGR_QUERY_LEAF_ORACLE")=="1"){LeafOracle(context,output);return;}
            var host=Get(context,"CombatObservation");var selection=Get(host,"Selection");
            var worker=Get(native,"Worker");int workerId=((System.Diagnostics.Process)Get(worker,"child")).Id;
            int type=int.Parse(Environment.GetEnvironmentVariable("JUEMINGR_QUERY_TYPE")??"176");
            int backgrounds=int.Parse(Environment.GetEnvironmentVariable("JUEMINGR_QUERY_BACKGROUNDS")??"32");
            if(backgrounds<0 || backgrounds>48 || !new[]{1,110,176,177}.Contains(type))throw new InvalidOperationException("Bounded query scene required.");
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
            Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
            Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            string prefetch=Environment.GetEnvironmentVariable("JUEMINGR_QUERY_PREFETCH");
            if(prefetch!=null && prefetch!="baseline" && prefetch!="ideal")throw new InvalidOperationException("Unknown prefetch experiment.");
            if(prefetch!=null)
            {
                // A paired feasibility experiment must not inherit the warm
                // player's regen/buff clocks. Only this initial scene reset
                // is allowed; natural state then runs through all 945 updates.
                Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
                Main.dayTime=false;Main.time=1800;Main.dayRate=1;
            }
            var player=Main.LocalPlayer;
            player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
            player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.dead=false;
            player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;
            player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            player.statLife=player.statLifeMax=player.statLifeMax2=500;player.immune=false;player.immuneTime=0;
            Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
            Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
            for(int i=0;i<10;i++)player.armor[i].TurnToAir();
            // Plain defensive equipment keeps the bounded encounter alive;
            // it has no guardian, dodge or long immunity. Native damage and
            // poison still run and are part of the compared input.
            for(int i=0;i<3;i++)player.armor[i].SetDefaults(696+i);
            typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
            int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,type,Start:64,Target:Main.myPlayer);
            var npc=Main.npc[target];if(npc.noGravity)npc.position=new Vector2(1400,2140);
            var towns=new List<NPC>();
            for(int i=0;i<backgrounds;i++)
            {
                var town=new NPC();town.SetDefaults(678);town.whoAmI=i;town.active=true;
                town.position=new Vector2(3000+i*30,2400-town.height);Main.npc[i]=town;towns.Add(town);
            }
            // A second real target remains in the same world and is advanced
            // throughout. Switching the mouse is the only selection stimulus.
            int other=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),700,2400,1,Start:65,Target:Main.myPlayer);
            File.WriteAllLines(Path.Combine(output,"query-initial.csv"),new[]{"mode,tick,dayTime,time,life,lifeRegen,lifeRegenCount,lifeRegenTime,targetType,backgrounds",Csv(prefetch??"ordinary",Main.GameUpdateCount,Main.dayTime,Main.time,player.statLife,player.lifeRegen,player.lifeRegenCount,player.lifeRegenTime,type,backgrounds)});
            NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
            Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            string[] phases={"natural","switch-away","return-target","simple","off","on-simple"};
            int[] durations={240,45,240,180,60,180};
            var rows=new List<string>{"phase,frame,tick,target,selected,published,capture,count,lastTick,pending,accepted,failed,reason,playerLife,immune,immuneTime,playerX,playerY,playerVX,playerVY,npcX,npcY,npcLife,ai0,ai1,ai2,ai3,fullNpcs,fullProjectiles,npcUpdates,projectileUpdates,selectedRngBefore,selectedRngAfter"};
            var summaries=new List<string>{"phase,startTick,endTick,updates,selected,published,longestBlank,firstPublished,requests,received,rejected,refused,carryIn,carryOut,npcUpdates,projectileUpdates,playerLife"};
            bool finished=false;int globalFrame=0;
            using(var trace=new NativeCombatAttackTrace(native,output))
            using(var prefetchProbe=prefetch==null?null:new NativeCombatPrefetchChecks(native,trace,npc,towns.Concat(new[]{npc,Main.npc[other]}),output,prefetch=="ideal"))
            try
            {
                for(int axis=0;axis<phases.Length;axis++)
                {
                    if(axis==3){npc.active=false;foreach(var town in towns)town.active=false;}
                    if(axis==4)NativeCombatObservationChecks.Save(host,new ObservationOptions());
                    if(axis==5)NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
                    int selectedSlot=axis==1 || axis>=3?other:target;var selectedNpc=Main.npc[selectedSlot];
                    trace.Phase=phases[axis];trace.Selected=selectedSlot;trace.Frame=-1;
                    long requests=(long)Get(native,"Requests"),received=trace.Received,rejected=(long)Get(native,"Rejected"),refused=(long)Get(native,"Refused"),ns=trace.NpcUpdates,ps=trace.ProjectileUpdates;
                    object carry=Tick(Get(native,"pending"));ulong start=Main.GameUpdateCount;
                    int shown=0,selectedCount=0,blank=0,longest=0,first=-1,updates=0;
                    for(int frame=0;frame<durations[axis];frame++,globalFrame++)
                    {
                        trace.Frame=frame;
                        // Ordinary short walking inputs invalidate old premises
                        // naturally. No position, AI, life or immunity is reset.
                        int motion=globalFrame%240;
                        player.controlRight=motion>=150 && motion<180;player.controlLeft=motion>=210;
                        NativeCombatModeledImpactChecks.SampleMouse(context,selectedNpc.Center);step();updates++;
                        if(trace.Fault!=null)throw new InvalidOperationException("Original Prepare failed.",trace.Fault);
                        Require(ReferenceEquals(worker,Get(native,"Worker")) && ((System.Diagnostics.Process)Get(worker,"child")).Id==workerId,"One worker must span the entire scene.");
                        Require(!(bool)Get(native,"Failed") && !(bool)Get(host,"pathFailed"),"The ordinary Session must remain healthy.");
                        Require(!player.dead && selectedNpc.active,"The natural player and selected target must remain alive; preserve partial evidence if this scene ends.");
                        bool selected=(bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==selectedSlot;
                        if(selected)selectedCount++;
                        var path=cache.Read(0);bool present=path!=null;
                        if(present)
                        {
                            Require(selected && ReferenceEquals(path.Identity.Token,selectedNpc) && path.SampleTick==Main.GameUpdateCount && path.Count>=minimumFuture+1 && path.Count<=121,"Only the current selected instance with its real required future counts.");
                            shown++;blank=0;if(first<0)first=frame;
                        }
                        else if(selected)longest=Math.Max(longest,++blank);else blank=0;
                        if(axis==4)Require(!present && (long)Get(native,"Requests")==requests,"OFF must not capture or publish.");
                        rows.Add(Csv(trace.Phase,frame,Main.GameUpdateCount,selectedSlot,selected,present,path?.CaptureTick,path?.Count,path==null?null:(object)(path.SampleTick+path.Count-1),Tick(Get(native,"pending")),Tick(Get(native,"acceptedRequest")),Get(native,"Failed"),Get(native,"Reason"),player.statLife,player.immune,player.immuneTime,player.position.X,player.position.Y,player.velocity.X,player.velocity.Y,selectedNpc.position.X,selectedNpc.position.Y,selectedNpc.life,selectedNpc.ai[0],selectedNpc.ai[1],selectedNpc.ai[2],selectedNpc.ai[3],Get(Get(native,"npcs"),"Count"),Get(Get(native,"projectiles"),"Count"),trace.NpcUpdates,trace.ProjectileUpdates,trace.SelectedRngBefore,trace.SelectedRngAfter));
                    }
                    summaries.Add(Csv(trace.Phase,start+1,Main.GameUpdateCount,updates,selectedCount,shown,longest,first,(long)Get(native,"Requests")-requests,trace.Received-received,(long)Get(native,"Rejected")-rejected,(long)Get(native,"Refused")-refused,carry,Tick(Get(native,"pending")),trace.NpcUpdates-ns,trace.ProjectileUpdates-ps,player.statLife));
                    Console.WriteLine("QUERY "+trace.Phase+" selected="+selectedCount+" published="+shown+"/"+updates+" longest="+longest+" playerLife="+player.statLife);
                }
                finished=true;
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output,"query-updates.csv"),rows);File.WriteAllLines(Path.Combine(output,"query-summary.csv"),summaries);
                File.WriteAllText(Path.Combine(output,"query-status.txt"),(finished?"complete mechanism reconstruction":"partial or invalid reconstruction")+"; worker="+workerId+"; no field/gameplay PASS implied");
            }
        }
        private static void LeafOracle(object context,string output)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();
            var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);
            player.controlLeft=player.controlRight=player.controlJump=false;player.velocity=Vector2.Zero;
            player.immune=false;player.immuneTime=0;Main.netMode=0;
            var town=new NPC();town.SetDefaults(678);town.whoAmI=1;town.active=true;
            town.position=new Vector2(3200,2400-town.height);Main.npc[1]=town;
            for(int x=199;x<=204;x++)for(int y=145;y<150;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
            typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdateNPCs",new UnifiedRandom(879)}});
            Require(!town.stinky && !town.buffType.Contains(120),"The negative leaf begins with no stinky flag or pending stink buff.");
            string before,after;
            using(Main.SwapRandom("UpdateNPCs"))
            {
                before=NativeCombatWorkerChecks.RandomStamp();
                town.UpdateNPC(town.whoAmI);
                after=NativeCombatWorkerChecks.RandomStamp();
            }
            File.WriteAllLines(Path.Combine(output,"query-leaf-rng.csv"),new[]{"path,stinky,wet,ai0,ai1,rng",Csv("skip-update",false,false,0,0,before),Csv("original-update",town.stinky,town.wet,town.ai[0],town.ai[1],after)});
            Require(!town.stinky && town.wet,"The original wet town still satisfies the same false leaf answer.");
            Require(before!=after,"The single omitted original NPC update must expose the shared RNG counterexample.");
            Console.WriteLine("QUERY-LEAF hypothesis disproved: stinky=false before/after, but one original UpdateNPC changes the complete UpdateNPCs random state; no production shortcut installed.");
        }
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
