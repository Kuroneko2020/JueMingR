using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatResourceChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static object attack;private static bool watching,hadContact;
        private static void BeforeItem(Player __instance){if(watching && __instance.whoAmI==0)hadContact|=GetOptional(attack,"ExpectedImpact")!=null;}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");attack=Get(combat,"Attack");var input=Get(context,"Input");var audit=new Harmony("JueMingR.Tests.AttackResource");
            audit.Patch(typeof(Player).GetMethod("ItemCheck",Flags),prefix:new HarmonyMethod(typeof(NativeCombatResourceChecks).GetMethod("BeforeItem",Flags)){priority=Priority.Last});
            bool manaV2=Terraria.Testing.DebugOptions.ManaV2;
            try
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3570,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);p.statMana=0;p.statManaMax=p.statManaMax2=200;p.slowMagicUse=false;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1040,646);n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));p.AddBuff(35,60);Step(context,p,n,false);hadContact=false;watching=true;Step(context,p,n,true);watching=false;
                Console.WriteLine("RESOURCE silence="+p.silence+" born="+Active()+" planBeforeNative="+hadContact+" ManaV2="+Terraria.Testing.DebugOptions.ManaV2);
                Require(p.silence && Active()==0 && !hadContact,"natural silence rejects initial magic and no unusable Contact is advertised before native ItemCheck");
                Array.Clear(p.buffType,0,p.buffType.Length);Array.Clear(p.buffTime,0,p.buffTime.Length);p.statMana=0;p.slowMagicUse=false;Step(context,p,n,false);Step(context,p,n,true);
                Console.WriteLine("RESOURCE recover zeroMana born="+Active()+" slow="+p.slowMagicUse+" animationMax="+p.itemAnimationMax+" useAnimation="+p.HeldItem.useAnimation);
                Require(Active()>0 && p.slowMagicUse && p.itemAnimationMax>p.HeldItem.useAnimation,"actual ManaV2 zero-mana initial use remains legal and slow; silence removal plus fresh input restores birth");
                FeeBoundaries(context,combat);
            }
            finally{watching=false;attack=null;Terraria.Testing.DebugOptions.ManaV2=manaV2;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static int Active(){int count=0;foreach(var shot in Main.projectile)if(shot.active && shot.owner==0)count++;return count;}
        private static void FeeBoundaries(object context,object combat)
        {
            var clock=Get(combat,"Attack").GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackResources+FeeClock",true);
            foreach(int item in new[]{3541,2882})foreach(bool mode in new[]{true,false})
            {
                Terraria.Testing.DebugOptions.ManaV2=mode;var p=Main.LocalPlayer;p.inventory[0].SetDefaults(item);p.selectedItemState.Select(0);p.selectedItemState.Update();p.channel=true;p.noItems=false;p.statMana=0;p.slowMagicUse=false;p.manaFlower=false;
                foreach(var shot in Main.projectile)shot.active=false;
                int slot=Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_ItemUse(p,p.HeldItem),p.MountedCenter,Vector2.UnitX,p.HeldItem.shoot,10,0,p.whoAmI);var parent=Main.projectile[slot];parent.ai[0]=item==3541?29:79;parent.ai[1]=4;
                var privateClock=Activator.CreateInstance(clock,Flags,null,new object[]{p,parent},null);var advance=clock.GetMethod("Advance",Flags);object[] args={0f,false};bool allowed=(bool)advance.Invoke(privateClock,args);float expected=(float)args[0];parent.AI();
                Console.WriteLine("RESOURCE parent="+parent.type+" ManaV2="+mode+" expectedCharge="+expected+" actual="+parent.ai[0]+" allowed="+allowed+" active="+parent.active+" slow="+p.slowMagicUse);
                Require(allowed==mode && parent.active==mode && parent.ai[0]==expected,"real fee beat follows current ManaV2: default survives at zero, legacy mode rejects this beat");
                if(mode)
                {
                    Require(p.slowMagicUse,"native unpaid fee marks slow only after the current increment");args=new object[]{0f,false};Require((bool)advance.Invoke(privateClock,args),"private next fee prefix survives");expected=(float)args[0];parent.AI();Require(Math.Abs(parent.ai[0]-expected)<.0001f && Math.Abs(parent.ai[0]-(item==3541?31:80.4f))<.0001f,"next prism remains +1 while charged cannon uses newly slow .4");
                }
            }
        }
        private static void Step(object context,Player p,NPC n,bool left)
        {var input=Get(context,"Input");NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),left);Call(Get(context,"Combat"),"Sample");Call(Get(context,"CombatObservation"),"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);n.UpdateNPC(n.whoAmI);Call(context,"UpdateRuntime");}
    }
}
