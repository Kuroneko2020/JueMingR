using System;
using System.Reflection;
using System.Collections;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Narrow result-ownership countercontrols. These native components do not
    // claim full Main.Update/Draw, frame timing or a second prediction model.
    internal static class NativeCombatPreviewLeaseChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static int solves,reads;
        private static void Solve(){solves++;}
        private static void Read(){reads++;}
        internal static void WindGate(object context)
        {
            var state=Get(context,"CombatObservation").GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileEnvironment",true).GetNestedType("State",Flags);
            var withPlayer=state.GetConstructor(Flags,null,new[]{typeof(bool),typeof(bool),typeof(Player)},null);
            Func<Player,object> Capture=p=>withPlayer==null?Activator.CreateInstance(state,Flags,null,new object[]{true,true},null):withPlayer.Invoke(new object[]{true,true,p});
            bool wind=Main.windPhysics;float speed=Main.windSpeedCurrent,strength=Main.windPhysicsStrength;var player=Main.LocalPlayer;bool pushed=player.windPushed;
            try
            {
                Main.windPhysics=false;player.windPushed=false;
                var shot=new Projectile();shot.SetDefaults(1);
                Require(!shot.ShouldUseWindPhysics(),"locked original projectile rejects wind physics before consulting speed or strength");
                var disabled=Capture(player);Main.windSpeedCurrent=speed+.137f;Main.windPhysicsStrength=strength+.023f;
                Require((bool)Get(disabled,"Current"),"disabled projectile wind gate and unpushed player retain a hand environment despite unused wind parameter drift");
                Main.windPhysics=true;Require(!(bool)Get(disabled,"Current"),"enabling the real wind gate revokes the disabled snapshot");
                Main.windSpeedCurrent=speed;Main.windPhysicsStrength=strength;var enabled=Capture(player);
                Main.windSpeedCurrent=speed+.137f;Require(!(bool)Get(enabled,"Current"),"enabled wind retains its speed dependency");
                Main.windSpeedCurrent=speed;Main.windPhysicsStrength=strength+.023f;Require(!(bool)Get(enabled,"Current"),"enabled wind retains its strength dependency");
                Main.windPhysics=false;Main.windSpeedCurrent=speed;Main.windPhysicsStrength=strength;player.windPushed=true;
                Require(player.CanBePushedByWind(),"player countercontrol has the original independent wind-push gate");
                var playerWind=Capture(player);Main.windSpeedCurrent=speed+.137f;
                Require(!(bool)Get(playerWind,"Current"),"player wind push still reads speed when projectile wind physics is disabled");
                Main.windSpeedCurrent=speed;player.windPushed=false;Require(!(bool)Get(playerWind,"Current"),"changing the actual player's wind-push gate revokes its prior environment");
                var conservative=Activator.CreateInstance(state,Flags,null,withPlayer==null?new object[]{true,true}:new object[]{true,true,null},null);
                Main.windSpeedCurrent=speed+.137f;Require(!(bool)Get(conservative,"Current"),"unqualified other-family state keeps its conservative wind dependency");
                Console.WriteLine("PASS original wind gate / disabled unused parameters / enabled speed-strength / independent player wind push; no solver or input permission change");
            }
            finally{Main.windPhysics=wind;Main.windSpeedCurrent=speed;Main.windPhysicsStrength=strength;player.windPushed=pushed;}
        }
        internal static void TerrainOwner(object host,object attack,AttackContact contact,bool controlled)
        {
            var plan=Get(attack,"preview");var dependency=controlled?Get(plan,"ControlDependencies"):Get(plan,"Flight");
            var lease=Get(dependency,controlled?"terrain":"Terrain");var values=(Array)Get(lease,"values");
            Require(values.Length>0,"positive preview owns the terrain actually read by its solver");
            var owner=controlled?Get(Get(attack,"Control"),"previewTerrain"):Get(attack,"flightPreviewTerrain");
            var decoy=Get(plan,"Terrain");int oldWidth=(int)Get(decoy,"width");Call(decoy,"Reset");
            // A previously initialized empty hand terrain is deliberately a
            // valid decoy. It must neither reject nor authorize this result.
            Require((bool)Get(decoy,"Unchanged"),"unused hand terrain is initialized and unchanged for the wrong-owner countercontrol");
            Call(owner,"Reset");
            var copy=Activator.CreateInstance(plan.GetType(),true);Call(copy,"CopyPresentation",plan);
            Require(ReferenceEquals(dependency,Get(copy,controlled?"ControlDependencies":"Flight")),"presentation copy retains the immutable result's dependency owner");
            var audit=new Harmony("JueMingR.Tests.PreviewLeaseOwner");
            audit.Patch(attack.GetType().GetMethod("PreparePlan",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPreviewLeaseChecks),nameof(Solve)));
            audit.Patch(attack.GetType().GetMethod("ReadFlight",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPreviewLeaseChecks),nameof(Solve)));
            audit.Patch(owner.GetType().GetMethod("Read",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPreviewLeaseChecks),nameof(Read)));
            int key=(int)Get(values.GetValue(0),"Key"),width=(int)Get(lease,"width");var tile=Main.tile[key%width,key/width];ushort wall=tile.wall;
            var work=(CombatPredictionWork)Get(host,"PredictionWork");
            try
            {
                solves=reads=0;
                using(work.Enter(Main.GameUpdateCount))using(CombatPredictionWork.Optional(3))
                    Require((bool)Call(attack,"PreviewDependenciesCurrent"),"resetting the solver owner cannot erase this result's old frozen read set");
                Require(reads>0,"positive dependency validation really reads the frozen solver terrain");
                reads=0;Call(attack,"PreviewCurrent",contact);Require(reads==0 && solves==0,"the drawing member gate does not read terrain or replay a solver");
                tile.wall=(ushort)(wall==0?1:0);reads=0;
                using(work.Enter(Main.GameUpdateCount))using(CombatPredictionWork.Optional(3))
                {
                    Require(!(bool)Call(attack,"PreviewDependenciesCurrent") && (bool)Get(decoy,"Unchanged"),"same-update real solver tile changes reject the result while the initialized wrong terrain remains unchanged");
                    Call(attack,"ValidatePreviewPresentation");
                }
                Require(reads>0 && GetOptional(attack,"ExpectedPreview")==null && solves==0,"consumption withdraws only the changed preview lease without solver or flight replay");
                Require(ReferenceEquals(contact,Get(plan,"Contact")),"invalidating the presentation does not clear the independent contact result");
                Console.WriteLine("PASS preview terrain provenance controlled="+controlled+" oldUnusedWidth="+oldWidth+" actualFrozenCells="+values.Length+" resetPreserved=true tileRejected=true consumptionSolves="+solves);
            }
            finally{tile.wall=wall;audit.UnpatchAll(audit.Id);}
        }

        internal static void Navigation(object host,object attack,Player player,AttackContact actual)
        {
            var control=Get(attack,"Control");var phase=Get(control,"preparedPhase");
            var observed=(AttackContact)Call(control,"Preview",actual.Timeline,phase,0,false);
            Require(observed!=null,"navigation ownership countercontrol has an actual qualified reused controller result");
            var dependency=Call(control,"CapturePreviewDependencies",observed);var nav=Get(dependency,"navigation");
            Require(nav!=null,"reused yoyo contact freezes its own navigation scalar and terrain dependencies");
            var work=(CombatPredictionWork)Get(host,"PredictionWork");bool prior=player.yoyoString;Vector2 position=player.position;
            using(work.Enter(Main.GameUpdateCount))using(CombatPredictionWork.Optional(3))
            {
                Require((bool)Call(dependency,"Current",player),"fresh actual navigation dependency is valid");
                player.yoyoString=!prior;Require(!(bool)Call(dependency,"Current",player),"same-update yoyo string change withdraws the old navigation result");player.yoyoString=prior;
                player.position+=Vector2.UnitX;Require(!(bool)Call(dependency,"Current",player),"same-update holder motion withdraws the old navigation result");player.position=position;
                // Normal ClearPreview retires the producer's mutable binding,
                // while the already-copied result keeps its scalar read set.
                Call(control,"ClearPreview");Require((bool)Call(dependency,"Current",player),"clearing the preview producer cannot replace a copied navigation read set");
            }
            Require(ReferenceEquals(actual,GetOptional(attack,"ExpectedImpact")),"pure navigation preview validation preserves the qualified actual controller Contact");
            Console.WriteLine("PASS preview navigation result string/motion rejected; copied scalar owner preserved; actual input unchanged");
        }
        internal static void Opening(object context,object host,object combat,object attack,object input)
        {
            foreach(int delay in new[]{0,2})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;
                var n=NativeCombatControlPreviewChecks.Dummy(2,51,40);Main.screenPosition=new Vector2(600,400);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                NativeToolExecutionChecks.Sample(context,input,new Vector2(600,500),false);Call(combat,"Sample");Call(host,"SampleMouse");
                p.itemTime=delay+1;p.velocity=delay==0?Vector2.Zero:new Vector2(2,0);p.controlRight=delay!=0;NativeQuickItemChecks.BeginWorldStep();NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                var path=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1);var phase=Enum.Parse(attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var control=Get(attack,"Control");
                AttackContact result;object dependency;var work=(CombatPredictionWork)Get(host,"PredictionWork");
                using(work.Enter(Main.GameUpdateCount))using(CombatPredictionWork.Optional(3))
                {
                    result=(AttackContact)Call(control,"Preview",path,phase,delay,true);dependency=Call(control,"CapturePreviewDependencies",result);
                    Require(result!=null && dependency!=null && (bool)Call(dependency,"Current",p),"idle yoyo opening binds the real original holder instead of the private future body: delay="+delay);
                    var nav=Get(dependency,"navigation");Require(nav!=null && ReferenceEquals(Get(Get(nav,"player"),"PlayerToken"),p),"opening comparison keeps the live input token while its replay uses a clone");
                    bool prior=p.yoyoString;p.yoyoString=!prior;Require(!(bool)Call(dependency,"Current",p),"opening string dependency is real in the same update");p.yoyoString=prior;
                    Vector2 position=p.position;p.position+=Vector2.UnitX;Require(!(bool)Call(dependency,"Current",p),"opening holder motion dependency is real in the same update");p.position=position;
                }
                Require(!System.Linq.Enumerable.Any(Main.projectile,q=>q.active) && GetOptional(control,"previewSource")==null,"a positive opening creates no live shot or control receipt");
                Console.WriteLine("PASS yoyo opening original input lease delay="+delay+" velocity="+p.velocity+" receive="+result.ReceiveTick+" noBirth=true");
            }
        }
        internal static void OrdinaryReceive(object context,object host,object combat,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,ItemID.WoodenBow,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(ItemID.JestersArrow);p.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(910,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;Array.Clear(n.immune,0,n.immune.Length);Main.screenPosition=new Vector2(600,400);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,false));
            AttackContact contact=null;
            for(int step=0;step<8 && contact==null;step++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);contact=(AttackContact)GetOptional(attack,"ExpectedPreview");}
            Require(contact!=null && !(bool)Get(Get(attack,"preview"),"Controlled") && GetOptional(Get(attack,"preview"),"Flight")==null,"receiver-only countercontrol owns a qualified ordinary idle piercing-arrow preview");
            var work=(CombatPredictionWork)Get(host,"PredictionWork");int immune=n.immune[p.whoAmI];
            try
            {
                using(work.Enter(Main.GameUpdateCount))using(CombatPredictionWork.Optional(3))
                {
                    Require((bool)Call(attack,"PreviewDependenciesCurrent"),"unchanged ordinary receive dependencies remain valid");
                    n.immune[p.whoAmI]++;
                    Require(GetOptional(host,"DisplayPath")!=null && (bool)Get(attack,"CurrentReceiver") && !(bool)Call(attack,"PreviewDependenciesCurrent"),"only real receiver immunity changes: path/identity survive while the ordinary result dependency rejects it");
                    Call(attack,"ValidatePreviewPresentation");
                }
                Require(GetOptional(attack,"ExpectedPreview")==null,"same-update immunity change withdraws ordinary preview without regenerating it");
                Console.WriteLine("PASS ordinary preview unchanged receive positive -> sameGUC receiver immunity rejected; path retained=true");
            }
            finally{n.immune[p.whoAmI]=immune;}
            // Bullet 14 does not use this owner-immunity gate. Preserve the
            // real positive instead of treating every immune change as stale.
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
            n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(910,667);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;Array.Clear(n.immune,0,n.immune.Length);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,false));contact=null;
            for(int step=0;step<8 && contact==null;step++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);contact=(AttackContact)GetOptional(attack,"ExpectedPreview");}
            Require(contact!=null,"nonpiercing bullet countercontrol has a qualified ordinary preview");immune=n.immune[p.whoAmI];
            try
            {
                using(work.Enter(Main.GameUpdateCount))using(CombatPredictionWork.Optional(3))
                {
                    Require((bool)Call(attack,"PreviewDependenciesCurrent"),"unchanged nonpiercing bullet dependency is valid");
                    n.immune[p.whoAmI]++;
                    Require((bool)Call(attack,"PreviewDependenciesCurrent"),"original bullet14 ignores owner immunity rather than falsely retiring its receive");
                }
                Console.WriteLine("PASS ordinary bullet14 irrelevant owner-immunity change preserves its original receive qualification");
                var plan=Get(attack,"preview");int age=(int)((long)Main.GameUpdateCount-Convert.ToInt64(Get(attack,"previewStep")));
                Require(!Main.windPhysics && !p.windPushed,"qualified ordinary wind fixture has both original parameter-use gates closed");
                float windSpeed=Main.windSpeedCurrent,windStrength=Main.windPhysicsStrength;
                try
                {
                    Require((bool)Call(plan,"PresentationMembersCurrent",p,age),"real qualified ordinary preview starts with current captured hand members");
                    Main.windSpeedCurrent=windSpeed+.137f;Main.windPhysicsStrength=windStrength+.023f;
                    Require((bool)Call(plan,"PresentationMembersCurrent",p,age),"real qualified preview survives unused closed-gate wind drift without a new solve");
                    Main.windPhysics=true;Require(!(bool)Call(plan,"PresentationMembersCurrent",p,age),"real wind gate activation withdraws the old qualified preview");
                    Main.windPhysics=false;p.windPushed=true;Require(!(bool)Call(plan,"PresentationMembersCurrent",p,age),"independent real player wind-push activation withdraws the old hand snapshot");
                }
                finally{Main.windSpeedCurrent=windSpeed;Main.windPhysicsStrength=windStrength;Main.windPhysics=false;p.windPushed=false;}
                Console.WriteLine("PASS qualified ordinary preview: unused wind drift retained; projectile/player real gate activation rejected");
            }
            finally{n.immune[p.whoAmI]=immune;}
        }
        internal static void HandReceive(object context,object host,object combat,object attack,object input)
        {
            // Reuse the existing native full-animation edge calibration and
            // actual Strike oracle, not an unproved wooden-sword placement.
            NativeCombatSwingChecks.IdleReceiveQualification(context);
        }
        internal static void HandReceivePositive(Player p,NPC n,object attack,AttackContact contact)
        {
            var plan=Get(attack,"preview");
            Require(contact!=null && contact.ReceiveGeometry==AttackReceiveGeometry.Melee && GetOptional(plan,"Flight")==null && !(bool)Get(plan,"Controlled"),"near original sword fixture owns a qualified future hand Contact");
            int immune=n.immune[p.whoAmI],attackCd=p.attackCD,meleeCd=p.meleeNPCHitCooldown[n.whoAmI];
            long remaining=contact.ReceiveTick-((long)Main.GameUpdateCount-contact.Timeline.SampleTick)+1;
            try
            {
                Require((bool)Call(plan,"PresentationMembersCurrent",p,0),"unchanged completed-world hand receive qualification remains valid");
                n.immune[p.whoAmI]=(int)remaining;Require(!(bool)Call(plan,"PresentationMembersCurrent",p,0),"same-update owner immunity past elapsed-1 withdraws the hand receive");n.immune[p.whoAmI]=immune;
                p.attackCD=(int)remaining+1;Require(!(bool)Call(plan,"PresentationMembersCurrent",p,0),"same-update attack cooldown past receive withdraws the hand result");p.attackCD=attackCd;
                p.meleeNPCHitCooldown[n.whoAmI]=(int)remaining+1;Require(!(bool)Call(plan,"PresentationMembersCurrent",p,0),"same-update melee cooldown past receive withdraws the hand result");p.meleeNPCHitCooldown[n.whoAmI]=meleeCd;
                n.immune[p.whoAmI]=Math.Max(0,(int)remaining-1);p.attackCD=p.meleeNPCHitCooldown[n.whoAmI]=(int)remaining;
                Require((bool)Call(plan,"PresentationMembersCurrent",p,0),"the original exact receive-time limits remain a positive hand countercontrol");
                // Advance only this component clock and naturally decay the
                // three timers. No new result, source or synthetic rebind.
                NativeQuickItemChecks.BeginWorldStep();n.immune[p.whoAmI]=Math.Max(0,n.immune[p.whoAmI]-1);p.attackCD=Math.Max(0,p.attackCD-1);p.meleeNPCHitCooldown[n.whoAmI]=Math.Max(0,p.meleeNPCHitCooldown[n.whoAmI]-1);
                Require((bool)Call(plan,"PresentationMembersCurrent",p,1),"aged completed-world hand keeps the same feasible receive as immunity/cooldowns decay");
                Console.WriteLine("PASS hand receive qualification elapsed="+remaining+" immune/attack/melee rejected; exact boundary and one aged decay valid");
            }
            finally{n.immune[p.whoAmI]=immune;p.attackCD=attackCd;p.meleeNPCHitCooldown[n.whoAmI]=meleeCd;}
        }
    }
}
