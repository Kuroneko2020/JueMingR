using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatYoyoFamilyConsumerChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Projectile receiving;private static long first;private static int hitType;private static int playerFailures;
        private static void Damage(Projectile __instance){receiving=__instance;}
        private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(__result>0 && __instance.whoAmI==2 && receiving!=null && first<0){first=Main.GameUpdateCount;hitType=receiving.type;}}
        private static void PlayerAdvance(bool __result,JueMingR.Platform.Combat.PredictionStop __3)
        {if(!__result && playerFailures++==0)Console.WriteLine("YOYO FAMILY player continuation stop="+__3);}
        private static NPC Receiver(int slot,Vector2 center,int width=18)
        {
            var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=40;n.Center=center;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.life=n.lifeMax=100000;n.defense=0;n.friendly=n.immortal=n.dontTakeDamage=false;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.YoyoFamilyConsumers");
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatYoyoFamilyConsumerChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatYoyoFamilyConsumerChecks),nameof(DamageDone)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatYoyoFamilyConsumerChecks),nameof(Strike)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(PlayerMotionContinuation).GetMethod("Advance"),postfix:new HarmonyMethod(typeof(NativeCombatYoyoFamilyConsumerChecks),nameof(PlayerAdvance)));playerFailures=0;
            var random=typeof(Main).GetField("_rngs",Flags);var prior=random.GetValue(null);random.SetValue(null,new System.Collections.Generic.Dictionary<string,Terraria.Utilities.UnifiedRandom>{{"UpdateProjectiles",new Terraria.Utilities.UnifiedRandom(702)}});
            try{Consumers(context);InputContactSeparation(context);}finally{random.SetValue(null,prior);audit.UnpatchAll(audit.Id);receiving=null;}
        }
        private static void InputContactSeparation(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3389,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseJump=p.releaseUp=true;p.channel=p.controlUseItem=p.releaseUseItem=true;p.yoyoGlove=p.magicString=false;p.velocity=Vector2.Zero;
            foreach(var n in Main.npc)n.active=false;var selected=Receiver(2,p.Center+new Vector2(150,0));
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,50,false,true));NativeToolExecutionChecks.Sample(context,input,selected.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,selected,0);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var parent=Main.projectile.Single(q=>q.active && q.type==603);parent.Center=p.Center+new Vector2(50,0);parent.velocity=new Vector2(4,0);parent.ai[0]=parent.localAI[0]=0;
            selected.velocity=new Vector2(1,0);NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,selected.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,selected,1,phaseName:"BeforeNpc");
            var controller=Get(attack,"Control");var entry=((System.Collections.IDictionary)Get(controller,"shots"))[(int)parent.key];var nav=Get(entry,"Navigation");var root=Get(entry,"Obstacles");
            Require((bool)Get(entry,"HasPoint") && (bool)Get(root,"Unchanged"),"direct navigation and full family Contact start from the same valid observed scene");
            float before=selected.position.X;selected.UpdateNPC(selected.whoAmI);Require(selected.position.X==before+1,"the selected receiver advances through its real original NPC action");
            Require(!(bool)Get(root,"Unchanged") && (bool)Call(nav,"Current",p),"normal NPC movement retires the old chase Contact, while the separately proved direct motor input remains valid");
            object[] args={parent,Vector2.Zero};Require((bool)controller.GetType().GetMethod("TryPoint",Flags).Invoke(controller,args) && Get(Get(attack,"consumedInput"),"Kind").ToString()=="Enhanced","the real registered AI cursor consumes the prepared direct input without a new solve or stale Contact");
            p.velocity.X=1;Require(!(bool)Call(nav,"Current",p),"changed owner motion still revokes direct navigation input");p.velocity=Vector2.Zero;
            Console.WriteLine("PASS direct yoyo input / complete family Contact separation across native NPC movement; actual source and owner-motion rejection preserved");
        }
        private static void Consumers(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");int cases=0;
            foreach(int weapon in new[]{3282,3283,3291,3292,5294,3389})
            {
                string only=Environment.GetEnvironmentVariable("JMR_YOYO_FAMILY_WEAPON");if(only!=null && only!=weapon.ToString())continue;
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseJump=p.releaseUp=true;p.channel=p.controlUseItem=p.releaseUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;p.yoyoHitCounter=0;p.velocity=Vector2.Zero;Main.windPhysics=false;Main.tileSolid[1]=true;Main.screenPosition=new Vector2(600,400);
                foreach(var n in Main.npc)n.active=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);
                float range=ProjectileID.Sets.YoyosMaximumRange[p.HeldItem.shoot];var selected=Receiver(2,p.Center+new Vector2(range+24,0));Receiver(3,p.Center+new Vector2(range+350,0),800);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,50,false,true));NativeToolExecutionChecks.Sample(context,input,selected.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,selected,0);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var parent=Main.projectile.Single(q=>q.active && q.type==p.HeldItem.shoot);
                // A real registered native birth is moved to a legal observed
                // held state. The selected receiver lies outside its rope;
                // only a true hit on the other receiver can produce this
                // weapon's useful autonomous child. No child is registered.
                parent.Center=p.Center+new Vector2(range-100,0);parent.velocity=new Vector2(4,0);parent.ai[0]=0;parent.localAI[0]=0;first=-1;hitType=0;int enhanced=0,points=0,contacts=0;long start=Main.GameUpdateCount;
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,selected,0);
                for(int t=0;t<110 && first<0 && parent.active;t++)
                {
                    NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,selected.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");
                    foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
                    NativeCombatAimChecks.PrepareMechanismFixture(host,attack,selected,0);
                    var controller=Get(attack,"Control");var registered=(System.Collections.IDictionary)Get(controller,"shots");var entry=registered[(int)parent.key];if(entry!=null){if((bool)Get(entry,"HasPoint"))points++;if(GetOptional(entry,"Contact")!=null)contacts++;}
                    if(t==0)Console.WriteLine("YOYO FAMILY READY registry="+registered.Count+" entry="+(entry!=null)+" valid="+(entry!=null && (bool)Call(controller,"Valid",entry))+" permission="+Get(attack,"Permission")+" receiver="+Get(attack,"CurrentReceiver")+" failed="+Get(attack,"Failed")+" prepared="+(entry==null?"none":Get(entry,"Prepared").ToString())+" target="+Get(Get(host,"Selection"),"Target")+" nativeCounter="+p.yoyoHitCounter);
                    if(t==0 && entry!=null){var nav=Get(entry,"Navigation");Console.WriteLine("YOYO FAMILY NAV searches="+Get(nav,"Searches")+" retry="+Get(nav,"retryStep")+" path="+string.Join(";",((System.Collections.IEnumerable)Get(nav,"path")).Cast<object>())+" debugStop="+GetOptional(nav,"ReplayStop")+" debugStep="+GetOptional(nav,"ReplayStep"));}
                    NativeCombatPhaseChecks.ProjectilePhase();var consumed=GetOptional(attack,"consumedInput");if(consumed!=null && Get(consumed,"Kind").ToString()=="Enhanced")enhanced++;
                }
                Console.WriteLine("YOYO FAMILY CONSUMER weapon="+weapon+" type="+parent.type+" range="+range+" first="+(first-start)+" hitType="+hitType+" enhanced="+enhanced+" points="+points+" contacts="+contacts+" parent="+parent.Center+" counter="+p.yoyoHitCounter);
                Require(first>=start && hitType!=parent.type && enhanced>0 && contacts>0,"registered real held AI receives via its own OnHit/periodic autonomous family, beyond the parent rope");
                var control=Get(attack,"Control");var entries=(System.Collections.IDictionary)Get(control,"shots");foreach(var q in Main.projectile.Where(q=>q.active && q.type!=parent.type))Require(!entries.Contains((int)q.key),"free finite child does not acquire a cursor source");cases++;
            }
            Console.WriteLine("PASS YOYO-FAMILY actual registered held/other-receiver consumers="+cases+" (component preparation, not ordinary Main cost evidence)");
        }
    }
}
