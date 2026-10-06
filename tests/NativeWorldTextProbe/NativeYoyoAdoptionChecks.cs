using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeYoyoAdoptionChecks
    {
        internal static void Run(object context,object combat,object tools,object input)
        {
            var use=Get(combat,"Use");int cases=0;
            var configuration=(System.Reflection.AssemblyConfigurationAttribute)Attribute.GetCustomAttribute(use.GetType().Assembly,typeof(System.Reflection.AssemblyConfigurationAttribute));Require(configuration!=null && (configuration.Configuration=="Debug" || configuration.Configuration=="Release"),"Actual candidate declares Debug or Release configuration.");bool releaseBuild=configuration.Configuration=="Release";
            Require(!releaseBuild || GetOptional(use,"ProjectileDiscoveryReads")==null,"Ordinary Release has no DEBUG discovery counter.");
            if(releaseBuild)Console.WriteLine("RELEASE ADOPTION: functional native takeover/cadence/identity checks execute; discovery workload observation unavailable, authenticated Debug evidence remains separate.");
            foreach(int empty in new[]{0,1,3})
            {
                int[] original=null;
                foreach(bool managed in new[]{false,true})
                {
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,tools,input,3262,0,0);
                    foreach(var item in p.armor)item.TurnToAir();
                    NativeCombatCadenceChecks.Step(context,false,false,0);
                    Require(!p.magicString,"first equipment effect is genuinely absent before native update");
                    p.armor[3].SetDefaults(ItemID.MagicString);
                    NativeCombatCadenceChecks.Save(combat,new CombatOptions(managed?16:0));
                    var known=new HashSet<int>();var births=new List<int>();int split=0,release=0;
                    int? scans=releaseBuild?(int?)null:(int)Get(use,"ProjectileDiscoveryReads"),adoptedReads=null;Projectile first=null;
                    for(int t=0;t<40;t++)
                    {
                        bool live=Main.projectile.Any(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot && q.ai[0]!=-2);
                        bool ready=p.itemAnimation<=0 && p.itemTime<=0 && p.reuseDelay<=0 && !p.delayUseItem;
                        NativeCombatCadenceChecks.Step(context,managed || !live && ready || t<release,false,empty);
                        foreach(var q in Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && q.aiStyle==99))
                            if(known.Add((int)q.key)){if(q.ai[0]==-2)split++;else{births.Add(t);release=t+1;}}
                        if(t==0)
                        {
                            first=Main.projectile.First(q=>q.active && q.owner==p.whoAmI && q.type==p.HeldItem.shoot && q.ai[0]>=0);
                            Require(!(bool)Get(use,"Active") && p.ownedProjectileCounts[p.HeldItem.shoot]==0,"native birth precedes equipment qualification and count refresh");
                        }
                        if(t==1)
                        {
                            Require(first.ai[0]==-3 && split==1 && (!managed || !(bool)Get(use,"press")),"takeover releases on the first post-birth native action without an extra hold");
                            if(!releaseBuild)adoptedReads=(int)Get(use,"ProjectileDiscoveryReads");
                        }
                    }
                    Require(births.Count>=3 && split>=3,"first-effect native/managed chain keeps repeating");
                    Console.WriteLine("YOYO ADOPTION empty="+empty+" managed="+managed+" births="+string.Join(",",births)+" splits="+split+" discovery="+(releaseBuild?"NA_RELEASE":((int)Get(use,"ProjectileDiscoveryReads")-scans).ToString()));
                    if(!managed)original=births.ToArray();
                    else
                    {
                        Require(births.SequenceEqual(original),"takeover preserves the actual native emission cadence");
                        if(!releaseBuild)
                        {int reads=(int)Get(use,"ProjectileDiscoveryReads")-scans.Value;Require(reads>0 && reads<=Main.maxProjectiles && (int)Get(use,"ProjectileDiscoveryReads")==adoptedReads,"one bounded takeover scan; every later native action adds zero discovery reads");}
                    }
                    cases++;
                }
            }
            // State-boundary consumer checks: no native future is claimed here.
            foreach(int boundary in new[]{-2,-3,1})
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var p=NativeToolExecutionChecks.Reset(context,tools,input,3262,0,0);
                foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(ItemID.MagicString);
                NativeCombatCadenceChecks.Step(context,false,false,0);
                int slot=Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_ItemUse(p,p.HeldItem),p.Center,Vector2.Zero,p.HeldItem.shoot,0,0,boundary==1?(p.whoAmI+1)%Main.maxPlayers:p.whoAmI);
                Main.projectile[slot].ai[0]=boundary==1?0:boundary;
                NativeCombatCadenceChecks.Save(combat,new CombatOptions(16));
                NativeToolExecutionChecks.Sample(context,input,p.Center+new Vector2(180,0),true);Call(combat,"Sample");Call(use,"Sync",p);
                Require((bool)Get(use,"Active") && GetOptional(use,"primary")==null,"split, returning original and foreign owner cannot become a held takeover original: "+boundary);
                Call(use,"FinishFrame");cases++;
            }
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            Console.WriteLine("PASS YOYO ADOPTION native first-effect takeover empty0/1/3 and three identity/stage boundaries cases="+cases);
        }
    }
}
