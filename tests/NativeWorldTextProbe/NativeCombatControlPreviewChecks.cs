using System;
using System.Reflection;
using System.Linq;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real Player/ItemCheck owns birth and registry. Read-only stage components
    // retain their former geometry coverage without resurrecting idle display
    // demand or treating an opening hypothesis as a current AI capability.
    internal static class NativeCombatControlPreviewChecks
    {
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            var failures=new System.Collections.Generic.List<string>();
            foreach(int type in new[]{3475,2797,3540,3854,3930,113,218,495,3030,3278,3541})
            {
                var originalRandom=Main.rand;
                try
                {
                // This nine-sample component needs a reproducible native
                // lottery, independent of earlier integration groups. In
                // particular Player chooses 714.ai0=5*Next(0,20). Do not
                // select a favorable stage or alter the actual draw/AI.
                Main.rand=new Terraria.Utilities.UnifiedRandom(17);
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,type,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=200;
                p.inventory[54].SetDefaults(p.HeldItem.useAmmo==40?1334:p.HeldItem.useAmmo==771?771:3104);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);
                var n=Dummy(2,type==3278?51:52,40);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,25,true,true));
                for(int i=0;i<3;i++)
                {
                    NativeCombatSelectionPreviewChecks.Step(context,n.Center,false,true);
                    Require(GetOptional(attack,"ExpectedPreview")==null && ((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Required==0 && ((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1)==null,"idle controlled family has no retired display or attack Source demand: "+type);
                    Require(!Main.projectile.Any(q=>q.active) && !p.controlUseItem && !p.channel,"controlled preview cannot create its own live source: "+type);
                }
                NativeCombatSelectionPreviewChecks.Step(context,n.Center,true);
                Require(Main.projectile.Any(q=>q.active && q.owner==0),"first actual click creates original source without needing an earlier hit: "+type);
                var registry=(System.Collections.IDictionary)Get(Get(attack,"Control"),"shots");Require(registry.Count>0,"first natural source is registered for later legal consumers: "+type);
                if(type==3930)Console.WriteLine("CONTROL component seed17 native parent first ai="+string.Join(";",Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && q.type==714).Select(q=>q.ai[0]+"/"+q.ai[1])));
                bool opening;var stage=ReadStageFacts(context,n,out opening);int visible=Feasible(stage)?1:0,current=Feasible(stage) && !opening?1:0,future=Feasible(stage) && opening?1:0;bool retiredMissile=false;
                if(visible==0)Missing(type,0,p,n);
                for(int i=0;i<8;i++)
                {
                    NativeCombatSelectionPreviewChecks.Step(context,n.Center,true);var result=ReadStageFacts(context,n,out opening);if(Feasible(result)){visible++;if(opening)future++;else current++;}else Missing(type,i+1,p,n);
                    if(type==113 && p.ownedProjectileCounts[p.HeldItem.shoot]>0 && !Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot))
                    {retiredMissile=true;Require(opening && result.Contact!=null && result.Contact.Delay>=p.itemTime-1,"naturally retired missile's stale count retains a correctly delayed NEW hand component, not a current AI capability");}
                    Require(GetOptional(attack,"ExpectedPreview")==null,"read-only stage component never restores retired idle presentation");
                }
                if(type==113)Require(retiredMissile,"natural missile fixture actually crosses the projectile-pass retirement after Player's cached count");
                Require(visible==9,"all nine original controller stage components remain feasible, with current vs future hand explicitly separated: "+type+" visible="+visible+"/9");
                Console.WriteLine("PASS natural controlled source/stages item="+type+" idle3/noDemand/noPreview current="+current+" futureHand="+future+"/9 sources="+registry.Count);
                }
                catch(Exception error){failures.Add(type+": "+error.Message);Console.WriteLine("FAIL natural controlled preview/first birth item="+type+": "+error);}
                finally{Main.rand=originalRandom;}
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            Require(failures.Count==0,"natural controller family failures: "+string.Join(" | ",failures));
            NativeCombatCelebrationChecks.Run(context);
        }
        private static bool Feasible(NativeCombatAimChecks.SolutionFacts facts)
        {return facts.Contact!=null || facts.Route=="representative" && facts.HasPoint && facts.Reachability=="Reachable" && facts.Timeline!=null && facts.ReceiveTick>=facts.FirstTick && facts.ReceiveTick<facts.Timeline.Count;}
        // Celebration retains the original dedicated Contact ABI. Held-family
        // readers consume facts instead; neither adapter grants input permission.
        internal static AttackContact ReadStage(object context,NPC target,out bool opening)
        {return ReadStageFacts(context,target,out opening).Contact;}
        private static NativeCombatAimChecks.SolutionFacts ReadStageFacts(object context,NPC target,out bool opening)
        {
            var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");var control=Get(attack,"Control");var p=Main.LocalPlayer;
            var keys=Main.projectile.Where(q=>q.active).Select(q=>q.key).ToArray();bool use=p.controlUseItem,channel=p.channel;int mouseX=Main.mouseX,mouseY=Main.mouseY;
            NativeCombatAimChecks.SolutionFacts result;
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
            {
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);
                var path=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1);Require(path!=null,"controlled component owns an explicit current shared trajectory");
                var phase=Enum.Parse(attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");
                if(p.HeldItem.shoot==615 || p.HeldItem.shoot==630 || p.HeldItem.shoot==705)
                {
                    // The current held adapter returns a representative solution,
                    // not the retired complete Preview Contact. Determine source
                    // role with the controller's actual observation predicate.
                    opening=NativeCombatAimChecks.ReadObservedHeldSource(attack)==null;
                    object plan;result=NativeCombatUsageChecks.ReadMechanismFacts(host,attack,target,out plan,path);
                    Require(result.Route=="representative" && ReferenceEquals(result.Timeline,path),"held stage facts retain their private current trajectory and representative route");
                }
                else
                {
                    var contact=(AttackContact)Call(control,"Preview",path,phase,0,false);opening=contact==null;
                    if(opening)contact=(AttackContact)Call(control,"Preview",path,phase,0,true);
                    result=new NativeCombatAimChecks.SolutionFacts{Route="dedicated-contact",Contact=contact,Phase="CompletedWorld",Delay=contact?.Delay??0};
                }
            }
            Require(use==p.controlUseItem && channel==p.channel && mouseX==Main.mouseX && mouseY==Main.mouseY && keys.SequenceEqual(Main.projectile.Where(q=>q.active).Select(q=>q.key)),"read-only controlled stage never lends cursor, presses input, or creates a live source");
            return result;
        }
        private static void Missing(int type,int frame,Player p,NPC n)
        {Console.WriteLine("CONTROL preview missing item="+type+" frame="+frame+" timers="+p.itemAnimation+"/"+p.itemTime+" channel="+p.channel+" npcImmune="+n.immune[p.whoAmI]+" source="+string.Join(";",Main.projectile.Where(q=>q.active && q.owner==p.whoAmI).Select(q=>q.type+"/"+q.position+"/"+q.velocity+"/ai="+string.Join(",",q.ai)+"/left="+q.timeLeft)));}
        internal static NPC Dummy(int slot,int x,int y)
        {
            // Native AI92 retires a dummy without its real placed tile. Keep
            // the full 2x3 object and vanilla spawn coordinates; do not disable
            // that AI to make an invalid target survive the outer update.
            for(int dx=0;dx<2;dx++)for(int dy=0;dy<3;dy++)
            {NativeToolsChecks.Tile(x+dx,y+dy,378);Main.tile[x+dx,y+dy].frameX=(short)(dx*18);Main.tile[x+dx,y+dy].frameY=(short)(dy*18);}
            // SetDefaults does not clear a previous occupant's velocity.
            // Use vanilla's fresh-instance slot boundary, as NewNPC does,
            // or an ostensibly placed dummy drifts on an old zombie vector.
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{slot,(byte)0});
            var n=Main.npc[slot];n.SetDefaults(488);n.whoAmI=slot;n.active=true;n.position=new Vector2(x*16+16-n.width/2,y*16+48-n.height);n.ai[0]=x;n.ai[1]=y;n.target=0;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
    }
}
