using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMagicStringAimChecks
    {
        private static readonly List<Vector2> parents=new List<Vector2>(),children=new List<Vector2>();
        private static readonly List<int> emissions=new List<int>();private static uint origin;
        private static Projectile receiver;private static int detachedHits;
        private static void DamageBefore(Projectile __instance){receiver=__instance;}
        private static void DamageAfter(){receiver=null;}
        private static void Strike(NPC __instance,int __result){if(__instance.whoAmI==2 && __result>0 && receiver!=null && receiver.aiStyle==99 && receiver.ai[0]==-2)detachedHits++;}
        private static void Born(Player __instance,Projectile __0)
        {
            if(__instance.whoAmI!=0 || __0.aiStyle!=99 || __0.counterweight)return;
            if(__0.ai[0]==-2)children.Add(__0.velocity);
            else {parents.Add(__0.velocity);emissions.Add((int)(Main.GameUpdateCount-origin));}
        }
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");
            var audit=new Harmony("JueMingR.Tests.MagicStringAim");
            audit.Patch(AccessTools.Method(typeof(Player),"TryUpdateChannel"),postfix:new HarmonyMethod(typeof(NativeCombatMagicStringAimChecks),nameof(Born)));
            audit.Patch(AccessTools.Method(typeof(Projectile),"Damage"),prefix:new HarmonyMethod(typeof(NativeCombatMagicStringAimChecks),nameof(DamageBefore)),postfix:new HarmonyMethod(typeof(NativeCombatMagicStringAimChecks),nameof(DamageAfter)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatMagicStringAimChecks),nameof(Strike)));
            try
            {
                FreePreview(context,combat,host,input);
                foreach(bool moving in new[]{false,true})foreach(int mirror in new[]{1,-1})
                {
                    int[] manual=null;
                    foreach(bool aim in new[]{false,true})
                    {
                        NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                        var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3262,0,0);p.position=new Vector2(1000,646);p.releaseUseItem=true;
                        foreach(var armor in p.armor)armor.TurnToAir();p.armor[3].SetDefaults(ItemID.MagicString);
                        var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=p.Center+new Vector2(mirror*(moving?240:300),-100);n.velocity=new Vector2(moving?mirror:0,0);n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;
                        n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Main.screenPosition=p.Center-new Vector2(400,300);
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,aim));NativeCombatCadenceChecks.Save(combat,new CombatOptions(16));
                        parents.Clear();children.Clear();emissions.Clear();detachedHits=0;origin=Main.GameUpdateCount;
                        var cursor=p.Center+new Vector2((aim?-mirror:mirror)*300,-100);
                        for(int t=0;t<120;t++)
                        {
                            NativeCombatSelectionPreviewChecks.Step(context,cursor,true);

                        }
                        Console.WriteLine("MAGIC STRING mirror="+mirror+" moving="+moving+" aim="+aim+" parents="+parents.Count+" split="+children.Count+" emissions="+string.Join(",",emissions)+" first="+(children.Count>0?children[0].ToString():"none")+" damage="+(n.lifeMax-n.life)+" detachedReceivers="+detachedHits);
                        if(moving)Require((n.position.X-p.Center.X)*mirror>330,"declared moving receiver advances through actual NPC.UpdateNPC, not a held velocity fixture");
                        if(aim)Console.WriteLine("MAGIC STRING END receiver="+n.Center+" velocity="+n.velocity+" immune="+n.immune[0]+" aimFailed="+Get(Get(combat,"Attack"),"Failed")+" splits="+string.Join(";",children.Select(v=>v.ToString())));
                        Require(parents.Count>=10 && children.Count==parents.Count,"native parent/split emissions retain repeated cadence");
                        Require(children.All(v=>v.X*mirror>0),"actual split inherits target-bearing parent velocity despite opposite physical mouse");
                        if(!aim)manual=emissions.ToArray();else
                        {
                            Require(emissions.SequenceEqual(manual),"Aim cannot delay any magic-string native emission opportunity");
                            Require(children.Any(v=>v.Y<0) && detachedHits>0 && n.life<n.lifeMax,"detached native ball flies upward and actually damages separated target");
                        }
                        var detached=Main.projectile.FirstOrDefault(s=>s.active && s.aiStyle==99 && s.ai[0]==-2);
                        if(detached!=null){object[] args={detached,Vector2.Zero};Require(!(bool)Call(Get(Get(combat,"Attack"),"Control"),"TryPoint",args),"detached AI99 cannot acquire ongoing navigation input");}
                        for(int t=0;t<3;t++)NativeCombatSelectionPreviewChecks.Step(context,cursor,false);
                        Require(!(bool)Get(Get(combat,"Use"),"Active"),"physical release revokes managed source");
                    }
                }
                foreach(int mirror in new[]{1,-1})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3262,0,0);p.position=new Vector2(1000,646);p.releaseUseItem=true;
                    foreach(var armor in p.armor)armor.TurnToAir();p.armor[3].SetDefaults(ItemID.MagicString);
                    foreach(var n in Main.npc)n.active=false;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,false,25,false,true));NativeCombatCadenceChecks.Save(combat,new CombatOptions(16));
                    parents.Clear();children.Clear();emissions.Clear();origin=Main.GameUpdateCount;
                    var physical=p.Center+new Vector2(mirror*170,-20);
                    for(int t=0;t<16;t++)NativeCombatSelectionPreviewChecks.Step(context,physical,true);
                    Require(parents.Count==4 && children.Count==4 && children.All(v=>v.X*mirror>0),"Aim with no selected NPC retains physical native split direction and cadence");
                }
                // Reuse the existing real glove/counterweight and cancellation
                // owners while Aim is enabled. Their Player/Projectile-only
                // scope remains explicit; it is not the natural NPC matrix.
                if(Environment.GetEnvironmentVariable("JMR_MAGIC_STRING_BOUNDARIES")=="1")
                {
                    string output=Environment.GetEnvironmentVariable("JUEMINGR_MAGIC_STRING_EVIDENCE");
                    System.IO.Directory.CreateDirectory(output);NativeYoyoBoundaryHitChecks.Run(context,output);
                }
                Console.WriteLine("PASS MagicString+Aim actual mirrored split direction, detached damage, identical emission cadence and cursor/source retirement");
            }
            finally{audit.UnpatchAll(audit.Id);NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static void FreePreview(object context,object combat,object host,object input)
        {
            foreach(int mirror in new[]{1,-1})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3262,0,0);p.position=new Vector2(1000,646);p.releaseUseItem=true;
                foreach(var armor in p.armor)armor.TurnToAir();p.armor[3].SetDefaults(ItemID.MagicString);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=p.Center+new Vector2(mirror*500,-100);n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;n.knockBackResist=0;
                Main.screenPosition=p.Center-new Vector2(400,300);Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,false,50,true,true));NativeCombatCadenceChecks.Save(combat,new CombatOptions(16));
                var control=Get(Get(combat,"Attack"),"Control");NpcTrajectory timeline=null;
                for(int t=0;t<24;t++)
                {NativeCombatSelectionPreviewChecks.Step(context,p.Center+new Vector2(-mirror*300,-100),true);timeline=GetOptional(control,"preparedTimeline") as NpcTrajectory;if(timeline!=null && timeline.Count>=80)break;}
                Require(timeline!=null && timeline.Count>=40,"normal observation supplies a finite free-flight receiver window");
                // Observe a private next legal opening after retiring the
                // current native pool; no fabricated trajectory or Contact.
                foreach(var s in Main.projectile)s.active=false;
                var phase=Enum.Parse(control.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase"),"CompletedWorld");
                var contact=Call(control,"Preview",timeline,phase,0,true) as AttackContact;
                Console.WriteLine("MAGIC STRING FREE PREVIEW mirror="+mirror+" range="+ProjectileID.Sets.YoyosMaximumRange[p.HeldItem.shoot]+" receiverDistance="+Vector2.Distance(p.Center,n.Center)+" count="+timeline.Count+" contact="+(contact==null?"none":contact.Tick+"/"+contact.Confidence));
                Require(contact!=null && contact.Confidence==AttackConfidence.Representative,"clear-line opening proves dry detached reach outside held rope range");
                var lease=Call(control,"CapturePreviewDependencies",contact);Require((bool)Call(lease,"Current",p),"free opening owns current input dependencies");
                p.magicString=false;Require(!(bool)Call(lease,"Current",p),"removing magic string revokes free-flight preview lease");p.magicString=true;
                int wall=(int)((p.Center.X+n.Center.X)/32);for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(wall,y,1);
                Require(!(bool)Call(lease,"Current",p),"new terrain retires previously proved free flight");
                Require(Call(control,"Preview",timeline,phase,0,true)==null,"free-flight wall cannot borrow ordinary held-navigation contact");
                for(int y=0;y<Main.maxTilesY;y++)Main.tile[wall,y].ClearEverything();
                foreach(bool aim in new[]{false,true})
                {
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());foreach(var s in Main.projectile)s.active=false;
                    p.channel=false;p.releaseUseItem=true;p.itemTime=p.itemAnimation=p.reuseDelay=0;Array.Clear(n.immune,0,n.immune.Length);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,false,50,true,aim));NativeCombatCadenceChecks.Save(combat,new CombatOptions(16));
                    parents.Clear();children.Clear();emissions.Clear();detachedHits=0;origin=Main.GameUpdateCount;
                    var cursor=aim?p.Center+new Vector2(-mirror*300,-100):new Vector2(contact.AimX,contact.AimY);
                    for(int t=0;t<120;t++)NativeCombatSelectionPreviewChecks.Step(context,cursor,true);
                    Console.WriteLine("MAGIC STRING FREE ACTUAL mirror="+mirror+" aim="+aim+" parents="+parents.Count+" split="+children.Count+" detachedReceivers="+detachedHits+" distance="+Vector2.Distance(p.Center,n.Center));
                    Require(parents.Count==30 && children.Count==30 && detachedHits>0,"native manual prepared angle and enhanced clear-line opening both hit beyond held rope without cadence loss");
                }
            }
        }
    }
}
