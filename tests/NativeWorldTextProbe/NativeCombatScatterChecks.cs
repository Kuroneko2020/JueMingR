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
            foreach(var row in new[]{new[]{534,97,4,5},new[]{964,97,3,4},new[]{4703,97,8,8},new[]{3788,97,5,5},new[]{2624,40,5,5},new[]{2624,1334,5,5},new[]{1229,40,2,3}})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,row[0],0,0);p.position=new Vector2(700,646);Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=Vector2.Zero;n.velocity=new Vector2(1,0);n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;Main.rand=new Terraria.Utilities.UnifiedRandom(7123);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,1);
                var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null && plan.Confidence==AttackConfidence.Representative,"multiple native shots share one declared representative plan: "+row[0]);
                int x=Main.mouseX,y=Main.mouseY;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shots=Main.projectile.Where(q=>q.active && q.owner==0).ToArray();Require(shots.Length>=row[2] && shots.Length<=row[3],"native scatter count is preserved");
                Require(shots.All(q=>q.velocity.X>0) && Main.mouseX==x && Main.mouseY==y,"actual native spread points towards chosen future and returns physical cursor");
                if(row[0]==3788)Require(shots.Count(q=>q.type==661)==1,"Onyx extra attack retains its native role");
                if(row[0]==2624)Require(shots.Select(q=>q.Center).Distinct().Count()==5,"parallel arrows retain separate real births");
                int first=0;for(int tick=1;tick<=120;tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);
                    for(int slot=0;slot<Main.maxProjectiles;slot++){var q=Main.projectile[slot];if(!q.active)continue;int life=n.life;int type=q.type;var key=q.key;Main.ProjectileUpdateLoopIndex=slot;try{q.Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}if(n.life<life && first==0){first=tick;Console.WriteLine("SCATTER Damage window: type="+type+" key="+key+" tick="+tick+" old="+life+" new="+n.life);}}
                    if(first>0)break;
                }
                Console.WriteLine("SCATTER actual result: weapon="+row[0]+" seed=7123 births="+shots.Length+" representative="+plan.Tick+" firstDamage="+first+" target="+n.position+" life="+n.life);Require(first>0,"declared representative scatter/parallel has a useful natural Damage result against the same moving target");
            }
            HellwingIchor(context,combat,host,attack,tools,input);
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static void HellwingIchor(object context,object combat,object host,object attack,object tools,object input)
        {
            int useful=0;var savedRandom=Main.rand;
            try{for(int seed=0;seed<16;seed++)
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,3019,0,0);p.position=new Vector2(700,646);Main.screenPosition=new Vector2(600,500);p.inventory[54].SetDefaults(1334);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;n.netOffset=n.velocity=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Main.rand=new Terraria.Utilities.UnifiedRandom(7123+seed);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);
                var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(plan!=null && plan.Confidence==AttackConfidence.Representative,"Hellwing actual Ichor Arrow is an explicit random-direction representative");
                float speed=(float)Get(Get(attack,"ammo"),"Speed");int x=Main.mouseX,y=Main.mouseY;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active && q.owner==0);
                Require(shot.type==278 && Math.Abs(shot.velocity.Length()-speed)<.001f && Main.mouseX==x && Main.mouseY==y,"native random Hellwing birth preserves actual Ichor identity/speed and cursor ownership");
                int first=0;for(int tick=1;tick<=120 && shot.active;tick++){shot.Update(shot.whoAmI);if(n.life<10000){first=tick;useful++;break;}}
                Console.WriteLine("HELLWING ICHOR seed="+(7123+seed)+" firstDamage="+first+" representative="+plan.Tick);
            }}finally{Main.rand=savedRandom;}
            // Random-direction representatives cannot promise every birth.
            // Preserve the real ammo and show natural useful outcomes in a
            // fixed finite distribution, retaining misses in the denominator.
            Require(useful>0,"actual Ichor Hellwing distribution has natural damage outcomes");Console.WriteLine("PASS Hellwing Ichor natural useful births="+useful+"/16; confidence=Representative");
        }
    }
}
