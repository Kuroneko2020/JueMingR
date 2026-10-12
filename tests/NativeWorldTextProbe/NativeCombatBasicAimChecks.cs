using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The oracle is vanilla's natural birth/receive and the owner's input
    // contract. A distant target is deliberately selected without a Contact;
    // no test supplies a contact, calls Shoot or repairs preparation afterward.
    internal static class NativeCombatBasicAimChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Player player;
        private static NPC target;
        private static Vector2 physical;
        private static int itemType,parentType,childType,parentBirths,childBirths,parentHits,childHits,damageType,assistedConsumers,manualConsumers,enhancedConsumers;
        private static object currentAttack;
        private static bool assisted,pressed,reverseGravity;
        private static int handStarts;
        private static readonly List<string> consumers=new List<string>();

        private static void Birth(int __result)
        {
            if(player==null || __result<0 || __result>=Main.maxProjectiles)return;
            var shot=Main.projectile[__result];
            if(shot.owner!=player.whoAmI || shot.type!=parentType && shot.type!=childType)return;
            if(shot.type==childType){childBirths++;return;}
            parentBirths++;
            var cursor=new Vector2(Main.mouseX+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-Main.mouseY:Main.mouseY+Main.screenPosition.Y);
            bool permitted=assisted && pressed;
            if(permitted)assistedConsumers++;else manualConsumers++;
            if(permitted && Get(Get(currentAttack,"consumedInput"),"Kind").ToString()=="Enhanced")enhancedConsumers++;
            consumers.Add("tick="+Main.GameUpdateCount+" cursor="+cursor+" target="+target.Center+" birth="+shot.Center+" velocity="+shot.velocity+" permitted="+permitted);
            Require(permitted?cursor.X>player.Center.X && Vector2.DistanceSquared(cursor,physical)>4:Vector2.DistanceSquared(cursor,physical)<2,
                "every natural birth consumes the legal selected-target or manual cursor: item="+itemType);
            if(itemType==3818){Require(Vector2.DistanceSquared(cursor,target.Center+target.netOffset)<4,"enhanced-failed DD2 birth consumes its admitted selected placement point");return;}
            if(itemType==65 && permitted)
            {
                // Starfury's sky birth and tile gate use the actual cursor
                // location. A long direction ray would change both semantics.
                Require(Vector2.DistanceSquared(cursor,target.Center)<4,"Starfury without a receive solution consumes the current target world coordinate");
                Require(Math.Abs(shot.Center.X-target.Center.X)<220 && shot.Center.Y<player.MountedCenter.Y-590,"Starfury's original sky birth follows the selected target X");
            }
            else if(itemType!=65)
            {
                var ray=shot.velocity.SafeNormalize(Vector2.UnitX);
                var intended=(permitted?target.Center:physical)-shot.Center;
                Require(Vector2.Dot(ray,intended.SafeNormalize(Vector2.UnitX))>.85f,"real short blade/spear initial velocity follows the chosen input");
            }
        }
        private static void BeforeDamage(Projectile __instance,out int __state){__state=damageType;damageType=__instance.type;}
        private static void AfterDamage(int __state){damageType=__state;}
        private static void Hit(NPC __instance,int __result)
        {
            if(!ReferenceEquals(__instance,target) || __result<=0)return;
            if(damageType==parentType)parentHits++;
            if(damageType==childType)childHits++;
        }
        private static void HandStarted(Player __instance,Item __0)
        {
            if(!ReferenceEquals(__instance,player) || itemType!=ItemID.WoodenSword || __0.type!=itemType)return;
            handStarts++;
            Require(player.itemAnimation>0 && player.direction==(assisted && pressed?1:-1),"natural admitted pure-swing start consumes target direction with independent Facing OFF");
        }
        private static int placementChecks;
        private static void PlacementChecked(Player __instance,Item __0)
        {
            if(!ReferenceEquals(__instance,player) || itemType!=3818 || __0.type!=itemType || !__instance.controlUseItem)return;
            placementChecks++;
            Require(Vector2.DistanceSquared(Main.screenPosition+new Vector2(Main.mouseX,Main.mouseY),target.Center+target.netOffset)<4,"original DD2 CanUse terrain gate reads the same placement point despite an enhanced fault");
        }
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.BasicAimNaturalConsumers");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})
                audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13);
            audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatBasicAimChecks),nameof(Birth)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBasicAimChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatBasicAimChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBasicAimChecks),nameof(Hit)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags),postfix:new HarmonyMethod(typeof(NativeCombatBasicAimChecks),nameof(HandStarted)){priority=Priority.Last});
            audit.Patch(typeof(Player).GetMethod("ItemCheck_CheckCanUse_Inner",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBasicAimChecks),nameof(PlacementChecked)){priority=Priority.Last});
            var failures=new List<string>();
            try
            {
                foreach(bool assist in new[]{false,true})
                {
                    Check(context,3507,938,0,85,false,assist,failures);
                    Check(context,65,9,0,85,false,assist,failures);
                    foreach(int x in new[]{49,60,85})Check(context,1228,222,228,x,false,assist,failures);
                    Check(context,1228,222,228,60,true,assist,failures);
                }
                // Intermediate distances make the old solution-to-null input
                // boundary observable, rather than testing just its endpoints.
                foreach(int x in new[]{52,56,64,70})Check(context,1228,222,228,x,false,true,failures);
                reverseGravity=true;Check(context,65,9,0,85,false,true,failures);reverseGravity=false;
                PureSwing(context);
                FutureFaultBasic(context);
                PreviewBasicCompatibility(context);
                PlacementBasicFault(context);
                YoyoBodyFallback(context);
                Require(failures.Count==0,string.Join(" | ",failures));
            }
            finally
            {
                player=null;target=null;reverseGravity=false;
                NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
                NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
                foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);
            }
        }
        private static void Check(object context,int item,int parent,int child,int tileX,bool moving,bool assist,List<string> failures)
        {
            try{Scene(context,item,parent,child,tileX,moving,assist);}
            catch(Exception error){failures.Add(item+"/"+tileX+"/moving="+moving+"/assist="+assist+": "+error.Message);Console.WriteLine("FAIL BASIC "+error+" consumers="+string.Join(";",consumers));}
        }
        private static void Scene(object context,int item,int parent,int child,int tileX,bool moving,bool assist)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");currentAttack=attack;
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,item,0,0);player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;
            if(reverseGravity){player.AddBuff(BuffID.Gravitation,600);player.gravDir=-1;}
            Main.screenPosition=new Vector2(480,400);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
            target=NativeCombatControlPreviewChecks.Dummy(2,tileX,40);physical=new Vector2(550,430);
            itemType=item;parentType=parent;childType=child;assisted=assist;pressed=false;
            parentBirths=childBirths=parentHits=childHits=damageType=assistedConsumers=manualConsumers=enhancedConsumers=0;consumers.Clear();
            // This is a real receive wait, not target revocation. It forces
            // Starfury's contact to remain unknown across this finite window.
            if(item==65)target.immune[player.whoAmI]=500;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,50,true,assist));
            for(int i=0;i<3;i++)Step(context,false,false);
            Require(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==target.whoAmI,"range-valid far receiver remains the shared final target");
            if(item==3507 || tileX==85)Require(GetOptional(attack,"ExpectedPreview")==null,"independent distant/immunity scene has no claimed receive solution");
            Vector2 initial=player.position;
            for(int i=0;i<75;i++)Step(context,i<30 || i>=40,moving && i<25);
            Require(parentBirths>=2 && parentBirths==assistedConsumers+manualConsumers,"all natural first/repress consumers have a recorded denominator");
            if(assist)Require(assistedConsumers>=2 && manualConsumers==0,"no allowed natural birth silently reverts to the opposite physical mouse");
            else Require(manualConsumers==parentBirths,"Aim OFF preserves every original manual birth");
            if(moving)Require(player.position.X>initial.X+20,"movement scene executes continuous original player movement");
            if(item==3507 || tileX==85)Require(parentHits+childHits==0,"basic assistance does not invent distant blade/spear or immunity-blocked damage");
            if(item==1228 && assist && !moving && tileX==49)Require(parentHits>0,"body-reachable Chlorophyte retains natural damage");
            if(item==1228 && assist && !moving && tileX==60)Require(childHits>0,"spore-reachable Chlorophyte retains natural derived damage");
            if(item==1228 && assist && !moving && tileX==49)Require(enhancedConsumers>0,"a healthy in-range Chlorophyte still consumes its enhanced solution");
            // Disable assistance while keeping the same native source and
            // receiver. A new physical press must really consume manual input.
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,50,true,false));assisted=false;
            for(int i=0;i<40;i++)Step(context,false,false);
            int oldManual=manualConsumers;for(int i=0;i<30;i++)Step(context,true,false);
            Require(manualConsumers>oldManual,"Aim revocation takes effect at the next real consumer");
            Console.WriteLine("PASS BASIC item="+item+" targetTileX="+tileX+" moving="+moving+" reverseGravity="+reverseGravity+" assist="+assist+" consumers="+parentBirths+" assisted="+assistedConsumers+" enhanced="+enhancedConsumers+" manual="+manualConsumers+" parentHits="+parentHits+" childHits="+childHits);
        }
        private static void PureSwing(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");
            foreach(bool assist in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,ItemID.WoodenSword,0,0);player.position=new Vector2(700,646);player.direction=-1;
                target=NativeCombatControlPreviewChecks.Dummy(2,85,40);physical=new Vector2(550,430);itemType=ItemID.WoodenSword;parentType=childType=0;assisted=assist;pressed=false;handStarts=0;
                Require(player.HeldItem.shoot<=0 && !(bool)Call(combat,"IsEnabled",5),"pure swung counterexample has no projectile consumer and Facing remains OFF");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,50,true,assist));
                for(int i=0;i<3;i++)Step(context,false,false);
                for(int i=0;i<75;i++)Step(context,i<30 || i>=40,false);
                Require(handStarts>=2 && target.life==target.lifeMax,"first and repeated out-of-range hand uses turn without invented damage");
                Console.WriteLine("PASS BASIC pure-swing item="+itemType+" assist="+assist+" naturalStarts="+handStarts+" direction="+player.direction);
            }
        }
        private static void FutureFaultBasic(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            currentAttack=attack;
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3507,0,0);player.position=new Vector2(700,646);player.releaseUseItem=true;
            target=NativeCombatControlPreviewChecks.Dummy(2,85,40);physical=new Vector2(550,430);itemType=3507;parentType=938;childType=0;assisted=true;pressed=false;parentBirths=assistedConsumers=manualConsumers=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,true));
            for(int i=0;i<3;i++)Step(context,false,false);
            // Fault injection is restricted to enhancement state; the actual
            // native selector/update and Shoot still have to process a NEW NPC.
            Set(host,"pathFailed",true);Call(attack,"FailLocal",new InvalidOperationException("isolated enhanced fault"));target.life=0;
            target=NativeCombatControlPreviewChecks.Dummy(3,85,40);
            for(int i=0;i<10;i++)Step(context,true,false);
            Require(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==3 && parentBirths==1 && assistedConsumers==1 && manualConsumers==0,"Path/Marker OFF and failed future still select a new receiver and assist its natural birth");
            Require((bool)Get(host,"pathFailed") && GetOptional(attack,"ExpectedImpact")==null,"basic consumer does not recover or invent failed enhancement");
            foreach(string boundary in new[]{"death","weapon","ui","focus","off","selector","identity"})
            {
                var weapon=player.HeldItem;bool dead=player.dead;
                if(boundary=="death")player.dead=true;
                if(boundary=="identity")typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{target.whoAmI,(byte)0});
                if(boundary=="weapon")player.inventory[0]=weapon.Clone();
                if(boundary=="ui")Main.drawingPlayerChat=true;
                if(boundary=="focus"){Set(input,"foregroundWindow",(Func<IntPtr>)(()=>IntPtr.Zero));NativeToolExecutionChecks.Sample(context,input,physical,true);Call(combat,"Sample");}
                if(boundary=="off")NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,false));
                if(boundary=="selector")Set(host,"selectionFailed",true);
                Require(Call(attack,"BeginShot",player,weapon,true)==null,"basic actual input rejects "+boundary);
                player.dead=dead;player.inventory[0]=weapon;Main.drawingPlayerChat=false;Set(host,"selectionFailed",false);
                Set(input,"foregroundWindow",(Func<IntPtr>)(()=>new IntPtr(1)));NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,true));
                NativeToolExecutionChecks.Sample(context,input,physical,false);Call(combat,"Sample");NativeToolExecutionChecks.Sample(context,input,physical,true);Call(combat,"Sample");
            }
            Set(host,"pathFailed",false);
            Console.WriteLine("PASS BASIC future-fault selection/native consumer and death/identity/weapon/UI/focus/OFF/selector rejection");
        }
        private static void Step(object context,bool left,bool rightward)
        {
            pressed=left;
            if(!rightward){NativeCombatSelectionPreviewChecks.Step(context,physical,left);return;}
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),physical,left);
            PlayerInput.Triggers.Current.Right=true;Call(combat,"Sample");Call(host,"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();player.Update(player.whoAmI);
            foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
            for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}Main.ProjectileUpdateLoopIndex=-1;
            Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(Main.mouseX==x && Main.mouseY==y,"natural moving consumer restores the physical cursor");
        }
        private static void PreviewBasicCompatibility(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");currentAttack=attack;
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,ItemID.WoodenBow,0,0);player.position=new Vector2(700,646);player.releaseUseItem=true;
            player.inventory[54].SetDefaults(ItemID.WoodenArrow);player.inventory[54].stack=999;
            target=NativeCombatControlPreviewChecks.Dummy(2,60,40);physical=new Vector2(550,430);itemType=ItemID.WoodenBow;parentType=1;childType=0;assisted=true;pressed=false;parentBirths=assistedConsumers=manualConsumers=enhancedConsumers=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,50,false,true));
            for(int i=0;i<3;i++)Step(context,false,false);
            Require(GetOptional(attack,"ExpectedPreview")!=null,"idle bow has an independently solved arc before the injected execution fault");
            Call(attack,"FailLocal",new InvalidOperationException("isolated bow execution fault"));
            Step(context,true,false);
            var inputFact=Get(attack,"consumedInput");
            Require(parentBirths==1 && assistedConsumers==1 && Get(inputFact,"Kind").ToString()=="Basic","actual natural bow birth uses Basic when only enhanced execution failed");
            var free=(AttackContact)GetOptional(Get(attack,"preview"),"Contact");
            Require(free!=null,"independent enhanced preview still has a solved arc; absence is not this oracle");
            var origin=(Vector2)Get(attack,"consumedOrigin");var actual=((Vector2)Get(inputFact,"Point")-origin).SafeNormalize(Vector2.Zero);var proposed=(new Vector2(free.AimX,free.AimY)-origin).SafeNormalize(Vector2.Zero);
            Require(Vector2.DistanceSquared(actual,proposed)>.000001f,"original gravity makes the free arc require a different input from the consumed current-target ray");
            Require(GetOptional(attack,"ExpectedPreview")==null,"actual Basic does not display another freely solved arc's precise contact");
            Step(context,true,false);
            Require(GetOptional(attack,"ExpectedPreview")==null,"a still-failed held Basic hand does not restore a different free arc between native births");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,50,false,false));assisted=false;
            for(int i=0;i<40;i++)Step(context,false,false);
            Require(GetOptional(attack,"ExpectedPreview")!=null && parentBirths==1,"idle Path-only recovers the read-only arc without acquiring another native input");
            Console.WriteLine("PASS BASIC actual bow input/free-preview incompatibility and idle Path-only hypothesis preservation");
        }
        private static void PlacementBasicFault(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");currentAttack=attack;
            player=(Player)typeof(NativeCombatPlacementChecks).GetMethod("Setup",Flags).Invoke(null,new object[]{context,combat,host,input,3818});
            target=(NPC)typeof(NativeCombatPlacementChecks).GetField("target",Flags).GetValue(null);physical=new Vector2(600,500);itemType=3818;parentType=player.HeldItem.shoot;childType=0;assisted=true;pressed=false;parentBirths=assistedConsumers=manualConsumers=enhancedConsumers=placementChecks=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,false,true));
            for(int i=0;i<3;i++)Step(context,false,false);
            Call(attack,"FailLocal",new InvalidOperationException("isolated placement enhancement fault"));
            Step(context,true,false);
            Require(parentBirths==1 && assistedConsumers==1 && placementChecks>0 && GetOptional(attack,"placementUse")==null,"one real DD2 CanUse/Shoot shares a completed receipt despite Failed");
            // The following two component negatives reuse a naturally expired
            // cooldown; they do not create extra native uses or birth counts.
            for(int i=0;i<70;i++)Step(context,false,false);
            NativeToolExecutionChecks.Sample(context,input,physical,true);Call(combat,"Sample");
            var receipt=Call(attack,"EnterPlacementUse",player);Require(receipt!=null,"known placement point has its own admitted-use receipt");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,false,false));
            Require(Call(attack,"BeginPlacement",player,player.HeldItem,true)==null,"Aim OFF revokes placement receipt input");Call(receipt,"End");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,false,true));
            receipt=Call(attack,"EnterPlacementUse",player);Require(receipt!=null,"second isolated placement receipt admitted");var item=player.HeldItem;player.inventory[0]=item.Clone();
            Require(Call(attack,"BeginPlacement",player,player.HeldItem,true)==null,"replaced weapon identity cannot consume an old placement receipt");Call(receipt,"End");
            Console.WriteLine("PASS BASIC enhanced-failed DD2 native CanUse/Shoot and Aim OFF/receipt identity rejection");
        }
        private static void YoyoBodyFallback(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            foreach(int gap in new[]{0,8,16})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);player.position=new Vector2(700,646);
                target=NativeCombatControlPreviewChecks.Dummy(2,54,40);physical=new Vector2(550,430);assisted=false;pressed=false;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,50,true,false));
                for(int i=0;i<3;i++)Step(context,false,false);
                // This bounded geometry check is separate from native usage:
                // the current 16px ball intent must fit the actual opening,
                // without claiming a navigation route or a future contact.
                player.position.Y=643;target.Center=new Vector2(target.Center.X,664);
                for(int y=5;y<115;y++)NativeToolsChecks.Tile(50,y,1);
                if(gap>0)Main.tile[50,41].ClearEverything();
                if(gap==8){NativeToolsChecks.Tile(50,41,1);Main.tile[50,41].halfBrick(true);}
                var phaseType=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);
                var args=new object[]{player,player.HeldItem,null,Enum.Parse(phaseType,"BeforeNpc"),null};
                bool up=Collision.up,down=Collision.down;
                Require((bool)attack.GetType().GetMethod("TryBasicPreviewInput",Flags).Invoke(attack,args),"path-only yoyo current intent exists without acquiring input permission");
                var point=(Vector2)Get(args[4],"Point");var sample=ContentSamples.ProjectilesByType[player.HeldItem.shoot];
                Require(sample.width==16 && sample.height==16,"yoyo body counterexample uses its original 16px dimensions");
                Require(gap<16?point.X+sample.width*.5f<=800:point.X>816,"solid wall and 8px gap stop the whole body; a 16px gap admits its direct intent: gap="+gap+" point="+point);
                Require(Collision.up==up && Collision.down==down,"basic body passage leaves vanilla shared collision flags untouched");
                Require(Vector2.Distance(point,player.Center)<=Terraria.ID.ProjectileID.Sets.YoyosMaximumRange[player.HeldItem.shoot]+.01f,"basic yoyo point respects the original line length");
                Console.WriteLine("PASS BASIC yoyo current-body geometry gap="+gap+" point="+point+" source="+player.RotatedRelativePoint(player.MountedCenter));
            }
        }
    }
}
