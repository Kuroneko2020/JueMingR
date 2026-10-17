using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMeleeHitFamilyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Assembly host;private static object context,attack,observation;private static NPC victim,selected;
        private static int itemType,first,primary,births,depth,rng,startCalls,startedFacing,expectedIndex,originalDamage,nativeHitDamage,nativeHitRandomized,minimumBirthSlot,lotteryInts,lotteryDoubles,lotteryFloats,lotterySamples,lotteryBeeKinds;private static long epoch;private static bool nativeLottery,prepare,negative,enhanced,observePermission;
        private static Projectile[] expected;private static Rectangle nativeHand;private static float nativeKb;
        private static NPC nativeHitReceiver;
        private static Projectile damaging;private static AttackContact planned;private static object plannedRoot;
        private sealed class OrdinaryHand{internal int World,Facing,Born,Primary,First,Direct;internal string Kind,BirthState="";internal AttackContact Plan;internal Vector2 Point;}
        private static bool ordinary;private static int ordinaryWorld;private static OrdinaryHand ordinaryHand;
        private static readonly List<OrdinaryHand> ordinaryHands=new List<OrdinaryHand>();
        private static readonly Dictionary<ProjectileKey,OrdinaryHand> ordinaryChildren=new Dictionary<ProjectileKey,OrdinaryHand>();
        private static readonly HashSet<ProjectileKey> nativeChildren=new HashSet<ProjectileKey>();
        private static bool collectModelBirth;private static readonly List<int> modelDamages=new List<int>();
        private static bool Trace {get{return Environment.GetEnvironmentVariable("JMR_MELEE_HIT_FAMILY_TRACE")=="1";}}
        private static void Pose(string label,Player p,Rectangle hand)
        {Vector2 a,b,v;p.GetPointOnSwungItemPath(70,70,0,p.GetAdjustedItemScale(p.HeldItem),out a,out v);p.GetPointOnSwungItemPath(70,70,.9f,p.GetAdjustedItemScale(p.HeldItem),out b,out v);Console.WriteLine("MELEE POSE "+label+" tick="+(Main.GameUpdateCount-epoch)+" anim="+p.itemAnimation+"/"+p.itemAnimationMax+" dir="+p.direction+" pos="+p.position+" loc="+p.itemLocation+" rot="+p.itemRotation+" hand="+hand+" capsule="+a+"/"+b);}
        private static void NativeHand(Player __instance,Rectangle itemRectangle,int npcIndex,int originalDamage,float knockBack)
        {
            if(!prepare || npcIndex!=victim.whoAmI)return;if(Trace)Pose("native",__instance,itemRectangle);
            nativeHand=itemRectangle;NativeCombatMeleeHitFamilyChecks.originalDamage=originalDamage;nativeKb=knockBack;lotteryInts=lotteryDoubles=lotteryFloats=lotterySamples=lotteryBeeKinds=0;
            if(itemType==1166)ExpectedBirth(__instance,0,0);
        }
        private static void ExpectedBirth(Player p,int damage,int randomized)
        {
            var bounds=new MotionRect(victim.position.X,victim.position.Y,victim.width,victim.height);object[] args={p,p.HeldItem,nativeHand,bounds,victim.velocity,originalDamage,damage,randomized,nativeKb,victim.HittableForOnHitRewards(),!victim.immortal,true};
            depth++;try{expected=(Projectile[])host.GetType("JueMingR.TerrariaHost.Combat.HostMeleeHitFamilyAttack",true).GetMethod("Births",Flags).Invoke(null,args);expectedIndex=0;}finally{depth--;}
        }
        private static void OnHit(Player __instance,NPC npc,int damage,int dmgRandomized){if(prepare){nativeHitReceiver=npc;nativeHitDamage=damage;nativeHitRandomized=dmgRandomized;}if(prepare && ReferenceEquals(npc,victim) && itemType!=1166)ExpectedBirth(__instance,damage,dmgRandomized);}
        private static void ModelHand(Player p,Rectangle hand){if(Trace && prepare && depth>0)Pose("model-hit",p,hand);}
        private static void ModelBirth(Player p,Projectile[] __result)
        {if(collectModelBirth && depth>0 && !ReferenceEquals(p,Main.LocalPlayer))foreach(var shot in __result)modelDamages.Add(shot.damage);}
        private static bool Family(int type){return type==1111 || type==524 || type==977 || type==978 || type==181 || type==566;}
        private static string BirthTuple(Projectile p)
        {return "type="+p.type+" owner="+p.owner+" slot="+p.whoAmI+" key="+p.key+" center="+p.Center+" velocity="+p.velocity+" damage="+p.damage+" originalDamage="+p.originalDamage+" knockBack="+p.knockBack+" ai="+string.Join(",",p.ai)+" armorPenetration="+p.armorPenetration+" bonusTagDamage="+p.bonusTagDamage+" melee="+p.melee;}
        private static bool NextInt(object[] __args,ref int __result)
        {
            if(depth>0){rng++;return true;}if(!nativeLottery)return true;lotteryInts++;
            int min=__args.Length==2?(int)__args[0]:0,max=__args.Length==0?int.MaxValue:(int)__args[__args.Length-1];
            __result=itemType==1123 && min==0 && max==2?0:min+(max-min)/2;return false;
        }
        private static bool NextDouble(ref double __result){if(depth>0){rng++;return true;}if(!nativeLottery)return true;lotteryDoubles++;__result=1073741824d/int.MaxValue;return false;}
        // The default Effects predecessors may already have compiled this
        // wrapper with NextDouble inlined. Audit/override the native float
        // entry itself as well; ordinary hands never enable nativeLottery.
        private static bool NextFloat(ref float __result){if(depth>0){rng++;return true;}if(!nativeLottery)return true;lotteryFloats++;__result=.5f;return false;}
        // Rectangle sampling may itself contain already-inlined NextFloat /
        // NextDouble. Fix only their legal INTEGER random input, never the
        // rectangle/vector result: native normalization, curve and birth math
        // must still run. This raw sample rounds to .5f and makes Next(2)==1.
        private static bool InternalSample(ref int __result){if(depth>0){rng++;return true;}if(!nativeLottery)return true;lotterySamples++;__result=1073741824;return false;}
        private static double[] RandomSequence(Terraria.Utilities.UnifiedRandom r)
        {var rectangle=r.NextVector2FromRectangle(new Rectangle(10,20,94,66));return new[]{(double)r.Next(3),r.Next(-35,36),r.NextDouble(),r.NextFloat(),rectangle.X,rectangle.Y,r.Next()};}
        private static int[] BeeRandomSequence()
        {
            var original=Main.rand;var owner=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(Main.LocalPlayer,null);var rows=new List<int>();
            try
            {
                Main.rand=new Terraria.Utilities.UnifiedRandom(17);
                // Only private scalar owner fields are written by these real
                // methods. Keep the live equipped owner and RNG object intact.
                foreach(bool strong in new[]{false,true})for(int i=0;i<8;i++){owner.strongBees=strong;rows.Add(owner.beeType());rows.Add(owner.beeDamage(10));rows.Add((int)(owner.beeKB(0)*2));rows.Add(Main.rand.Next());}
                return rows.ToArray();
            }
            finally{Main.rand=original;}
        }
        private static int LotteryNext(Terraria.Utilities.UnifiedRandom r){int value=0;return NextInt(Array.Empty<object>(),ref value)?r.Next():value;}
        private static int LotteryNext(Terraria.Utilities.UnifiedRandom r,int max){int value=0;return NextInt(new object[]{max},ref value)?r.Next(max):value;}
        private static int LotteryNext(Terraria.Utilities.UnifiedRandom r,int min,int max){int value=0;return NextInt(new object[]{min,max},ref value)?r.Next(min,max):value;}
        private static int LotteryBeeTypeNext(Terraria.Utilities.UnifiedRandom r,int max)
        {
            if(depth>0 || !nativeLottery)return LotteryNext(r,max);
            Require(max==2,"native beeType retains its original two-way random domain");lotteryInts++;lotteryBeeKinds++;
            // Hive Pack may legitimately persist from an earlier real hand.
            // Native beeType needs 1 for the legal NORMAL representative;
            // native beeDamage separately needs 0 for its legal +0 damage.
            // Leave both functions, makeStrongBee and beeKB formulas native.
            return 1;
        }
        private static double LotteryDouble(Terraria.Utilities.UnifiedRandom r){double value=0;return NextDouble(ref value)?r.NextDouble():value;}
        private static float LotteryFloat(Terraria.Utilities.UnifiedRandom r){float value=0;return NextFloat(ref value)?r.NextFloat():value;}
        // Rewrite ONLY scalar random reads in the five native spawn callers
        // and their two genuine helpers. Earlier JIT compilation cannot hide
        // these reads in an inlined wrapper. All vector/rectangle sampling,
        // normalization, curve integration and NewProjectile IL stays native.
        private static IEnumerable<CodeInstruction> NativeLotteryInputs(IEnumerable<CodeInstruction> instructions)
        {
            foreach(var instruction in instructions)
            {
                var method=instruction.operand as MethodInfo;string name=null;Type[] parameters=null;
                if((instruction.opcode==OpCodes.Call || instruction.opcode==OpCodes.Callvirt) && method!=null)
                {
                    if(method.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (method.Name=="Next" || method.Name=="NextDouble"))
                    {name=method.Name=="Next"?nameof(LotteryNext):nameof(LotteryDouble);parameters=new[]{typeof(Terraria.Utilities.UnifiedRandom)}.Concat(method.GetParameters().Select(p=>p.ParameterType)).ToArray();}
                    else if(method.DeclaringType==typeof(Utils) && method.Name=="NextFloat" && method.GetParameters().Length==1)
                    {name=nameof(LotteryFloat);parameters=new[]{typeof(Terraria.Utilities.UnifiedRandom)};}
                }
                if(name!=null){instruction.opcode=OpCodes.Call;instruction.operand=typeof(NativeCombatMeleeHitFamilyChecks).GetMethod(name,Flags,null,parameters,null);}
                yield return instruction;
            }
        }
        private static IEnumerable<CodeInstruction> NativeBeeTypeInputs(IEnumerable<CodeInstruction> instructions)
        {
            foreach(var instruction in NativeLotteryInputs(instructions))
            {
                var method=instruction.operand as MethodInfo;
                if(method!=null && method.DeclaringType==typeof(NativeCombatMeleeHitFamilyChecks) && method.Name==nameof(LotteryNext))instruction.operand=typeof(NativeCombatMeleeHitFamilyChecks).GetMethod(nameof(LotteryBeeTypeNext),Flags);
                yield return instruction;
            }
        }
        private static void Damage(Projectile __instance){damaging=__instance;}
        private static void DamageDone(){damaging=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(__result<=0)return;if(damaging==null && ReferenceEquals(__instance,victim)){primary++;if(ordinary && ordinaryHand!=null)ordinaryHand.Primary++;}
            if(ordinary && ReferenceEquals(__instance,selected)){OrdinaryHand hand;if(damaging!=null && ordinaryChildren.TryGetValue(damaging.key,out hand) && hand.First==0)hand.First=ordinaryWorld+1;else if(damaging==null && ordinaryHand!=null)ordinaryHand.Direct++;}
            if(damaging!=null && Family(damaging.type) && ReferenceEquals(__instance,selected) && (!prepare || nativeChildren.Contains(damaging.key)) && first==0)first=(int)(Main.GameUpdateCount-epoch);
        }
        private static void Birth(IEntitySource __0,int __result)
        {
            if(!(prepare || observePermission || ordinary) || __result<0 || __result>=Main.maxProjectiles || !Family(Main.projectile[__result].type))return;births++;
            var q=Main.projectile[__result];var source=__0 as EntitySource_ItemUse;Require(source!=null && source.Item.type==itemType,"future-family retains its real native ItemUse source");
            if(ordinary){Require(ordinaryHand!=null,"ordinary child belongs to an actual earlier native hand");ordinaryHand.Born++;ordinaryChildren[q.key]=ordinaryHand;ordinaryHand.BirthState+=q.type+"@"+q.Center+"/"+q.velocity+";";}
            if(!prepare)return;
            nativeChildren.Add(q.key);
            if(collectModelBirth)Require(modelDamages.Contains(q.damage),"private pre-birth replay includes the exact native Nano child damage, not only its eventual contact time");
            minimumBirthSlot=Math.Min(minimumBirthSlot,q.whoAmI);
            Require(expected!=null && expectedIndex<expected.Length,"native own OnHit birth count matches its legal representative");var p=expected[expectedIndex++];
            var owner=Main.player[q.owner];
            Require(q.type==p.type && q.owner==p.owner && Vector2.DistanceSquared(q.Center,p.Center)<.00000001f && Vector2.DistanceSquared(q.velocity,p.velocity)<.00000001f && q.damage==p.damage && q.knockBack==p.knockBack && q.ai[0]==p.ai[0] && q.ai[1]==p.ai[1] && q.armorPenetration==p.armorPenetration && q.bonusTagDamage==p.bonusTagDamage,"native own child exact birth/velocity/ai/damage/source scalars item="+itemType+" native=["+BirthTuple(q)+"] model=["+BirthTuple(p)+"] handOriginalDamage="+originalDamage+" hitDamage="+nativeHitDamage+" hitRandomized="+nativeHitRandomized+" nativeKb="+nativeKb+" itemArmor="+owner.HeldItem.armorPenetration+" itemTag="+owner.HeldItem.bonusTagDamage+" strongBees="+owner.strongBees+" makeStrongBee="+GetOptional(owner,"makeStrongBee")+" representativeIntCalls="+lotteryInts+" representativeDoubleCalls="+lotteryDoubles+" representativeFloatCalls="+lotteryFloats+" rotation="+owner.fullRotation+" rotationOrigin="+owner.fullRotationOrigin+" frame="+owner.bodyFrame+" hand="+nativeHand+" trigger="+nativeHitReceiver?.whoAmI+" sameTrigger="+ReferenceEquals(nativeHitReceiver,victim)+" triggerBounds="+nativeHitReceiver?.Hitbox+" triggerVelocity="+nativeHitReceiver?.velocity);
            if(itemType==1166){Require(lotteryFloats>=2,"actual native 1166 float lottery entry is intercepted even after compiled predecessors");Console.WriteLine("MELEE FAMILY native lottery item=1166 floatCalls="+lotteryFloats+" velocity="+q.velocity);}
            if(itemType==155){Require(ReferenceEquals(nativeHitReceiver,victim) && lotteryFloats>=4,"native Muramasa rectangle/discard/curve scalar draws are audited while the original same-receiver formula executes");Console.WriteLine("MELEE FAMILY native lottery item=155 floatCalls="+lotteryFloats+" rawSamples="+lotterySamples+" rotation="+owner.fullRotation+" frame="+owner.bodyFrame+" trigger="+nativeHitReceiver.whoAmI+" bounds="+nativeHitReceiver.Hitbox+" native="+q.Center+"/"+q.velocity+" curve="+q.ai[0]);}
            if(itemType==1123){Require(!owner.strongBees || lotteryBeeKinds>0,"a legitimate strong-bee owner reaches the original beeType caller's normal-representative draw");Console.WriteLine("MELEE FAMILY native lottery item=1123 strongOwner="+owner.strongBees+" armor3="+owner.armor[3].type+" beeTypeInputs="+lotteryBeeKinds+" native=["+BirthTuple(q)+"]");}
        }
        private static void BeforeStart(Player __instance)
        {
            if(!(prepare || observePermission || ordinary) || __instance!=Main.LocalPlayer)return;startCalls++;
            if(ordinary){ordinaryHand=new OrdinaryHand{World=ordinaryWorld+1,Plan=GetOptional(attack,"ExpectedImpact") as AttackContact};ordinaryHands.Add(ordinaryHand);return;}
            if(!prepare)return;
            depth++;try{NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,selected,0,phaseName:"BeforeNpc");}finally{depth--;}
            planned=GetOptional(attack,"ExpectedImpact") as AttackContact;plannedRoot=GetOptional(attack,"obstacles");
            Require(negative || planned!=null && plannedRoot!=null,"actual admitted StartActualUse has a pre-birth finite-family contact and root item="+itemType);
        }
        private static void AfterStart(Player __instance)
        {
            if(!(prepare || observePermission || ordinary) || __instance!=Main.LocalPlayer)return;
            bool consumed=(bool)Get(attack,"hasConsumedInput") && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount;
            enhanced=consumed && Get(Get(attack,"consumedInput"),"Kind").ToString()=="Enhanced";
            startedFacing=__instance.direction;
            if(ordinary){ordinaryHand.Kind=consumed?Get(Get(attack,"consumedInput"),"Kind").ToString():"None";ordinaryHand.Facing=__instance.direction;if(consumed)ordinaryHand.Point=(Vector2)Get(Get(attack,"consumedInput"),"Point");if(enhanced)ordinaryHand.Plan=GetOptional(attack,"current") as AttackContact;return;}
            if(Trace)Console.WriteLine("MELEE START actualDir="+__instance.direction+" input="+Get(Get(attack,"consumedInput"),"Kind")+" root="+(plannedRoot==null?"null":Get(plannedRoot,"UnchangedForHandStart"))+" planned="+planned?.AimX);
        }
        private static Player Scene(int type)
        {
            NativeCombatObservationChecks.Save(observation,new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),type,0,0);
            p.position=new Vector2(700,646);p.velocity=Vector2.Zero;p.direction=1;p.gravDir=1;p.attackCD=0;p.ResetMeleeHitCooldowns();p.releaseUseItem=p.releaseJump=p.releaseUp=true;p.itemAnimation=p.itemTime=0;p.strongBees=false;
            if(Environment.GetEnvironmentVariable("JMR_MELEE_HIT_FAMILY_ROTATED_OWNER")=="1")p.fullRotation=.4f;
            Main.screenPosition=new Vector2(550,400);Main.windPhysics=false;Main.ProjectileUpdateLoopIndex=-1;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++)Main.tile[x,y].ClearEverything();Main.tileSolid[1]=true;
            for(int x=5;x<Main.maxTilesX-5;x++)NativeToolsChecks.Tile(x,43,1);
            NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,true,false,0,false,true));
            return p;
        }
        private static NPC Target(int slot,Vector2 center,int side=24,int life=100000)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=n.height=side;n.Center=center;n.life=n.lifeMax=life;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.friendly=n.immortal=n.dontTakeDamage=false;n.velocity=n.netOffset=Vector2.Zero;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        private static NpcTrajectory Path(NPC n)
        {var id=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var s=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,Active=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,s)).ToArray();return new NpcTrajectory(id,Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);}
        private static AttackContact Solve(Player p,out object root)
        {
            var swing=Get(attack,"swing");var path=Path(selected);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);
            var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");AttackContact result;
            depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))result=(AttackContact)Call(swing,"Solve",p,p.HeldItem,path,clock,terrain,false,120);}finally{depth--;}
            root=GetOptional(swing,"FamilyRoot");return result;
        }
        private static Vector2 FindLane(Player p,int type,int facing)
        {
            // Fixed finite grid chooses a receiver BEFORE native use/birth.
            // Require a child contact, not merely a direct hand overlap. The
            // future proof therefore cannot be rescued by post-birth placement.
            var preferred=type==1123?new Vector2(180,-4):type==1166?new Vector2(145,-70):type==3211?new Vector2(180,-30):type==121?new Vector2(105,-45):new Vector2(110,-65);
            var positions=new[]{preferred}.Concat(from x in new[]{90,115,145,180,220,-90,-115} from y in new[]{-85,-55,-25,5} select new Vector2(x,y));
            foreach(var offset in positions)
            {
                selected.Center=p.Center+new Vector2(offset.X*facing,offset.Y);object root;var contact=Solve(p,out root);
                if(contact!=null && contact.ReceiveGeometry==AttackReceiveGeometry.Projectile)
                {
                    victim.active=false;AttackContact direct;try{direct=Solve(p,out root);}finally{victim.active=true;}
                    if(direct!=null)continue;
                    Console.WriteLine("MELEE FAMILY lane item="+type+" facing="+facing+" offset="+offset+" predicted="+contact.Tick);return selected.Center;
                }
            }
            throw new InvalidOperationException("finite pre-birth child lane missing item="+type+" facing="+facing);
        }
        private static void Boundaries(Player p,object root)
        {
            object ignored;bool value=victim.dontTakeDamage;victim.dontTakeDamage=true;Require(Solve(p,out ignored)==null,"invulnerable trigger cannot create a hand child");victim.dontTakeDamage=value;
            value=victim.friendly;victim.friendly=true;Require(Solve(p,out ignored)==null,"ordinary friendly NPC cannot be used as the hand trigger");victim.friendly=value;
            value=selected.dontTakeDamage;selected.dontTakeDamage=true;Require(Solve(p,out ignored)==null,"child cannot bypass selected receive qualification");selected.dontTakeDamage=value;
            int timer=victim.immune[p.whoAmI];victim.immune[p.whoAmI]=-1;Require(Solve(p,out ignored)==null,"permanently owner-immune trigger cannot create a future child");victim.immune[p.whoAmI]=timer;
            int defense=victim.defense;victim.defense++;Require(!(bool)Get(root,"Unchanged"),"actual trigger defense is part of the private death premise");victim.defense=defense;
            value=victim.ichor;victim.ichor=!value;Require(!(bool)Get(root,"Unchanged"),"armor debuff invalidates private hand/child lifetime ordering");victim.ichor=value;
            var oldItem=p.inventory[p.selectedItem];var replacement=new Item();replacement.SetDefaults(itemType);p.inventory[p.selectedItem]=replacement;Require(!(bool)Get(root,"Unchanged"),"same-type replacement item cannot reuse an old hand-family source");p.inventory[p.selectedItem]=oldItem;
            var fresh=Target(9,p.Center+new Vector2(350,0));Require(!(bool)Get(root,"Unchanged"),"new observed receiver invalidates captured hand slot ordering");fresh.active=false;
            Require((bool)Get(root,"Unchanged"),"restored finite-family inputs restore the same immutable premise");
            // Later in the SAME real animation neither a newly selected point
            // nor another candidate may turn the already admitted hand around.
            int animation=p.itemAnimation,maximum=p.itemAnimationMax,direction=p.direction;
            p.itemAnimationMax=p.HeldItem.useAnimation;p.itemAnimation=p.itemAnimationMax-2;p.direction=-direction;
            var ongoing=Solve(p,out ignored);Require(ongoing==null || Math.Sign(ongoing.AimX-p.Center.X)==p.direction,"ongoing hand preserves real native facing");
            if(itemType==121 || itemType==155)
            {
                var flag=typeof(Player).GetField(itemType==121?"_spawnVolcanoExplosion":"_spawnMuramasaCut",Flags);bool armed=(bool)flag.GetValue(p);flag.SetValue(p,false);
                var exhausted=Solve(p,out ignored);Require(exhausted==null || exhausted.ReceiveGeometry==AttackReceiveGeometry.Melee,"consumed once-per-use gate never predicts another child");flag.SetValue(p,armed);
            }
            p.direction=direction;p.itemAnimation=animation;p.itemAnimationMax=maximum;
        }
        private static void Cases()
        {
            int cases=0;
            foreach(int type in new[]{121,1166,3211,155,1123})foreach(int facing in new[]{1,-1})
            {
                itemType=type;var p=Scene(type);victim=Target(3,p.Center+new Vector2(36*facing,-4),34,1);selected=Target(2,p.Center+new Vector2(300,0));
                // Real idle Animate supplies the source texture frame. Aim has
                // no input permission until the later actual press/use below.
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,false);
                var lane=FindLane(p,type,facing);object root;var component=Solve(p,out root);Require(component!=null && root!=null && (bool)Get(root,"Unchanged"),"pre-birth finite family owns its complete actual receiver lease");
                int old=victim.life;victim.life++;Require(!(bool)Get(root,"Unchanged"),"same-GUC trigger life changes invalidate future-family proof");victim.life=old;
                old=victim.immune[p.whoAmI];victim.immune[p.whoAmI]=99;Require(!(bool)Get(root,"Unchanged"),"same-GUC original hand immunity revokes future family");victim.immune[p.whoAmI]=old;
                Boundaries(p,root);
                bool active=victim.active;victim.active=false;object unused;var missing=Solve(p,out unused);Require(missing==null,"no observed hand-hittable trigger means no invented future child");victim.active=active;
                // The original lottery is fixed to the exact legal midpoint
                // representative only in this oracle, never in production.
                nativeChildren.Clear();nativeLottery=prepare=true;negative=enhanced=false;planned=null;plannedRoot=null;expected=null;primary=first=births=startCalls=expectedIndex=0;epoch=Main.GameUpdateCount;
                try{for(int action=0;action<110 && first==0;action++)NativeCombatSelectionPreviewChecks.Step(context,lane,action<1);}finally{prepare=nativeLottery=false;}
                Require(startCalls==1 && primary>0 && births>0,"one real admitted hand hits its observed other receiver and creates native own children item="+type);
                Require(enhanced,"real StartActualUse consumes the finite hand's own Enhanced facing receipt item="+type);
                Require(planned!=null && first>0 && first==planned.Tick,"pre-birth selected first native Strike equals prepared contact item="+type+" native="+first+" planned="+planned?.Tick);
                Require(startedFacing==Math.Sign(planned.AimX-p.Center.X),"native admitted hand consumes the contact's legal facing outlet item="+type);
                Console.WriteLine("MELEE FAMILY actual item="+type+" facing="+facing+" primary="+primary+" born="+births+" first="+first+" plan="+planned.Tick);cases++;
            }
            Console.WriteLine("MELEE FAMILY actual hand-to-other-to-selected cases="+cases);
        }
        private static void OwnerWall()
        {
            itemType=121;var p=Scene(121);victim=Target(3,p.Center+new Vector2(36,-4),34,1);selected=Target(2,p.Center+new Vector2(115,-85));selected.noTileCollide=false;
            for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(48,y,1);
            for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,false);
            object root;Require(Solve(p,out root)==null,"finite Volcano child retains owner-to-selected wall qualification");
            nativeLottery=prepare=negative=true;enhanced=false;expected=null;primary=first=births=startCalls=expectedIndex=0;epoch=Main.GameUpdateCount;
            try{for(int action=0;action<65;action++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,action<1);}finally{prepare=nativeLottery=negative=false;}
            Require(startCalls==1 && primary>0 && births==1 && first==0 && !enhanced,"actual hand still births one native explosion but wall forbids selected Strike and Enhanced input");
            Console.WriteLine("MELEE FAMILY actual owner-wall born="+births+" selected0 Enhanced0");
        }
        private static void PermissionEdges()
        {
            foreach(bool ui in new[]{false,true})
            {
                itemType=121;var p=Scene(121);victim=Target(3,p.Center+new Vector2(36,-4),34,1);selected=Target(2,p.Center+new Vector2(115,-85));
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,false);
                object root;Require(Solve(p,out root)!=null,"permission counterexample starts from a physically viable finite hand");
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,true,false,0,false,ui));Main.drawingPlayerChat=ui;
                prepare=false;observePermission=nativeLottery=true;enhanced=false;primary=first=births=startCalls=0;epoch=Main.GameUpdateCount;
                try{for(int action=0;action<55;action++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,action<1);}finally{observePermission=nativeLottery=false;Main.drawingPlayerChat=false;}
                Require(!enhanced && !(bool)Get(attack,"hasConsumedInput") && p.direction==1,"Aim OFF/UI cannot consume or change a viable family's prepared facing");
                if(!ui)Require(startCalls==1 && primary>0 && births==1 && first>0,"Aim OFF preserves the original unassisted hand/birth/selected Damage");
                Console.WriteLine("MELEE FAMILY permission ui="+ui+" nativeStarts="+startCalls+" born="+births+" first="+first+" Enhanced0");
            }
        }
        private static void RunBoundaryHand(Player p,bool blocked,int frames)
        {
            nativeChildren.Clear();nativeLottery=prepare=true;negative=blocked;enhanced=false;planned=null;plannedRoot=null;expected=null;primary=first=births=startCalls=expectedIndex=0;minimumBirthSlot=Main.maxProjectiles;epoch=Main.GameUpdateCount;
            try{for(int action=0;action<frames;action++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,action<1);}finally{prepare=nativeLottery=negative=false;}
            Require(startCalls==1 && primary>0 && births>0,"boundary retains the real hand -> other receiver -> native own birth");
            if(blocked)Require(first==0 && !enhanced,"earlier ambient receive forbids a fabricated own-child Contact or Enhanced input");
            else Require(enhanced && planned!=null && first==planned.Tick,"boundary actual Enhanced own-child first Strike remains exact");
        }
        private static void SlotCases()
        {
            foreach(int type in new[]{121,1123})
            {
                itemType=type;var p=Scene(type);victim=Target(3,p.Center+new Vector2(36,-4),34,1);selected=Target(2,p.Center+new Vector2(300,0));
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,false);
                FindLane(p,type,1);
                for(int i=0;i<20;i++)
                {
                    int slot=Projectile.NewProjectile(new EntitySource_ItemUse(p,p.HeldItem),p.Center+new Vector2(0,-100),Vector2.Zero,14,0,0,255);var shot=Main.projectile[slot];
                    shot.friendly=false;shot.tileCollide=false;shot.timeLeft=600;Require(slot==i,"native lower slots remain occupied before hand use");
                }
                RunBoundaryHand(p,false,75);Require(minimumBirthSlot>=20,"native hand children allocate after all occupied lower slots");
                Console.WriteLine("MELEE FAMILY slots item="+type+" occupied20 firstChildSlot="+minimumBirthSlot+" born="+births+" first="+first+" plan="+planned.Tick);
            }
        }
        private static void AmbientCases()
        {
            foreach(int damage in new[]{5,0})
            {
                itemType=1123;var p=Scene(1123);victim=Target(3,p.Center+new Vector2(36,-4),34,1);selected=Target(2,p.Center+new Vector2(180,-4));
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,false);
                int slot=Projectile.NewProjectile(new EntitySource_ItemUse(p,p.HeldItem),selected.Center,Vector2.Zero,181,damage,0,p.whoAmI);var ambient=Main.projectile[slot];
                ambient.ai[0]=30;ambient.alpha=0;ambient.penetrate=ambient.maxPenetrate=-1;ambient.timeLeft=600;ambient.extraUpdates=0;ambient.tileCollide=false;
                object root;var proof=Solve(p,out root);Require(damage==0?proof!=null:proof==null,"ambient static history distinguishes real Damage from a harmless occupied body");
                RunBoundaryHand(p,damage>0,85);Require(minimumBirthSlot>slot,"ambient bee keeps its earlier native pool slot");
                Console.WriteLine("MELEE FAMILY ambient damage="+damage+" oldSlot="+slot+" born="+births+" ownFirst="+first+" plan="+planned?.Tick+" Enhanced="+enhanced);
            }
        }
        private static void CriticalParameters()
        {
            foreach(byte enchant in new byte[]{0,6})
            {
            itemType=121;var p=Scene(121);victim=Target(3,p.Center+new Vector2(36,-4),34,10000);selected=Target(2,p.Center+new Vector2(115,-85));
            p.meleeEnchant=enchant;p.meleeCrit=100;p.armorPenetration=4;p.HeldItem.armorPenetration=3;p.HeldItem.bonusTagDamage=2;victim.defense=9;victim.takenDamageMultiplier=1.3f;
            int slot=Projectile.NewProjectile(new EntitySource_ItemUse(p,p.HeldItem),victim.Center,Vector2.Zero,978,17,0,p.whoAmI);var q=Main.projectile[slot];q.melee=true;q.ranged=q.magic=false;
            var stateType=host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles+MeleeFamilyState",true);
            var state=Activator.CreateInstance(stateType,Flags,null,new object[]{p,p.HeldItem,(long)1,true},null);Call(state,"CaptureDamageMembers",new List<Projectile>{q});
            int modeled;depth++;try{modeled=(int)Call(state,"ProjectileDamage",victim.whoAmI,q);}finally{depth--;}
            Require((bool)Call(state,"InputsMatch",false),"critical scalar lease initially owns all private damage inputs");
            p.meleeCrit++;Require(!(bool)Call(state,"InputsMatch",false),"owner critical chance revokes the private death premise");p.meleeCrit--;
            p.armorPenetration++;Require(!(bool)Call(state,"InputsMatch",false),"owner armor penetration revokes private damage");p.armorPenetration--;
            p.HeldItem.armorPenetration++;Require(!(bool)Call(state,"InputsMatch",false),"ItemUse armor inheritance revokes future birth");p.HeldItem.armorPenetration--;
            q.bonusCritChance++;Require(!(bool)Call(state,"InputsMatch",false),"ambient bonus critical chance revokes its captured contribution");q.bonusCritChance--;
            q.melee=false;Require(!(bool)Call(state,"InputsMatch",false),"ambient damage class revokes owner-crit and armor dispatch");q.melee=true;
            q.stopsDealingDamageAfterPenetrateHits=!q.stopsDealingDamageAfterPenetrateHits;Require(!(bool)Call(state,"InputsMatch",false),"harmless-after-penetration lifetime cannot reuse an old slot premise");q.stopsDealingDamageAfterPenetrateHits=!q.stopsDealingDamageAfterPenetrateHits;
            Require((bool)Call(state,"InputsMatch",false),"all exact scalar restorations restore their immutable premise");
            int nativeDamage=q.damage+victim.checkArmorPenetration(p.GetArmorPenetration(q.melee)+q.armorPenetration,0);if(enchant==6)nativeDamage=(int)(nativeDamage*Player.GetNanoFlaskDamageBoost());
            int original=victim.StrikeNPC(nativeDamage,0,1,true,false,p.whoAmI);
            Require(modeled==original,"100% critical native Strike applies its multiplier before integer floor and taken-damage multiplier model="+modeled+" native="+original);
            Console.WriteLine("MELEE FAMILY critical100 enchant="+enchant+" native="+original+" model="+modeled+" sourceArmor="+q.armorPenetration+" sourceTag="+q.bonusTagDamage+" scalarRevocations6");
            }
        }
        private static void OrdinaryTrigger(Player p)
        {
            victim=Target(3,p.Center+new Vector2(36,-4),34,1);victim.SetDefaults(1);victim.whoAmI=3;victim.active=true;victim.width=victim.height=34;victim.Center=p.Center+new Vector2(36,-4);victim.life=victim.lifeMax=1;victim.defense=0;victim.knockBackResist=0;victim.noGravity=true;victim.velocity=Vector2.Zero;
            // Keep native AI001, including its clock. This is a real dormant
            // slime state, not aiStyle=-1 or a replaced NPC.Update method.
            victim.ai[0]=-1000;victim.target=p.whoAmI;
        }
        private static void TimerParameters()
        {
            foreach(float charge in new[]{-1f,0,999,1000})
            {
                itemType=121;var p=Scene(121);victim=Target(3,p.Center+new Vector2(36,-4),34,10000);selected=Target(2,p.Center+new Vector2(115,-85));
                p.accTimerCrit=true;p.accTimerCritCharge=charge;
                var stateType=host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles+MeleeFamilyState",true);var state=Activator.CreateInstance(stateType,Flags,null,new object[]{p,p.HeldItem,(long)1,true},null);
                p.accTimerCritCharge+=.5f;Require(!(bool)Call(state,"InputsMatch",false),"same-GUC forced-critical charge changes revoke private death");p.accTimerCritCharge=charge;
                p.accTimerCrit=false;Require(!(bool)Call(state,"InputsMatch",false),"forced-critical accessory permission is an actual damage input");p.accTimerCrit=true;
                p.meleeEnchant=6;Require(!(bool)Call(state,"InputsMatch",false),"Nano damage multiplier cannot reuse another enchantment's private life");p.meleeEnchant=0;
                for(int action=0;action<4;action++)
                {
                    p.itemAnimation=action%2==0?0:10;p.reuseDelay=0;p.channel=p.pendingItemReuse=false;p.velocity=action<2?Vector2.Zero:Vector2.UnitX;
                    depth++;try{Call(state,"AdvanceTimerCrit",p.UsingOrReusingItem,p.velocity);}finally{depth--;}
                    typeof(Player).GetMethod("Update_TimerCrit",Flags).Invoke(p,null);
                    Require((float)Get(state,"TimerCharge")==p.accTimerCritCharge,"one private timer step exactly matches the original Player prefix");
                    object[] args={new Rectangle(0,0,16,16),false};typeof(Player).GetMethod("TryConsumingTimerCrit",Flags).Invoke(p,args);
                    bool modeled;depth++;try{modeled=(bool)Call(state,"Critical",false);}finally{depth--;}
                    Require(modeled==(bool)args[1] && (float)Get(state,"TimerCharge")==p.accTimerCritCharge,"hand and ordered children share the native forced-crit consumption state");
                }
                Console.WriteLine("MELEE FAMILY timer initial="+charge+" native="+p.accTimerCritCharge+" model="+Get(state,"TimerCharge")+" playerSteps4 sharedConsumes4 scalarRevocations3");
            }
        }
        private static void ParameterHands()
        {
            foreach(int type in new[]{155,1123})
            {
                itemType=type;var p=Scene(type);victim=Target(3,p.Center+new Vector2(36,-4),34,1);selected=Target(2,p.Center+new Vector2(300,0));
                p.AddBuff(77,1200);
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,selected.Center,false);
                Require(p.meleeEnchant==6,"native buff update supplies the exact Nano damage premise");
                modelDamages.Clear();collectModelBirth=true;
                try{FindLane(p,type,1);RunBoundaryHand(p,false,75);}finally{collectModelBirth=false;}
                Console.WriteLine("MELEE FAMILY parameter actual item="+type+" enchant="+p.meleeEnchant+" timer="+p.accTimerCrit+" born="+births+" first="+first+" plan="+planned.Tick+" privateBirthDamages="+string.Join(",",modelDamages.Distinct())+" Enhanced1");
            }
            // The original release deprecates the only TimerCrit accessory.
            // Keep native field/update math coverage above without inventing a
            // wearable positive or reviving the removed item for this fixture.
            var owner=Scene(121);owner.ResetEffects();owner.armor[3].SetDefaults(6170);
            Require(Terraria.ID.ItemID.Sets.Deprecated[6170] && owner.armor[3].IsAir && !owner.accTimerCrit,"original deprecated TimerCrit item cannot grant a fictional equipped opportunity");
            Console.WriteLine("MELEE FAMILY TimerCrit original deprecated6170=air permissionFalse; forced field math is a separate native-function component");
            Require(rng==0,"parameter family replay never consumes original RNG");
        }
        private static void OrdinaryCases()
        {
            foreach(int type in new[]{121,1166})
            {
                itemType=type;var p=Scene(type);
                selected=NativeCombatControlPreviewChecks.Dummy(2,type==121?51:53,type==121?34:35);OrdinaryTrigger(p);
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,true,true,0,false,true));
                Main.rand=new Terraria.Utilities.UnifiedRandom(17);ordinaryHands.Clear();ordinaryChildren.Clear();ordinaryHand=null;primary=first=births=startCalls=0;epoch=Main.GameUpdateCount;
                // No PublishFixture, FindLane, explicit Prepare, enlarged work
                // scope, or idle Source warmup occurs in this fixed window.
                // The placed native AI092 dummy gives a stable observed lane;
                // the trigger retains native AI001 and the ordinary NPC pass.
                ordinary=true;prepare=observePermission=nativeLottery=false;
                try
                {
                    for(ordinaryWorld=0;ordinaryWorld<280;ordinaryWorld++)
                    {
                        bool release=ordinaryWorld>=100 && ordinaryWorld<160;int phase=ordinaryWorld<160?ordinaryWorld:ordinaryWorld-160;
                        bool pressed=!release && phase%40<20;
                        if(pressed && phase%40==0 && !victim.active)OrdinaryTrigger(p);
                        NativeCombatSelectionPreviewChecks.Step(context,selected.Center,pressed);
                    }
                }
                finally{ordinary=false;}
                foreach(var hand in ordinaryHands)Console.WriteLine("MELEE ORDINARY item="+type+" start="+hand.World+" phase="+(hand.World<=40?"cold":hand.World<=100?"continuous":"recovery")+" facing="+hand.Facing+" receipt="+hand.Kind+" point="+hand.Point+" plan="+hand.Plan?.Tick+" confidence="+hand.Plan?.Confidence+" born="+hand.Born+" birthState="+hand.BirthState+" primary="+hand.Primary+" selectedChildFirst="+hand.First+" selectedHand="+hand.Direct);
                int e=ordinaryHands.Count(h=>h.Kind=="Enhanced"),b=ordinaryHands.Count(h=>h.Kind=="Basic"),hits=ordinaryHands.Count(h=>h.First>0),enhancedHits=ordinaryHands.Count(h=>h.Kind=="Enhanced" && h.First>0),recovery=ordinaryHands.Count(h=>h.World>160);
                Require(ordinaryHands.Count>0 && ordinaryHands.All(h=>h.World<=100 || h.World>160),"ordinary release window never invents a new hand");
                Require(e>0 && enhancedHits>0 && recovery>0,"normal budget recovers an actual Enhanced hand -> other receiver -> own child -> selected Strike item="+type+" E="+e+" Basic="+b+" childHits="+hits+" EnhancedHits="+enhancedHits);
                Console.WriteLine("MELEE ORDINARY SUMMARY item="+type+" worlds=280 starts="+ordinaryHands.Count+" E="+e+" Basic="+b+" childBirths="+ordinaryHands.Sum(h=>h.Born)+" hitHands="+hits+" EnhancedHitHands="+enhancedHits+" recoveryStarts="+recovery+" nativeRng=unmodified budget=ordinary");
            }
        }
        internal static void Run(object value)
        {
            // This actual predecessor leaves its legitimate Hive Pack armor;
            // real Player.Update must restore strongBees before our hand.
            if(Environment.GetEnvironmentVariable("JMR_MELEE_HIT_FAMILY_PRECEDE_BEE_HAND")=="1")NativeCombatBeeHandChecks.Run(value);
            // Reproduce the default Effects predecessor in a narrow leaf:
            // it has already executed native NextFloat before our audit starts.
            if(Environment.GetEnvironmentVariable("JMR_MELEE_HIT_FAMILY_PRECEDE_CHILD")=="1")
            {
                // Earlier Effects leaves may compile this wrapper before any
                // lottery audit. Reflection reaches the real method body;
                // the predecessor then resets its own real random seeds.
                typeof(Utils).GetMethod("NextFloat",Flags,null,new[]{typeof(Terraria.Utilities.UnifiedRandom)},null).Invoke(null,new object[]{Main.rand});
                typeof(Utils).GetMethod("NextVector2FromRectangle",Flags,null,new[]{typeof(Terraria.Utilities.UnifiedRandom),typeof(Rectangle)},null).Invoke(null,new object[]{Main.rand,new Rectangle(10,20,94,66)});
                typeof(Player).GetMethod("beeType",Flags).Invoke(Main.LocalPlayer,null);typeof(Player).GetMethod("beeDamage",Flags).Invoke(Main.LocalPlayer,new object[]{10});
                NativeCombatMeleeHitChildChecks.Run(value);
            }
            context=value;observation=Get(value,"CombatObservation");attack=Get(Get(value,"Combat"),"Attack");host=attack.GetType().Assembly;
            var originalRandom=RandomSequence(new Terraria.Utilities.UnifiedRandom(17));
            var originalBees=BeeRandomSequence();
            if(Main.BestiaryTracker==null)Main.BestiaryTracker=new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();
            if(Main.BestiaryDB==null){Main.BestiaryDB=new Terraria.GameContent.Bestiary.BestiaryDatabase();new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator().Populate(Main.BestiaryDB);}
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
            var audit=new Harmony("JueMingR.Tests.MeleeHitFamily");depth=rng=0;
            try
            {
            foreach(var method in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),method.Name=="NextDouble"?nameof(NextDouble):nameof(NextInt)));
            audit.Patch(typeof(Utils).GetMethod("NextFloat",Flags,null,new[]{typeof(Terraria.Utilities.UnifiedRandom)},null),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(NextFloat)));
            audit.Patch(typeof(Terraria.Utilities.UnifiedRandom).GetMethod("InternalSample",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(InternalSample)));
            audit.Patch(typeof(Player).GetMethod("beeType",Flags),transpiler:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(NativeBeeTypeInputs)));
            foreach(var method in new[]{typeof(Player).GetMethod("beeDamage",Flags),typeof(Utils).GetMethod("NextVector2FromRectangle",Flags,null,new[]{typeof(Terraria.Utilities.UnifiedRandom),typeof(Rectangle)},null)})audit.Patch(method,transpiler:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(NativeLotteryInputs)));
            Require(!nativeLottery && originalRandom.SequenceEqual(RandomSequence(new Terraria.Utilities.UnifiedRandom(17))),"outside the component lottery, primitive/integer/float/rectangle draws and subsequent RNG state remain original");
            Require(originalBees.SequenceEqual(BeeRandomSequence()),"outside the component lottery, native normal/strong bee kind/damage/knockback and subsequent RNG state remain original");
            audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(BeforeStart)){priority=Priority.First},finalizer:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(AfterStart)));
            audit.Patch(typeof(Player).GetMethod("ProcessHitAgainstNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(NativeHand)),transpiler:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(NativeLotteryInputs)));
            audit.Patch(typeof(Player).GetMethod("ApplyNPCOnHitEffects",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(OnHit)),transpiler:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(NativeLotteryInputs)));
            audit.Patch(host.GetType("JueMingR.TerrariaHost.Combat.HostMeleeHitFamilyAttack",true).GetMethod("Births",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(ModelHand)),postfix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(ModelBirth)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(Strike)));
            var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatMeleeHitFamilyChecks),nameof(Birth)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            if(Environment.GetEnvironmentVariable("JMR_MELEE_HIT_FAMILY_PARAMETER_ONLY")=="1")ParameterHands();else if(Environment.GetEnvironmentVariable("JMR_MELEE_HIT_FAMILY_ORDINARY_ONLY")=="1")OrdinaryCases();else{if(Environment.GetEnvironmentVariable("JMR_MELEE_HIT_FAMILY_BOUNDARY_ONLY")!="1"){Cases();OwnerWall();PermissionEdges();}CriticalParameters();SlotCases();AmbientCases();TimerParameters();ParameterHands();Require(rng==0,"finite future-hand simulation never reads live RNG");Console.WriteLine("PASS MELEE-HIT-FAMILY RNG0");}
            }
            finally
            {
                audit.UnpatchAll(audit.Id);ordinary=observePermission=prepare=nativeLottery=false;damaging=null;NativeCombatObservationChecks.Save(observation,new ObservationOptions());context=attack=observation=null;
                Require(!Harmony.GetAllPatchedMethods().Any(method=>{var patches=Harmony.GetPatchInfo(method);return patches!=null && patches.Owners.Contains(audit.Id);}),"all native scalar/rectangle/bee/caller lottery hooks are removed with this fixture's audit scope");
                Require(originalRandom.SequenceEqual(RandomSequence(new Terraria.Utilities.UnifiedRandom(17))),"unpatched scalar/rectangle draws and subsequent RNG state remain original after cleanup");
                Require(originalBees.SequenceEqual(BeeRandomSequence()),"unpatched normal/strong bee functions and RNG state remain original after cleanup");
            }
        }
    }
}
