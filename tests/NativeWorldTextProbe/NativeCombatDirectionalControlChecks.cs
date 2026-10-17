using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatDirectionalControlChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.DirectionalControl");
            audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleSpecialEvent",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try{RunCore(context);}finally{audit.UnpatchAll(audit.Id);}
        }
        private static void RunCore(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var control=Get(attack,"Control");var input=Get(context,"Input");
            foreach(int weapon in new[]{3368,4144,3835,3858,6155})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.ResetEffects();p.position=new Vector2(1000,646);p.channel=p.controlUseItem=true;p.releaseUseItem=true;
                for(int x=50;x<115;x++)for(int y=20;y<60;y++)Main.tile[x,y].ClearEverything();
                Main.tileSolid[1]=true;
                for(int x=50;x<115;x++)NativeToolsChecks.Tile(x,43,1);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1300,648);n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.velocity=Vector2.Zero;n.immortal=n.dontTakeDamage=n.friendly=false;Array.Clear(n.immune,0,n.immune.Length);
                var physical=new Vector2(750,660);
                NativeToolExecutionChecks.Sample(context,input,physical,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});
                int type=p.HeldItem.shoot;var shot=Main.projectile.Single(q=>q.active && q.type==type);
                var entries=(IDictionary)Get(control,"shots");Require(entries.Contains((int)shot.key),"real native Shoot/birth registers the later directional consumer: "+type);
                if(type==1117){shot.Center=p.Center+new Vector2(80,0);shot.ai[0]=-1;shot.velocity=Vector2.Zero;}
                else{shot.ai[0]=24;shot.velocity=new Vector2(-1,0);p.direction=-1;}
                NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                int mouseX=Main.mouseX,mouseY=Main.mouseY;shot.AI();
                Require(shot.velocity.X>0,"actual native later input faces/travels toward the shared right target while physical mouse stays left: "+type);
                Require(Main.mouseX==mouseX && Main.mouseY==mouseY,"native AI cursor borrowing restores physical coordinates: "+type);
                if(type==697 || type==707)shot.ai[0]=24;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,false));
                Require(shot.active && entries.Contains((int)shot.key) && !(bool)Call(control,"TryPoint",new object[]{shot,Vector2.Zero}),"Aim OFF/path-only revokes the live actual source's input before native release or death");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);
                Require((bool)Call(control,"TryPoint",new object[]{shot,Vector2.Zero}),"Aim ON restores a still-live real native consumer after a fresh preparation");
                if(type==595 || type==735 || type==697 || type==707)
                {
                    n.Center=p.MountedCenter+new Vector2(34,0);n.noTileCollide=true;NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);
                    var entry=entries[(int)shot.key];Console.WriteLine("DIRECTIONAL BLADE type="+type+" body="+shot.Center+" speed="+p.HeldItem.shootSpeed+" target="+n.Center+" hasPoint="+Get(entry,"HasPoint")+" prepared="+Get(entry,"Prepared")+" contact="+(GetOptional(entry,"Contact")!=null));Require(GetOptional(entry,"Contact")!=null,"anchored directional blade prepares a real native body contact independently of HasPoint");
                    var lease=Get(entry,"Obstacles");float scale=shot.scale,rotation=shot.rotation;
                    if(type==595 || type==735)shot.scale+=.1f;else shot.rotation+=.1f;
                    Require(!(bool)Get(lease,"Unchanged"),"a same-GUC scale/rotation change revokes the actual motion/spawn read set without moving the body");shot.scale=scale;shot.rotation=rotation;
                    int life=n.life;shot.Update(shot.whoAmI);Require(n.life<life,"actual original controlled blade Update/Damage consumes the prepared direction and hits the same nearby target");
                    if(type==697)
                    {
                        Array.Clear(n.immune,0,n.immune.Length);shot.ai[0]=46;shot.rotation=0;shot.velocity=Vector2.UnitX;
                        var model=assemblyType(attack,"HostDirectionalStaffAttack");var shadow=(Projectile)assemblyType(attack,"HostThornAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{shot});
                        var expected=(Projectile)model.GetMethod("Ai",Flags).Invoke(null,new object[]{shadow,p,Vector2.Zero});Require(expected!=null,"the exact late staff clock crosses its native698 birth");
                        n.Center=expected.Center+new Vector2(110,0);Require(Vector2.Distance(n.Hitbox.ClosestPointInRect(p.MountedCenter),p.MountedCenter)>73,"child-only receive lies outside the parent's radial collision");
                        NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);Require(GetOptional(entries[(int)shot.key],"Contact")!=null,"registered parent predicts its actual later698 impact without a fake new hand");
                        shot.Update(shot.whoAmI);var child=Main.projectile.Single(q=>q.active && q.type==698);Require(entries.Contains((int)child.key) && Vector2.Distance(child.Center,expected.Center)<.001f,"real original AI birth retains parent Source and exact child position");
                        life=n.life;child.Update(child.whoAmI);Require(n.life<life,"original698 expanded collider delivers the predicted child-only Damage");
                    }
                }
                if(type==1117)
                {
                    shot.ai[0]=n.whoAmI;object[] pointArgs={shot,Vector2.Zero};
                    Require(!(bool)Call(control,"TryPoint",pointArgs),"a naturally locked storm bolt is autonomous and gets no later cursor lease");
                    shot.ai[0]=-1;shot.Center=n.Center;pointArgs[1]=Vector2.Zero;
                    Require(!(bool)Call(control,"TryPoint",pointArgs),"native nearby acquisition wins before the free cursor branch");
                    shot.AI();Require(shot.ai[0]==n.whoAmI,"original AI203 acquires the target without an assist cursor");
                    var unrelated=Main.projectile[20];unrelated.SetDefaults(1117);unrelated.whoAmI=20;unrelated.owner=p.whoAmI;unrelated.active=true;unrelated.ai[0]=-1;unrelated.Center=p.Center;
                    Require(!(bool)Call(control,"TryPoint",new object[]{unrelated,Vector2.Zero}),"a matching unobserved projectile cannot borrow another birth's source");
                    unrelated.active=false;shot.ai[0]=n.whoAmI;shot.ai[1]=29;shot.Center=n.Center;shot.velocity=Vector2.Zero;Array.Clear(n.immune,0,n.immune.Length);
                    NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0);Require(GetOptional(entries[(int)shot.key],"Contact")!=null,"naturally locked storm prepares its actual seeded Kill damage without claiming continued cursor control");
                    int life=n.life;shot.Update(shot.whoAmI);Require(!shot.active && n.life<life,"original storm's thirty-action Kill delivers the prepared native Damage");
                }
                else
                {
                    shot.ai[0]=24;p.controlUseItem=p.channel=false;
                    Require(!(bool)Call(control,"TryPoint",new object[]{shot,Vector2.Zero}),"released staff midpoint does not mint a new directional consumer");
                    shot.AI();Require(!shot.active,"original staff midpoint release still terminates naturally");
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,false));
                Require(!(bool)Call(control,"TryPoint",new object[]{shot,Vector2.Zero}),"Aim OFF revokes only assist input, including a retained source");
                Console.WriteLine("PASS DIRECTIONAL CONTROL weapon="+weapon+" source="+type+" actual-Shoot/AI cursor-opposed, release/lock and OFF ownership");
            }
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
        }
        private static Type assemblyType(object attack,string name){return attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat."+name,true);}
    }
}
