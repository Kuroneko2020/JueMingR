using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatSkyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");var tools=Get(context,"Tools");
            foreach(int type in new[]{3029,4381,2750,65,3065,3570})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,tools,input,type,0,0);p.position=new Vector2(700,900);p.velocity=Vector2.Zero;p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;
                Main.screenPosition=new Vector2(600,800);var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,1000);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
                // Tool Reset installs a full solid floor at tile Y43. This
                // positive scene is open sky: an arrow cannot reach the target
                // below that ceiling. Terrain negatives retain their own wall.
                for(int tx=5;tx<115;tx++)Main.tile[tx,43].ClearEverything();
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,850),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,1);
                var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(contact!=null && contact.Confidence==AttackConfidence.Representative,"sky consumer prepares its representative birth/flight: "+type);
                Require(contact.AimX>=1100 && contact.AimX<1300 && Math.Abs(contact.AimY-1020)<25,"sky AimInput is future cursor point, not a long muzzle direction");
                Main.rand=new Terraria.Utilities.UnifiedRandom(7123);int x=Main.mouseX,y=Main.mouseY;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shots=Main.projectile.Where(q=>q.active && q.owner==0).ToArray();Require(shots.Length>=1 && shots.Length<=4,"native sky quantity preserved");
                Require(shots.All(q=>q.Center.Y<=p.MountedCenter.Y-590 && q.velocity.Y>0),"native sky birth and downward flight use real input branch");
                if(type==4381)Require(shots.All(q=>q.type==819 && q.tileCollide),"blood arrow conversion retains immediate terrain collision");
                if(type==3570)Require(shots.All(q=>Math.Abs(q.ai[1]-contact.AimY)<1.01f),"lunar cursorY is preserved as native terrain threshold");
                if(type==3065)Require(shots.All(q=>Math.Abs(q.ai[1]-(p.Center.Y-200))<.01f),"star wrath clamps native terrain threshold to player height");
                Require(Main.mouseX==x && Main.mouseY==y,"sky cursor returns to physical intent");Console.WriteLine("PASS native sky input/birth: weapon="+type+" representativeTick="+contact.Tick+" cursor="+contact.AimX+","+contact.AimY+" births="+shots.Length);
                int firstDamage=0;
                for(int tick=1;tick<=120;tick++)
                {NativeQuickItemChecks.BeginWorldStep();n.position.X=1100+tick;if(n.immune[0]>0)n.immune[0]--;foreach(var q in Main.projectile.Where(q=>q.active && q.owner==0).ToArray())q.Update(q.whoAmI);if(n.life<10000){firstDamage=tick;break;}}
                Console.WriteLine("NATIVE-SKY-RESULT weapon="+type+" seeded firstDamage="+firstDamage+" meanPlan="+contact.Tick);
                // Random birth/spread is intentionally not equated to the
                // representative point. This fixed seed tests useful actual
                // native damage, with the prescribed shared moving timeline.
                if(type==3029 || type==3570)Require(firstDamage>0,"representative sky input reaches useful native damage in seeded scene: "+type);
                Array.Clear(n.immune,0,n.immune.Length);
                if(type==4381 || type==3570)
                {
                    foreach(var q in Main.projectile)q.active=false;n.position=new Vector2(1100,1000);n.life=10000;
                    for(int tx=5;tx<115;tx++)NativeToolsChecks.Tile(tx,43,1);
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(1100,1020),true);Call(combat,"Sample");NativeCombatAimChecks.Prepare(host,attack,n,1);
                    var roofPlan=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(type==4381?roofPlan==null:roofPlan!=null,"same roof blocks blood arrow but lunar native pre-threshold phase passes");
                    Main.rand=new Terraria.Utilities.UnifiedRandom(7123);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});int damage=0;
                    for(int tick=1;tick<=120;tick++){NativeQuickItemChecks.BeginWorldStep();n.position.X=1100+tick;if(n.immune[0]>0)n.immune[0]--;foreach(var q in Main.projectile.Where(q=>q.active && q.owner==0).ToArray())q.Update(q.whoAmI);if(n.life<10000){damage=tick;break;}}
                    Require(type==4381?damage==0:damage>0,"real same-roof Damage differs according to native terrain phase: "+type);
                    Array.Clear(n.immune,0,n.immune.Length);Console.WriteLine("PASS sky same roof -> real Damage: weapon="+type+" firstDamage="+damage+" plan="+(roofPlan!=null));
                }
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
    }
}
