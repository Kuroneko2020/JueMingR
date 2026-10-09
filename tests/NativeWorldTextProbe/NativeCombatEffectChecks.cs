using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatEffectChecks
    {
        private struct BirthCause {internal string Key;internal int Type;}
        private static readonly Dictionary<string,BirthCause> parents=new Dictionary<string,BirthCause>();
        private static int dummyHits;
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.AttackEffects");var birth=typeof(Projectile).GetMethods(BindingFlags.Public|BindingFlags.Static).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));var strike=AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)});
            audit.Patch(birth,prefix:new HarmonyMethod(typeof(NativeCombatEffectChecks),nameof(BeforeBirth)),postfix:new HarmonyMethod(typeof(NativeCombatEffectChecks),nameof(AfterBirth)));audit.Patch(strike,postfix:new HarmonyMethod(typeof(NativeCombatEffectChecks),nameof(Strike)));
            try{RunCore(context);}finally{audit.Unpatch(birth,HarmonyPatchType.All,audit.Id);audit.Unpatch(strike,HarmonyPatchType.All,audit.Id);parents.Clear();}
        }
        private static void BeforeBirth(IEntitySource __0,out BirthCause __state){__state=default(BirthCause);var parent=(__0 as EntitySource_Parent)?.Entity as Projectile;if(parent!=null)__state=new BirthCause{Key=parent.key.ToString(),Type=parent.type};}
        private static void AfterBirth(int __result,BirthCause __state){if(__state.Key!=null && __result>=0 && __result<Main.maxProjectiles)parents[Main.projectile[__result].key.ToString()]=__state;}
        private static void Strike(NPC __instance,int __result){if(__instance.type==488 && __result>0)dummyHits++;}
        private static void RunCore(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            foreach(var row in new[]{new[]{39,516,91,92},new[]{95,515,89,90},new[]{39,3568,639,640},new[]{95,1179,207,0}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;Main.rand=new Terraria.Utilities.UnifiedRandom(7123);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;n.defense=0;n.netOffset=Vector2.Zero;n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);
                var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null,"explicit primary/representative effect family prepares a useful input: "+row[2]);
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(q=>q.active && q.owner==0);Require(parent.type==row[2],"native effect ammo identity");
                string parentKey=parent.key.ToString();var keys=new HashSet<string>();keys.Add(parentKey);int primaryFirst=0,derivedFirst=0,children=0;Vector2 parentBirth=parent.Center,birthVelocity=parent.velocity;
                for(int tick=1;tick<=180;tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);
                    for(int slot=0;slot<Main.maxProjectiles;slot++)
                    {
                        var q=Main.projectile[slot];if(!q.active)continue;
                        if(keys.Add(q.key.ToString())){children++;BirthCause cause;Require(parents.TryGetValue(q.key.ToString(),out cause) && (cause.Key==parentKey || row[2]==91 && cause.Type==92),"actual EntitySource_Parent identifies the natural parent/second-generation star");Console.WriteLine("EFFECT birth: primary="+parentKey+" sourceParent="+cause.Key+" sourceType="+cause.Type+" child="+q.key+" type="+q.type+" tick="+tick+" center="+q.Center+" velocity="+q.velocity+" delay="+q.ai[1]);Require(q.type==row[3],"native derived role remains separate from the primary");if(q.type==640)Require(q.Center==parentBirth && q.velocity==birthVelocity,"night arrow trail returns to sealed birth origin/velocity");}
                        int oldLife=n.life;Main.ProjectileUpdateLoopIndex=slot;try{q.Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}
                        if(n.life<oldLife){if(q.type==row[2] && primaryFirst==0)primaryFirst=tick;else if(q.type==row[3] && derivedFirst==0)derivedFirst=tick;}
                    }
                }
                Console.WriteLine("EFFECT result: primary="+row[2]+" expected="+plan.Tick+" first="+primaryFirst+" children="+children+" derivedDamage="+derivedFirst+" life="+n.life);
                Require(primaryFirst>0,"natural primary has a useful result");Require(plan.Confidence==AttackConfidence.Representative || primaryFirst==plan.Tick,"deterministic primary first Damage matches Contact.Tick");if(row[3]!=0)Require(children>0,"natural death emits its real derived stage");
            }
            NativeCombatProjectileEnvironmentChecks.Run(context);
            Explosion(context,combat,host,attack,input);Explosion(context,combat,host,attack,input,true);
            Bounce(context,combat,host,attack,input);Bounce(context,combat,host,attack,input,true);
            HomingCases(context,combat,host,attack,input);
            NativeCombatObstacleChecks.Run(context);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static void HomingCases(object context,object combat,object host,object attack,object input)
        {
            foreach(int scene in new[]{0,1,2})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(1179);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(scene==2?488:3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.target=0;n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);if(scene!=2)n.life=n.lifeMax=10000;
                if(scene==0)for(int y=20;y<=43;y++){Main.tile[56,y].active(true);Main.tile[56,y].type=1;}
                if(scene==1){var other=Main.npc[3];other.SetDefaults(3);other.whoAmI=3;other.active=true;other.position=new Vector2(1000,646);other.life=other.lifeMax=10000;other.target=0;other.netOffset=Vector2.Zero;Array.Clear(other.immune,0,other.immune.Length);}
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");Call(Get(host,"Selection"),"SampleMouse",input);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,scene==2,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");dummyHits=0;
                typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.type==207);int first=0;bool untracked=n.CanBeChasedBy(null);
                for(int tick=1;tick<=45 && q.active;tick++){q.Update(q.whoAmI);if(scene==2?dummyHits>0:n.life<10000){first=tick;break;}}
                Console.WriteLine("EFFECT homing boundary: scene="+scene+" contact="+plan?.Tick+" first="+first+" nativeTarget="+q.ai[1]+" chaseable="+untracked+" dummyHits="+dummyHits+" blockerLife="+Main.npc[3].life);
                Require(scene==2?plan!=null && first>0 && !untracked && n.immortal && q.ai[1]==0:plan==null && first==0,"native autonomous wall/nearer target differs from valid direct immortal-dummy hit");if(scene==1)Require(Main.npc[3].life<10000,"native homing chooses and hits the nearer NPC without product steering");
            }
        }
        private static void Bounce(object context,object combat,object host,object attack,object input,bool closed=false)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(234);p.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(850,620);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Main.tile[49,40].active(true);Main.tile[49,40].type=1;
            if(closed)for(int y=39;y<=43;y++){Main.tile[51,y].active(true);Main.tile[51,y].type=1;}
            NativeToolExecutionChecks.Sample(context,input,new Vector2(810,708),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.type==36);int first=0;bool bounced=false;
            for(int tick=1;tick<=60 && q.active;tick++){var old=q.velocity;q.Update(q.whoAmI);if(old.Y>0 && q.velocity.Y<0)bounced=true;if(n.life<10000){first=tick;break;}}
            Console.WriteLine("EFFECT bounce: closed="+closed+" expected="+plan?.Tick+" first="+first+" bounce="+bounced+" remaining="+q.penetrate+" center="+q.Center);Require(closed?first==0:first>0 && bounced,"native MeteorShot reflection respects the open/closed passage");Require(closed?plan==null:plan!=null && plan.Tick==first,"finite reflection contact matches first natural Damage without changing native bounce");
        }
        private static void Explosion(object context,object combat,object host,object attack,object input,bool far=false)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,759,0,0);p.position=new Vector2(700,646);p.ResetEffects();Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(771);p.inventory[54].stack=999;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(far?990:920,610);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
            for(int y=20;y<=43;y++){Main.tile[56,y].active(true);Main.tile[56,y].type=1;}
            NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(true,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.type==134);int first=0;
            Console.WriteLine("EFFECT rocket birth: position="+q.position+" size="+q.width+"x"+q.height+" velocity="+q.velocity+" speed="+p.HeldItem.shootSpeed+" plan="+plan?.ImpactX+","+plan?.ImpactY);
            for(int tick=1;tick<=60 && q.active;tick++){q.Update(q.whoAmI);if(tick>=16)Console.WriteLine("EFFECT rocket phase: tick="+tick+" center="+q.Center+" velocity="+q.velocity+" active="+q.active);if(n.life<10000){first=tick;break;}}
            Console.WriteLine("EFFECT wall explosion: far="+far+" expected="+plan?.Tick+" first="+first+" active="+q.active+" body="+q.width+"x"+q.height+" center="+q.Center);
            bool expanded=false;foreach(var sample in (Array)Get(Get(host,"Geometry"),"Events"))if(sample!=null && (int)Get(sample,"Type")==134 && (int)Get(sample,"Count")>0){var shape=((Array)Get(sample,"Shapes")).GetValue(0);var a=(Vector2)Get(shape,"A");var b=(Vector2)Get(shape,"B");expanded|=b.X-a.X==128 && b.Y-a.Y==128;}
            // Kill restores/resizes the visual body after its Damage call.
            // Observe the existing natural event lane at the actual window,
            // rather than querying Damage or reading the final visual size.
            Require(!q.active && expanded,"native wall Kill emits its observed 128 damage rectangle beyond the untraversable wall");Require(far?first==0 && plan==null:first>0 && plan!=null && plan.Tick==first,"known explosion trigger/area distinguishes target inside/outside the native area");
        }
    }
}
