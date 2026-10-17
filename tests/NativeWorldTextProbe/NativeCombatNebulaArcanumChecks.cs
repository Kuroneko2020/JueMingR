using System;
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
    internal static class NativeCombatNebulaArcanumChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static NPC target;private static int hits;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0)hits++;}
        private static Player Scene(object context)
        {var p=NativeCombatBallisticCandidateChecks.Scene(context,3476,50);foreach(var n in Main.npc)n.active=false;Main.windPhysics=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();return p;}
        private static NPC Target(Vector2 center)
        {var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.width=n.height=40;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.noGravity=n.noTileCollide=true;n.aiStyle=-1;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Path(NPC n,int count=121)
        {var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var s=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,count).Select(t=>new NpcTrajectoryPoint(t,s)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);}
        private static Projectile Copy(Projectile q){return (Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{q});}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,Projectile q,NPC n,int count,out object root)
        {var path=Path(n,count);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);var terrain=Terrain();root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},path,clock,terrain,root});}
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostNebulaArcanumAttack",true);var targets=host.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true);int cases=0,actions=0,emits=0;
            foreach(float speed in new[]{3f,4f,7f})foreach(int clock in new[]{0,29,239,599})
            {
                var p=Scene(context);var n=Target(new Vector2(1200,600));n.active=false;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,450),new Vector2(speed,.1f),617,0,0,p.whoAmI);var actual=Main.projectile[slot];actual.ai[0]=clock;var shadow=Copy(actual);var find=targets.GetMethod("Capture",Flags).Invoke(null,new object[]{actual,1L});var locked=targets.GetMethod("CaptureChase",Flags).Invoke(null,new object[]{actual,1L,true});var path=Path(n);var terrain=Terrain();
                for(int action=0;action<50 && actual.active;action++)
                {
                    int before=Main.projectile.Count(q=>q.active && q.type==618);var args=new object[]{shadow,find,locked,path,Math.Min(120,action+1),terrain,false,false};Require((bool)model.GetMethod("ParentAi",Flags).Invoke(null,args),"AI118 concrete captured prefix");actual.AI();bool born=Main.projectile.Count(q=>q.active && q.type==618)>before;Require((bool)args[6]==born,"AI118 exact30 clock emission, including239 boundary");if(born){emits++;Require(Main.projectile.Where(q=>q.active && q.type==618).All(q=>q.ai[1]==(float)actual.key && q.owner==actual.owner && q.damage==actual.damage/2),"native orbiters keep their actual parent key/owner/damage");}
                    Require((bool)args[7]==!actual.active,"AI118 exact600 native Kill");if(!actual.active)break;Require(actual.ai[0]==shadow.ai[0] && actual.ai[1]==shadow.ai[1] && actual.alpha==shadow.alpha && actual.scale==shadow.scale && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.00001f,"AI118 native fast/slow reset and damping/scale agree speed="+speed+" clock="+clock+" action="+action);actual.position+=actual.velocity;shadow.position+=shadow.velocity;actions++;
                }cases++;
            }
            foreach(bool alive in new[]{false,true})foreach(int clock in new[]{0,419})
            {
                var p=Scene(context);int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,450),Vector2.Zero,617,0,0,p.whoAmI);var parent=Main.projectile[parentSlot];parent.oldPos[1]=parent.position-new Vector2(2,1);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(740,440),new Vector2(-12,1),618,0,0,p.whoAmI,0,(float)parent.key);var actual=Main.projectile[slot];actual.ai[0]=clock;var shadow=Copy(actual);parent.active=alive;model.GetMethod("ChildAi",Flags).Invoke(null,new object[]{shadow,alive?parent:null});actual.AI();Require(actual.active==shadow.active && (!actual.active || actual.ai[0]==shadow.ai[0] && Vector2.DistanceSquared(actual.position,shadow.position)<.00001f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.00001f),"AI119 exact parent oldPos[1] transfer,6 cap and missing-parent/420 boundary alive="+alive+" clock="+clock+" actual="+actual.active+"/"+actual.ai[0]+"/"+actual.position+"/"+actual.velocity+" model="+shadow.active+"/"+shadow.ai[0]+"/"+shadow.position+"/"+shadow.velocity);
            }
            foreach(int liquid in new[]{-1,0,2})
            {
                var p=Scene(context);if(liquid>=0)for(int x=25;x<105;x++)for(int y=10;y<75;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}for(int y=10;y<65;y++)NativeToolsChecks.Tile(50,y,1);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,450),new Vector2(8,-1),620,0,0,p.whoAmI,86);var actual=Main.projectile[slot];var shadow=Copy(actual);var terrain=Terrain();var motor=host.GetType("JueMingR.TerrariaHost.Combat.HostAi29SpellAttack",true);
                for(int action=0;action<45 && actual.active;action++){Require((bool)motor.GetMethod("Step",Flags).Invoke(null,new object[]{shadow,terrain,false}),"AI029620 concrete finite fragment");motor.GetMethod("Finish",Flags).Invoke(null,new object[]{shadow});NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);Require(actual.active==shadow.active && (!actual.active || actual.ai[1]==shadow.ai[1] && actual.timeLeft==shadow.timeLeft && Vector2.DistanceSquared(actual.position,shadow.position)<.001f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f),"AI029620 native40clock/damping/gravity/reflection/liquid");actions++;}cases++;
            }
            foreach(bool inside in new[]{false,true})
            {
                var p=Scene(context);var n=Target(new Vector2(inside?950:1000,450));int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850,450),new Vector2(1,0),617,50,0,p.whoAmI);var parent=Main.projectile[slot];parent.ai[0]=599;Projectile.NewProjectile(parent.GetProjectileSource_FromThis(),new Vector2(700,400),Vector2.Zero,618,0,0,p.whoAmI,0,(float)parent.key);object root;var contact=Known(p,parent,n,2,out root);Require((bool)Get(root,"Unchanged"),"captured orb family premise starts current");parent.oldPos[1]=new Vector2(1,1);Require(!(bool)Get(root,"Unchanged"),"same-GUC parent trail-only mutation revokes orbit lifetime/translation lease");parent.oldPos[1]=Vector2.Zero;NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active)Main.projectile[index].Update(index);
                Require(inside?contact!=null && contact.Tick==1 && n.life<100000:contact==null && n.life==100000,"native176px Kill burst has an exact outer edge negative before later fragments");Require(!Main.projectile.Any(q=>q.active && q.type==618) && Main.projectile.Count(q=>q.active && q.type==620)>=10,"native Kill retires keyed orbiters and births both5..8 fragment groups");
            }
            foreach(int type in new[]{618,620})
            {
                var p=Scene(context);var n=Target(new Vector2(730,450));int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,450),Vector2.Zero,617,0,0,p.whoAmI);var parent=Main.projectile[parentSlot];int slot=Projectile.NewProjectile(parent.GetProjectileSource_FromThis(),new Vector2(760,450),new Vector2(-4,0),type,40,0,p.whoAmI,0,type==618?(float)parent.key:0);var q=Main.projectile[slot];object root;var contact=Known(p,q,n,121,out root);int first=0;for(int tick=1;tick<=40 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active)Main.projectile[index].Update(index);if(n.life<100000){first=tick;break;}}Require(contact!=null && contact.Tick==first && first>0,"observed orbiters and Kill fragments independently receive actual Damage type="+type+" first="+first+" plan="+contact?.Tick);
            }
            var audit=new Harmony("JueMingR.Tests.NebulaArcanumInput");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatNebulaArcanumChecks),nameof(Strike)));
            try
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,3476,50);target=Main.npc[2];hits=0;var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"Nebula orb prepares its real finite family source");typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","Nebula original Shoot consumes the prepared source");for(int tick=1;tick<=120 && hits==0;tick++){NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active)Main.projectile[index].Update(index);}Require(hits>0,"Nebula actual original orb/derived family receives useful Damage");
            }
            finally{audit.UnpatchAll(audit.Id);target=null;}Console.WriteLine("NEBULA-ARCANUM motorCases="+cases+" actions="+actions+" exactEmits="+emits+" orbitBoundaries=4 burstEdges=2 observedDerived=2 trueHand=1");
        }
    }
}
