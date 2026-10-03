using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Utilities;
using JueMingR.Features.Combat;

namespace NativeWorldTextProbe
{
    // The locked original defines the oracle: dungeon shots end the factory
    // with timeLeft=300; Ichor children have penetrate=maxPenetrate=1. Hold
    // mailbox consumption only, never the original updates or proof history.
    internal static class NativeCombatModifierBirthChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static object worker;
        private static bool hold,inject;
        private static bool off;
        private static int offDungeon,offIchor,patchReads;
        private static string scenario;
        private static readonly List<int> born=new List<int>();
        private static readonly List<string> births=new List<string>();
        private static bool Mailbox(object __instance)=>!hold || !ReferenceEquals(worker,__instance);
        private static void Equivalent(Projectile p){p.timeLeft=300;}
        private sealed class InstanceModifier {internal void Apply(Projectile p){p.timeLeft=300;}}
        private static void Patched(){}
        private static void ReadPatches(MethodBase __0)
        {if(__0.DeclaringType==typeof(NewProjectileModifiers) || __0.DeclaringType==typeof(Projectile) && (__0.Name=="NewProjectile" || __0.Name=="SetDefaults"))patchReads++;}
        private static void Modify(ref NewProjectileModifier __12)
        {
            if(!inject || __12==null)return;
            inject=false;
            if(scenario=="foreign-equivalent")__12=Equivalent;
            if(scenario=="multicast")__12=(NewProjectileModifier)Equivalent+NewProjectileModifiers.HardmodeDungeonSkeletonShot;
            if(scenario=="duplicate-native")__12+=NewProjectileModifiers.HardmodeDungeonSkeletonShot;
            if(scenario=="instance")__12=new InstanceModifier().Apply;
            if(scenario=="replaced-native")__12=NewProjectileModifiers.IchorDartUpdatePenetrate;
            if(scenario=="removed-native")__12=null;
        }
        private static void Born(int __result)
        {
            if(__result<0 || __result>=Main.maxProjectiles)return;
            if(off)
            {
                var value=Main.projectile[__result];
                if(value.type==291 && value.timeLeft==300)offDungeon++;
                if(value.type==479 && value.ai[1]==-1000 && value.penetrate==1 && value.maxPenetrate==1)offIchor++;
            }
            if(!hold)return;
            var p=Main.projectile[__result];born.Add(p.type);
            births.Add(string.Join(",",scenario,Main.GameUpdateCount,p.whoAmI,p.type,(uint)p.key,p.timeLeft,p.penetrate,p.maxPenetrate));
            if(scenario.StartsWith("dungeon-",StringComparison.Ordinal) && (p.type==290 || p.type==291 || p.type==293))
                Require(p.timeLeft==300,"Original dungeon modifier sets timeLeft at the actual factory return.");
            if(scenario=="ichor" && p.type==479)Require(p.penetrate==1 && p.maxPenetrate==1,"Original Ichor child modifier preserves both penetration fields.");
        }
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            worker=Get(native,"Worker");var hooks=new Harmony("JueMingR.Tests.ModifierBirth");
            var scalar=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters()[1].ParameterType==typeof(float));
            var rows=new List<string>{"case,capture,arrive,history,births,events,retired,lifecycleInvalid,impact,accepted,reason"};
            births.Clear();births.Add("case,tick,slot,type,key,timeLeft,penetrate,maxPenetrate");
            hooks.Patch(worker.GetType().GetMethod("TryTakeResult",Flags),prefix:Hook(nameof(Mailbox)));
            hooks.Patch(scalar,prefix:Hook(nameof(Modify)),postfix:Hook(nameof(Born)));
            var trace=new NativeCombatAttackTrace(native,output);
            try
            {
                foreach(string mode in (Environment.GetEnvironmentVariable("JUEMINGR_MODIFIER_CASES")??"dungeon-285,dungeon-281,dungeon-283,ichor,foreign-equivalent,multicast,duplicate-native,instance,replaced-native,removed-native,patched-native,external-native").Split(','))
                {
                    scenario=mode;hold=inject=false;born.Clear();Call(native,"ClearTarget");trace.Phase="modifier-setup";trace.RequiredCapture=-1;
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
                    var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.velocity=Vector2.Zero;
                    player.controlLeft=player.controlRight=player.controlJump=false;player.dead=false;player.statLife=player.statLifeMax=player.statLifeMax2=100000;
                    player.immune=true;player.immuneTime=100000;player.wet=player.lavaWet=player.honeyWet=player.shimmerWet=false;player.fallStart=player.fallStart2=150;
                    Array.Clear(player.buffType,0,player.buffType.Length);Array.Clear(player.buffTime,0,player.buffTime.Length);Array.Clear(player.hurtCooldowns,0,player.hurtCooldowns.Length);
                    for(int i=0;i<10;i++)player.armor[i].TurnToAir();
                    typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
                    int type=mode.StartsWith("dungeon-",StringComparison.Ordinal)?int.Parse(mode.Substring(8)):mode=="ichor"?1:285;
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,type,Start:16,Target:0);var target=Main.npc[slot];target.life=target.lifeMax=100000;
                    NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:true,clearLine:false,mouseCenter:true,dummy:true,radius:25));
                    Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                    Action advance=()=>
                    {
                        // This closure test explicitly provides empty spawn
                        // slots; unscripted page discovery is tested separately.
                        ((SortedSet<int>)Get(native,"npcs")).Add(slot);for(int i=0;i<8;i++)((SortedSet<int>)Get(native,"projectiles")).Add(i);
                        NativeCombatModeledImpactChecks.SampleMouse(context,target.Center);step();
                        ((SortedSet<int>)Get(native,"npcs")).Add(slot);for(int i=0;i<8;i++)((SortedSet<int>)Get(native,"projectiles")).Add(i);
                        Require(ReferenceEquals(worker,Get(native,"Worker")) && !(bool)Get(native,"Failed"),"One healthy Session/worker throughout modifier proof.");
                        Require(player.statLife==100000 && !player.dead,"Modifier scenario is unhurt.");
                    };
                    object pending=null;long start=Main.GameUpdateCount,ichorStart=-1;var wait=Stopwatch.StartNew();
                    while(wait.Elapsed.TotalSeconds<5)
                    {
                        advance();pending=Get(native,"pending");
                        if(pending!=null && (long)Get(pending,"Tick")>start && !(bool)Get(pending,"Retired")
                            && ((int[])Get(pending,"Projectiles")).Length>=8 && Main.GameUpdateCount-(long)Get(pending,"Tick")<=2
                            && (mode=="ichor" || target.ai[1]==0 && target.ai[0]%100>=90 && target.ai[0]%100<=98))
                        {
                            if(mode=="ichor" && ichorStart<0)
                            {
                                // Spawn only after the empty pages are learned,
                                // then require a fresh capture of this parent.
                                Projectile.NewProjectile(new EntitySource_Parent(target),new Vector2(1700,2200),new Vector2(1,0),479,10,0,Main.myPlayer);
                                ichorStart=Main.GameUpdateCount;
                            }
                            else if(mode!="ichor" || (long)Get(pending,"Tick")>ichorStart && Main.projectile[0].ai[1]>=0)break;
                        }
                        pending=null;
                    }
                    Require(pending!=null,"Capture the prepared original attack before birth.");
                    hold=true;born.Clear();
                    long capture=(long)Get(pending,"Tick");trace.Phase="modifier-"+mode;trace.RequiredCapture=capture;trace.Selected=slot;
                    var adversary=new Harmony("JueMingR.Tests.ModifierAdversary");
                    bool positive=mode.StartsWith("dungeon-",StringComparison.Ordinal) || mode=="ichor";
                    try
                    {
                        if(!positive)inject=true;
                        if(mode=="patched-native")adversary.Patch(typeof(NewProjectileModifiers).GetMethod("HardmodeDungeonSkeletonShot"),postfix:Hook(nameof(Patched)));
                        if(mode=="external-native")Projectile.NewProjectile(new EntitySource_Parent(target),target.Center,new Vector2(-8,0),291,40,0,Main.myPlayer,player.Center.X,player.Center.Y,0,NewProjectileModifiers.HardmodeDungeonSkeletonShot);
                        int steps=mode=="dungeon-285"?54:mode=="ichor"?35:20;
                        for(int i=0;i<steps;i++)advance();
                    }
                    finally{adversary.UnpatchAll(adversary.Id);inject=false;}
                    Require(born.Count>0,"Actual original factory was exercised: "+mode);
                    if(mode=="dungeon-285")Require(born.Contains(291) && born.Contains(292),"Actual Diabolist shot reaches its target and creates the Kill child.");
                    if(mode=="ichor")Require(born.Count>=2 && born.All(t=>t==479),"Actual Ichor split children, not another actor's birth, cross the capture.");
                    hold=false;for(int i=0;i<5 && ReferenceEquals(pending,Get(native,"pending"));i++)advance();
                    bool accepted=ReferenceEquals(pending,Get(native,"acceptedRequest"));
                    rows.Add(string.Join(",",mode,capture,Main.GameUpdateCount,((IList)Get(pending,"History")).Count,born.Count,((IList)Get(Get(pending,"Impacts"),"hits")).Count,Get(pending,"Retired"),Get(pending,"LifecycleInvalid"),Get(pending,"Impact"),accepted,Get(native,"Reason")));
                    if(positive)
                    {
                        Require(!(bool)Get(pending,"Retired") && !(bool)Get(pending,"LifecycleInvalid") && (int)Get(pending,"Impact")==0,"Original modifier birth preserves the pending lifecycle: "+mode);
                        Require(((IList)Get(pending,"History")).Count==Main.GameUpdateCount-capture+1,"Every intervening original history frame reaches Receive.");
                        Require(accepted && cache.Read(0)!=null && cache.Read(0).SampleTick==Main.GameUpdateCount && cache.Read(0).Count==121,"Real Receive accepts modifier lineage and publishes current+120: "+mode+" reason="+Get(native,"Reason"));
                        var events=(IList)Get(Get(pending,"Impacts"),"hits");Require(events.Count>=born.Count,"All actual births have ordered proof events.");
                    }
                    else Require(!accepted && (bool)Get(pending,"Retired") && ((bool)Get(pending,"LifecycleInvalid") || (int)Get(pending,"Impact")!=0),"External or replaced modifier cannot borrow native birth authority: "+mode);
                    Console.WriteLine("MODIFIER-PROOF "+mode+" capture="+capture+" births="+born.Count+" history="+((IList)Get(pending,"History")).Count+" accepted="+accepted);
                }
                // Native modifier bodies must still run while observation is
                // OFF, without patch-metadata reads or a proof allocation.
                Call(native,"ClearTarget");NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
                NPC.ClearAll();Projectile.ClearAll();
                int npc=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,285,Start:16,Target:0);
                Main.npc[npc].life=Main.npc[npc].lifeMax=100000;
                var observer=native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeProjectileBirth",true);
                observer.GetField("hash",Flags).SetValue(null,null);
                hooks.Patch(typeof(Harmony).GetMethod("GetPatchInfo",Flags),prefix:Hook(nameof(ReadPatches)));
                patchReads=offDungeon=offIchor=0;off=true;long requests=(long)Get(native,"Requests");
                Projectile.NewProjectile(new EntitySource_Parent(Main.npc[npc]),new Vector2(1700,2200),new Vector2(1,0),479,10,0,Main.myPlayer);
                for(int i=0;i<150;i++)step();
                Require(offDungeon>0 && offIchor>=2,"Both original modifier mechanisms still execute with prediction OFF.");
                Require(patchReads==0 && observer.GetField("hash",Flags).GetValue(null)==null && observer.GetField("factory",Flags).GetValue(null)==null,"OFF native modifier births do not inspect patches or create proof writers/factories.");
                Require((long)Get(native,"Requests")==requests && Get(native,"pending")==null && Get(native,"acceptedRequest")==null && cache.Read(0)==null,"OFF native modifier births cannot start or retain prediction.");
                Console.WriteLine("MODIFIER-OFF dungeon="+offDungeon+" ichor-children="+offIchor+" patch-reads="+patchReads+" requests=0 writer=false factory=false");
            }
            finally{trace.Dispose();hold=inject=off=false;worker=null;hooks.UnpatchAll(hooks.Id);new Harmony("JueMingR.Tests.ModifierAdversary").UnpatchAll("JueMingR.Tests.ModifierAdversary");File.WriteAllLines(Path.Combine(output,"modifier-proof.csv"),rows);File.WriteAllLines(Path.Combine(output,"modifier-births.csv"),births);}
        }
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeCombatModifierBirthChecks).GetMethod(name,Flags));
        private static object Get(object value,string name){if(value==null)return null;var field=value.GetType().GetField(name,Flags);return field!=null?field.GetValue(value):value.GetType().GetProperty(name,Flags).GetValue(value);}
        private static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags).Invoke(value,args);
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
