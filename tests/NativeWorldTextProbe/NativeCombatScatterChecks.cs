using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatScatterChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var tools=Get(context,"Tools");var input=Get(context,"Input");
            foreach(var row in new[]{new[]{534,97,4,5},new[]{964,97,3,4},new[]{4703,97,8,8},new[]{3788,97,5,5},new[]{2624,40,5,5},new[]{1229,40,2,3}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,row[0],0,0);p.position=new Vector2(700,646);Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,1);
                var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null && plan.Confidence==AttackConfidence.Representative,"multiple native shots share one declared representative plan: "+row[0]);
                int x=Main.mouseX,y=Main.mouseY;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shots=Main.projectile.Where(q=>q.active && q.owner==0).ToArray();Require(shots.Length>=row[2] && shots.Length<=row[3],"native scatter count is preserved");
                Require(shots.All(q=>q.velocity.X>0) && Main.mouseX==x && Main.mouseY==y,"actual native spread points towards chosen future and returns physical cursor");
                if(row[0]==3788)Require(shots.Count(q=>q.type==661)==1,"Onyx extra attack retains its native role");
                if(row[0]==2624)Require(shots.Select(q=>q.Center).Distinct().Count()==5,"parallel arrows retain separate real births");
                Console.WriteLine("PASS native representative scatter/parallel: weapon="+row[0]+" births="+shots.Length+" planTick="+plan.Tick);
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
    }
}
