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
    // Investigation only: one target/world/Session/worker across each change.
    // Existing production retirement and page-learning decisions are observed,
    // never replaced. The parent advances at the existing original-update seam.
    internal static class NativeCombatHostileQueryChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            var host=Get(context,"CombatObservation");var selection=Get(host,"Selection");
            var worker=Get(native,"Worker");var child=(System.Diagnostics.Process)Get(worker,"child");int workerId=child.Id;
            var rows=new List<string>{"phase,frame,tick,background,selected,published,capture,count,pending,accepted,failed,reason,workerId,playerLife,immune,immuneTime,playerX,playerY,npcX,npcY,npcLife,canHit,activeProjectiles,fullNpcs,fullProjectiles,npcUpdates,projectileUpdates"};
            var summaries=new List<string>{"phase,startTick,endTick,updates,selected,published,longestBlank,firstPublished,requests,received,rejected,refused,carryIn,carryOut,npcUpdates,projectileUpdates"};
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
            Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();
            Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            var player=Main.LocalPlayer;player.controlLeft=player.controlRight=player.controlUp=player.controlDown=player.controlJump=false;
            player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;player.dead=false;
            player.wet=player.honeyWet=player.lavaWet=player.shimmerWet=false;player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            // More starting life prevents scene truncation; native Hurt,
            // immunity, knockback and every resulting retirement remain real.
            player.statLife=player.statLifeMax=player.statLifeMax2=100000;player.immune=false;player.immuneTime=0;
            Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
            Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);
            for(int i=0;i<10;i++)player.armor[i].TurnToAir();
            typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
            int target=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,110,Start:16,Target:Main.myPlayer);var n=Main.npc[target];n.life=n.lifeMax=100000;
            var towns=new List<NPC>();
            string[] phases={"far-0-normal","far-8-normal","far-32-normal","far-32-immune","far-32-teleport-immune","return-0-immune"};
            int[] counts={0,8,32,32,32,0},durations={360,600,720,600,300,360};bool finished=false;
            // The single follow-up records source-reset causes with no Hurt
            // or prior learned NPC pages; it is not another size matrix.
            if(Environment.GetEnvironmentVariable("JUEMINGR_HOSTILE_ISOLATED32")=="1")
            {phases=new[]{"isolated-32-immune"};counts=new[]{32};durations=new[]{900};player.immune=true;player.immuneTime=100000;}
            NativeCombatObservationChecks.Save(host,new ObservationOptions(collision:true,path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
            Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
            using(var trace=new NativeCombatAttackTrace(native,output))
            try
            {
                trace.Selected=target;
                for(int axis=0;axis<phases.Length;axis++)
                {
                    trace.Phase=phases[axis];trace.Frame=-1;
                    while(towns.Count<counts[axis])
                    {
                        int ordinal=towns.Count,slot=ordinal<target?ordinal:ordinal+1;
                        var town=new NPC();town.SetDefaults(678);town.whoAmI=slot;town.active=true;town.position=new Vector2(3000+ordinal*30,2400-town.height);Main.npc[slot]=town;towns.Add(town);
                    }
                    if(counts[axis]==0)foreach(var town in towns)town.active=false;
                    if(axis==3){player.immune=true;player.immuneTime=100000;Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);}
                    // A genuine original relocation isolates page relearning
                    // from damage. It invalidates results, never permits reuse.
                    if(axis==4)player.Teleport(player.position+new Vector2(8,0),0);
                    long requestStart=(long)Get(native,"Requests"),receiveStart=trace.Received,rejectStart=(long)Get(native,"Rejected"),refuseStart=(long)Get(native,"Refused"),npcStart=trace.NpcUpdates,projectileStart=trace.ProjectileUpdates;
                    object carryIn=Tick(Get(native,"pending"));ulong start=Main.GameUpdateCount;int shown=0,selectedCount=0,blank=0,longest=0,first=-1;
                    for(int frame=0;frame<durations[axis];frame++)
                    {
                        trace.Frame=frame;long ns=trace.NpcUpdates,ps=trace.ProjectileUpdates;
                        NativeCombatModeledImpactChecks.SampleMouse(context,n.Center);step();
                        Require(ReferenceEquals(worker,Get(native,"Worker")) && ReferenceEquals(child,Get(worker,"child")) && child.Id==workerId,"The same worker owns every phase.");
                        if(trace.Fault!=null)throw new InvalidOperationException("Original Prepare failed.",trace.Fault);
                        Require(!(bool)Get(native,"Failed") && !(bool)Get(host,"pathFailed") && !player.dead && n.active,"Continuous evidence keeps a healthy Session and live target/player.");
                        bool selected=(bool)Get(selection,"HasTarget") && ((NpcIdentity)Get(selection,"Target")).Slot==target;if(selected)selectedCount++;
                        var path=cache.Read(0);bool present=path!=null;
                        if(present)
                        {
                            Require(path.Identity.Slot==target && ReferenceEquals(path.Identity.Token,n) && path.SampleTick==Main.GameUpdateCount && path.Count==121,"Only genuine current+120 windows count as publications.");
                            shown++;blank=0;if(first<0)first=frame;
                        }
                        else longest=Math.Max(longest,++blank);
                        rows.Add(Csv(trace.Phase,frame,Main.GameUpdateCount,towns.Count(t=>t.active),selected,present,path?.CaptureTick,path?.Count,Tick(Get(native,"pending")),Tick(Get(native,"acceptedRequest")),Get(native,"Failed"),Get(native,"Reason"),workerId,player.statLife,player.immune,player.immuneTime,player.position.X,player.position.Y,n.position.X,n.position.Y,n.life,Collision.CanHit(n.position,n.width,n.height,player.position,player.width,player.height),Main.projectile.Count(p=>p.active),Get(Get(native,"npcs"),"Count"),Get(Get(native,"projectiles"),"Count"),trace.NpcUpdates-ns,trace.ProjectileUpdates-ps));
                    }
                    summaries.Add(Csv(trace.Phase,start+1,Main.GameUpdateCount,durations[axis],selectedCount,shown,longest,first,(long)Get(native,"Requests")-requestStart,trace.Received-receiveStart,(long)Get(native,"Rejected")-rejectStart,(long)Get(native,"Refused")-refuseStart,carryIn,Tick(Get(native,"pending")),trace.NpcUpdates-npcStart,trace.ProjectileUpdates-projectileStart));
                    Console.WriteLine("HOSTILE-QUERY "+trace.Phase+" published="+shown+"/"+durations[axis]+" longest="+longest+" worker="+workerId);
                }
                finished=true;
            }
            finally
            {
                File.WriteAllLines(Path.Combine(output,"hostile-updates.csv"),rows);File.WriteAllLines(Path.Combine(output,"hostile-summary.csv"),summaries);
                File.WriteAllText(Path.Combine(output,"hostile-status.txt"),finished?"configured-continuous-windows-completed; not a product correctness PASS":"interrupted-or-invalid");
            }
        }
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
