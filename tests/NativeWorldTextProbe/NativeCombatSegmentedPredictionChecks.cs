using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSegmentedPredictionChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(Assembly host,string output)
        {
            // A real native birth supplies all links; no manually assembled
            // body pages can make this routing check accidentally succeed.
            int[] chain=NativeCombatLongCoverageChecks.Destroyer(true);
            int slot=chain[chain.Length/2];
            object source=Create(host,output);
            var type=source.GetType();var cache=(NpcPredictionCache)type.GetField("Cache",Flags).GetValue(source);
            cache.Demand(0,120);
            try
            {
                for(int step=0;step<5;step++)
                {
                    Advance();NPC n=Main.npc[slot];
                    type.GetMethod("Prepare",Flags).Invoke(source,new object[]{Identity(n),Main.GameUpdateCount});
                    var path=cache.Read(0);
                    Require(path!=null && path.Count==121,"A natural multi-hit worm publishes 120 future updates immediately without waiting for native work.");
                    Require((path.Assumptions&PredictionAssumption.ApproximateMechanism)!=0,"Approved segmented strategy is explicitly approximate.");
                    Require(path.CaptureTick==Main.GameUpdateCount && path[0].Bounds.X==n.position.X && path[0].Bounds.Y==n.position.Y,"Current true origin and capture time.");
                    if(step>0)Require(n.velocity.LengthSquared()==0 && (path[1].Vx!=0 || path[1].Vy!=0),"Direct position movement is not mistaken for zero velocity.");
                }
                object native=type.GetField("Native",Flags).GetValue(source);
                Require((long)native.GetType().GetField("Requests",Flags).GetValue(native)==0,"No full-chain native request or shadow computation for the segmented path.");
                Require(native.GetType().GetProperty("Worker",Flags).GetValue(native)==null,"Cheap segmented prediction does not require IPC preparation.");
                Console.WriteLine("PASS natural 102-segment production routing / zero native requests / zero-velocity body / current + 120 future");
            }
            finally{type.GetMethod("Stop",Flags).Invoke(source,null);}
            Families(host,output);
            Split(host,output);
        }
        private static void Families(Assembly host,string output)
        {
            int moving=0,backwards=0;
            using(var csv=new StreamWriter(Path.Combine(output,"segmented-frozen-future.csv")))
            {
                csv.WriteLine("head,role,type,parts,tick,error_px,actual_distance_px,direction_cos,quality");
                foreach(int head in new[]{NPCID.TheDestroyer,NPCID.EaterofWorldsHead,NPCID.WyvernHead,NPCID.SeekerHead,NPCID.DevourerHead,NPCID.GiantWormHead,NPCID.BoneSerpentHead,NPCID.DiggerHead,NPCID.LeechHead,NPCID.DuneSplicerHead,NPCID.TombCrawlerHead,NPCID.CultistDragonHead,NPCID.BloodEelHead})
                foreach(int role in new[]{0,1,2})
                {
                    int[] chain=Birth(host,head);int slot=chain[role==0?0:role==1?chain.Length/2:chain.Length-1];
                    object owner=Create(host,output);var cache=Cache(owner);cache.Demand(0,120);
                    try
                    {
                        // Native fade-in and motion happen before selection.
                        // Only the last few observations seed the prediction.
                        for(int i=0;i<60;i++){Advance();if(i>=56)Prepare(owner,Identity(Main.npc[slot]));}
                        for(int i=0;Main.npc[slot].dontTakeDamage && i<240;i++)
                        {Require(cache.Read(0)==null,"Natural fade-in stays unselectable.");Advance();Prepare(owner,Identity(Main.npc[slot]));}
                        NPC n=Main.npc[slot];NpcTrajectory frozen=cache.Read(0);Check(frozen,n);
                        var origin=n.Center;var trend=new Vector2(frozen[1].Vx,frozen[1].Vy);
                        for(int tick=1;tick<=120;tick++)
                        {
                            Advance();Require(n.active,"Legal family remains alive during its measured future: "+head);
                            Vector2 actual=n.Center-origin,estimate=new Vector2(frozen[tick].Bounds.CenterX,frozen[tick].Bounds.CenterY);
                            float cosine=actual.LengthSquared()>.0001f && trend.LengthSquared()>.0001f?Vector2.Dot(Vector2.Normalize(actual),Vector2.Normalize(trend)):0;
                            if(tick==1 && actual.LengthSquared()>.01f){moving++;if(cosine<0)backwards++;}
                            if(new[]{1,5,15,30,60,120}.Contains(tick))csv.WriteLine(string.Join(",",head,role,n.type,chain.Length,tick,Vector2.Distance(estimate,n.Center).ToString("F3",CultureInfo.InvariantCulture),actual.Length().ToString("F3",CultureInfo.InvariantCulture),cosine.ToString("F3",CultureInfo.InvariantCulture),frozen.Quality));
                            // This is separate from the frozen two-second
                            // comparison: rolling output corrects at every tick.
                            Prepare(owner,Identity(n));Check(cache.Read(0),n);
                        }
                        ZeroNative(owner);
                    }
                    finally{owner.GetType().GetMethod("Stop",Flags).Invoke(owner,null);}
                }
            }
            Require(moving>20 && backwards*2<moving,"Observed first-step direction is not systematically reversed: "+backwards+"/"+moving);
            foreach(int excluded in new[]{NPCID.Probe,NPCID.SolarCrawltipedeHead,NPCID.SolarCrawltipedeBody,NPCID.SolarCrawltipedeTail,NPCID.Harpy,NPCID.DD2WyvernT1,NPCID.StardustWormHead})
                Require(Family(host,excluded)==0,"Unapproved independent/weak-point-only type keeps its original strategy: "+excluded);
            Console.WriteLine("PASS 13 natural multi-hit families x head/body/tail; frozen 1/5/15/30/60/120 future CSV; rolling corrections; first-step backwards="+backwards+"/"+moving);
        }
        internal static int[] Birth(Assembly host,int head)
        {
            if(head==NPCID.TheDestroyer)return NativeCombatLongCoverageChecks.Destroyer(false);
            NativeCombatWorkerChecks.Scene(false);NPC.ClearAll();Projectile.ClearAll();Main.getGoodWorld=false;
            Main.LocalPlayer.ZoneCorrupt=true;Main.LocalPlayer.ZoneUndergroundDesert=true;
            // Preserve native initial target=255 so TargetClosest establishes
            // direction before the first head step (not just a target index).
            int first=NPC.NewNPC(new EntitySource_DebugCommand(),550,900,head,Start:1);
            Advance();var chain=new List<int>();int slot=first;
            while(slot>0 && slot<Main.maxNPCs && !chain.Contains(slot))
            {
                NPC n=Main.npc[slot];Require(n.active && Family(host,n.type)==Family(host,head),"Native linked family "+head+" slot "+slot);
                chain.Add(slot);slot=(int)n.ai[0];
            }
            Require(chain.Count>=3 && slot==0,"Native head creates a finite multi-hit chain "+head+" count="+chain.Count);
            return chain.ToArray();
        }
        internal static int Family(Assembly host,int type)
        {return (int)host.GetType("JueMingR.TerrariaHost.Combat.Prediction.SegmentedNpcPrediction",true).GetMethod("Family",Flags).Invoke(null,new object[]{type});}
        private static void Split(Assembly host,string output)
        {
            Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");Lang.InitializeLegacyLocalization();
            ContentSamples.Initialize();Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
            int[] chain=Birth(host,NPCID.EaterofWorldsHead);int cut=chain.Length/2;
            object owner=Create(host,output);var cache=Cache(owner);cache.Demand(0,120);
            try
            {
                NPC formerBody=Main.npc[chain[cut+1]],formerTail=Main.npc[chain[cut-1]],victim=Main.npc[chain[cut]];
                for(int i=0;i<3;i++){Advance();Prepare(owner,Identity(formerBody));}
                var previous=cache.Read(0);byte generation=formerBody.generation;
                victim.StrikeNPCNoInteraction(1000000,0,0);Require(!victim.active,"Original hit actually kills the chosen EoW body.");
                Prepare(owner,Identity(victim));Require(cache.Read(0)==null,"Dead selected part immediately loses its path.");
                Advance();Prepare(owner,Identity(formerBody));var after=cache.Read(0);Check(after,formerBody);
                Require(formerBody.type==NPCID.EaterofWorldsHead && formerTail.type==NPCID.EaterofWorldsTail && formerBody.generation==generation && ReferenceEquals(previous.Identity.Token,formerBody),"Original split transforms surviving bodies without inventing new births.");
                Require(after.RelationVersion!=previous.RelationVersion && after.Identity.Type!=previous.Identity.Type,"Changed role and links retire the old relation and geometry.");
                // Kill each current endpoint, then another body. Each original
                // update must settle the new role without native preparation.
                foreach(int slot in new[]{chain[0],chain[chain.Length-1],chain[cut+3]})
                {
                    var dying=Main.npc[slot];dying.StrikeNPCNoInteraction(1000000,0,0);Require(!dying.active,"Natural endpoint/rapid split death.");Advance();
                    foreach(int survivor in chain)if(Main.npc[survivor].active){Prepare(owner,Identity(Main.npc[survivor]));Check(cache.Read(0),Main.npc[survivor]);}
                }
                // Use the real allocator for same-slot, same-type reuse. Old
                // identity remains rejected even though its object still exists.
                var retired=Identity(formerBody);formerBody.StrikeNPCNoInteraction(1000000,0,0);
                for(int i=0;i<2;i++)Advance();
                int replacement=NPC.NewNPC(new EntitySource_DebugCommand(),550,900,NPCID.EaterofWorldsHead,Start:retired.Slot);
                Require(replacement==retired.Slot && !ReferenceEquals(Main.npc[replacement],retired.Token),"Native allocator reuses the same slot with a new instance.");
                Prepare(owner,retired);Require(cache.Read(0)==null,"Old identity cannot publish for the new occupant.");
                Advance();Prepare(owner,Identity(Main.npc[replacement]));Check(cache.Read(0),Main.npc[replacement]);
                Main.npc[replacement].dontTakeDamage=true;Prepare(owner,Identity(Main.npc[replacement]));Require(cache.Read(0)==null,"Temporary invulnerability never acquires a hittable approximate path.");
                ZeroNative(owner);
                Console.WriteLine("PASS native EoW body split, endpoint deaths, rapid splits, Transform identity, relation replacement, same-slot native allocation and current invulnerability");
            }
            finally{owner.GetType().GetMethod("Stop",Flags).Invoke(owner,null);}
        }
        private static NpcPredictionCache Cache(object source){return (NpcPredictionCache)source.GetType().GetField("Cache",Flags).GetValue(source);}
        private static void Prepare(object source,NpcIdentity identity){source.GetType().GetMethod("Prepare",Flags).Invoke(source,new object[]{identity,(long)Main.GameUpdateCount});}
        private static void Check(NpcTrajectory path,NPC n)
        {
            Require(path!=null && path.Count==121 && path.Strategy==PredictionStrategy.SegmentedTrend && path.Identity.Equals(Identity(n)) && path.SampleTick==Main.GameUpdateCount,"Current full-domain target-specific segmented result type="+n.type+" active="+n.active+" life="+n.life+" invulnerable="+n.dontTakeDamage+" position="+n.position+" velocity="+n.velocity);
            Require(path[0].Bounds.X==n.position.X && path[0].Bounds.Y==n.position.Y && path[0].Bounds.Width==n.width && path[0].Bounds.Height==n.height && path[0].CanReceive,"Actual current geometry and eligibility.");
        }
        private static void ZeroNative(object source)
        {object native=source.GetType().GetField("Native",Flags).GetValue(source);Require((long)native.GetType().GetField("Requests",Flags).GetValue(native)==0 && native.GetType().GetProperty("Worker",Flags).GetValue(native)==null,"Segmented work never starts native whole-chain shadow work.");}
        internal static object Create(Assembly host,string output)
        {
            string hash;using(var stream=File.OpenRead(host.Location))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            var launch=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionLaunchIdentity",true),Flags,null,new object[]{hash,output},null);
            return Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true),Flags,null,new[]{launch},null);
        }
        internal static NpcIdentity Identity(NPC n){return new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);}
        internal static void Advance()
        {
            typeof(Main).GetField("_gameUpdateCount",Flags).SetValue(null,unchecked(Main.GameUpdateCount+1));
            NPC.UpdateProtectedSpawnSlots();NPC.ClearFoundActiveNPCs();NPC.UpdateFoundActiveNPCs();
            for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);
        }
        private static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    }
}
