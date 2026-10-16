using System;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatYoyoFamilyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            if(Environment.GetEnvironmentVariable("JMR_YOYO_FAMILY_CONSUMERS_ONLY")=="1"){NativeCombatYoyoFamilyConsumerChecks.Run(context);return;}
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
            var assembly=Get(context,"Combat").GetType().Assembly;var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostYoyoFamilyAttack",true);
            var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var copy=assembly.GetType("JueMingR.TerrariaHost.Combat.HostRaySpellAttack",true).GetMethod("Copy",Flags);
            int cases=0,actions=0;
            foreach(int type in new[]{1125,1134,1135,1130,604})foreach(Vector2 velocity in new[]{new Vector2(1.1f,0),new Vector2(-4,2),new Vector2(15,-9)})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3389,0,0);Main.windPhysics=false;
                foreach(var npc in Main.npc)npc.active=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,500),velocity,type,0,0,p.whoAmI,0,.65f);var native=Main.projectile[slot];
                var expected=(Projectile)copy.Invoke(null,new object[]{native});var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                for(int action=1;action<=60 && native.active;action++)
                {
                    for(int sub=0;sub<=expected.extraUpdates && expected.active;sub++)
                    {Require((bool)model.GetMethod("ChildStep",Flags).Invoke(null,new object[]{expected,terrain}),"yoyo child finite motor acquired terrain");if(expected.active && --expected.timeLeft<=0)expected.active=false;}
                    NativeQuickItemChecks.BeginWorldStep();native.Update(slot);actions++;
                    Require(native.active==expected.active && (!native.active || native.position==expected.position && native.velocity==expected.velocity && native.ai[0]==expected.ai[0] && native.ai[1]==expected.ai[1] && native.localAI[0]==expected.localAI[0] && native.timeLeft==expected.timeLeft && native.scale==expected.scale),"original yoyo child motor/clock exact type="+type+" action="+action+" native="+native.position+"/"+native.velocity+" model="+expected.position+"/"+expected.velocity+" fields="+native.ai[0].ToString("R")+"/"+expected.ai[0].ToString("R")+","+native.ai[1].ToString("R")+"/"+expected.ai[1].ToString("R")+","+native.localAI[0].ToString("R")+"/"+expected.localAI[0].ToString("R")+","+native.scale.ToString("R")+"/"+expected.scale.ToString("R")+",left="+native.timeLeft+"/"+expected.timeLeft+",posdiff="+(native.position-expected.position)+",veldiff="+(native.velocity-expected.velocity));
                    if(native.active)
                    {
                        // Native line1125 and rectangle1130/1135 are actual
                        // receiving shapes; a large displayed sprite is not
                        // accepted as its damage geometry by this assertion.
                        foreach(var at in new[]{native.Center,native.Center+native.velocity.SafeNormalize(Vector2.UnitX)*80,native.Center+new Vector2(0,100)})
                        {var box=new Rectangle((int)at.X-4,(int)at.Y-4,8,8);var b=new JueMingR.Platform.Combat.MotionRect(box.X,box.Y,box.Width,box.Height);bool actual=native.Colliding(native.Hitbox,box);bool projected=(bool)model.GetMethod("Collision",Flags).Invoke(null,new object[]{expected,b});Require(actual==projected,"actual finite yoyo receiving collider");}
                    }
                }
                cases++;
            }
            Births(context,assembly,model,terrainType);
            Periodic(context,assembly,model,terrainType);
            DetachedMotors(context,assembly,model,terrainType,copy);
            NativeCombatYoyoFamilyConsumerChecks.Run(context);
            Console.WriteLine("PASS YOYO-FAMILY native child motors="+cases+" actions="+actions+" (full navigation consumers pending separate checks)");
        }
        private static NpcTrajectory Timeline(Assembly assembly,NPC npc)
        {
            var identity=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{npc,1L});
            var state=new NpcMotionState{Identity=identity,X=npc.position.X,Y=npc.position.Y,Width=npc.width,Height=npc.height,CanReceive=true,NoTileCollide=true};
            var points=Enumerable.Range(0,121).Select(i=>new NpcTrajectoryPoint(i,state)).ToArray();
            return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static NPC Receiver(int slot,Vector2 center)
        {
            var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.friendly=n.immortal=n.dontTakeDamage=false;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        private static void Periodic(object context,Assembly assembly,Type model,Type terrainType)
        {
            int cases=0;
            foreach(bool detached in new[]{false,true})foreach(bool wall in new[]{false,true})foreach(int clockValue in new[]{4,5})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3389,0,0);p.ResetEffects();p.channel=true;Main.windPhysics=false;
                foreach(var n in Main.npc)n.active=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
                var selected=Receiver(2,new Vector2(1040,667));Receiver(3,new Vector2(900,760));if(wall)for(int y=5;y<78;y++)NativeToolsChecks.Tile(60,y,1);
                var parent=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,667),new Vector2(2,0),603,40,0,p.whoAmI,detached?-2:0)];parent.penetrate=3;parent.localAI[1]=clockValue;
                var path=Timeline(assembly,selected);var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc")},null);
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var scene=Activator.CreateInstance(model,Flags,null,new object[]{p,parent,path,clock,terrain,null,false},null);var rows=(System.Collections.IList)Get(scene,"rows");var member=rows.Cast<object>().Single(r=>((Projectile)Get(r,"Shot")).whoAmI==parent.whoAmI);
                Require((bool)model.GetMethod("Periodic",Flags).Invoke(scene,new object[]{member,1}),"finite Terrarian periodic query acquired its target/LOS");
                parent.AI();var actual=Main.projectile.Where(q=>q.active && q.type==604).ToArray();var predicted=rows.Cast<object>().Select(r=>(Projectile)Get(r,"Shot")).Where(q=>q.type==604).ToArray();
                Require(actual.Length==predicted.Length && actual.Length==(clockValue==5?1:0),"original periodic6 trigger survives held/detached phases");
                if(actual.Length>0)Require(actual[0].position==predicted[0].position && actual[0].velocity==predicted[0].velocity && actual[0].damage==predicted[0].damage,"native periodic nearest-target and wall/slot ordering agree");cases++;
            }
            Console.WriteLine("YOYO-FAMILY original Terrarian periodic cases="+cases);
        }
        private static void DetachedMotors(object context,Assembly assembly,Type model,Type terrainType,MethodInfo copy)
        {
            int cases=0,actions=0;
            foreach(int type in new[]{545,546,554,555,999,603})foreach(int terrainCase in new[]{0,1,2})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3389,0,0);p.ResetEffects();p.channel=false;Main.windPhysics=false;Main.tileSolid[1]=true;
                foreach(var n in Main.npc)n.active=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
                if(terrainCase==1)for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);if(terrainCase==2)for(int y=5;y<78;y++)NativeToolsChecks.Tile(58,y,1);
                var native=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,650),new Vector2(3,8),type,0,0,p.whoAmI,-2)];native.penetrate=3;var expected=(Projectile)copy.Invoke(null,new object[]{native});
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                for(int t=1;t<=25 && native.active;t++)
                {
                    Require((bool)model.GetMethod("ParentStep",Flags).Invoke(null,new object[]{expected,p,p.Center,Vector2.Zero,false,terrain}),"detached finite parent motor acquired terrain");if(expected.active)expected.timeLeft=5;
                    NativeQuickItemChecks.BeginWorldStep();native.Update(native.whoAmI);actions++;
                    Require(native.active==expected.active && (!native.active || native.position==expected.position && native.velocity==expected.velocity),"native detached AI99 bounce/side-Kill exact type="+type+" terrain="+terrainCase+" t="+t+" native="+native.position+"/"+native.velocity+" model="+expected.position+"/"+expected.velocity);
                }
                cases++;
            }
            Console.WriteLine("YOYO-FAMILY original detached motors="+cases+" actions="+actions);
        }
        private static void Births(object context,Assembly assembly,Type model,Type terrainType)
        {
            int checkedCases=0;
            foreach(int parentType in new[]{545,546,554,555,999})foreach(bool ready in parentType==999?new[]{true}:new[]{false,true})
            {
                bool observed=false;
                for(int seed=0;seed<30 && !observed;seed++)
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3389,0,0);p.ResetEffects();Main.windPhysics=false;p.yoyoHitCounter=ready?0:1;
                    foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;Array.Clear(Projectile.perIDStaticNPCImmunity,0,Projectile.perIDStaticNPCImmunity.Length);
                    for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
                    foreach(int slot in new[]{2,3})
                    {
                        var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=new Vector2(slot==2?1040:900,500);n.life=n.lifeMax=100000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.friendly=n.immortal=n.dontTakeDamage=false;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);
                    }
                    int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,500),Vector2.UnitX,parentType,40,0,p.whoAmI);var parent=Main.projectile[parentSlot];
                    var path=Timeline(assembly,Main.npc[2]);var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc")},null);
                    var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    var scene=Activator.CreateInstance(model,Flags,null,new object[]{p,parent,path,clock,terrain,null,false},null);
                    var rows=(System.Collections.IList)Get(scene,"rows");object member=null;foreach(var row in rows)if(ReferenceEquals(Get(row,"Shot"),parent) || ((Projectile)Get(row,"Shot")).whoAmI==parentSlot){member=row;break;}
                    Require(member!=null && (bool)model.GetMethod("OnHit",Flags).Invoke(scene,new[]{member,(object)3,1,1}),"private yoyo actual OnHit stage modeled");
                    var predicted=rows.Cast<object>().Select(r=>(Projectile)Get(r,"Shot")).Where(q=>q.whoAmI!=parentSlot).ToArray();
                    Main.rand=new Terraria.Utilities.UnifiedRandom(seed);parent.Damage();
                    var actual=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && q.whoAmI!=parentSlot).ToArray();
                    if(parentType==999 && actual.Length==0)continue;observed=true;
                    int expected=!ready?0:parentType==546?5:parentType==554?3:1;
                    Require(actual.Length==expected && predicted.Length==expected && actual.Select(q=>q.type).OrderBy(q=>q).SequenceEqual(predicted.Select(q=>q.type).OrderBy(q=>q)),"original yoyo hit counter/type/count agrees parent="+parentType+" ready="+ready+" native="+actual.Length+" model="+predicted.Length);
                    Require(p.yoyoHitCounter==Convert.ToInt32(Get(scene,"counter")),"owner-global yoyo counter advances only on its original branch");
                    foreach(var q in actual)
                    {
                        Require(parentType==999?q.damage>=20 && q.damage<=21:q.damage==(parentType==545 || parentType==555?80:parentType==554?20:40),"native yoyo child damage stage");
                        if(parentType==545 || parentType==555)Require(q.localNPCImmunity[3]==-1 && predicted[0].localNPCImmunity[3]==-1,"original child excludes its triggering victim, not another selected receiver");
                        if(parentType==546)Require(Projectile.perIDStaticNPCImmunity[q.immunityIdentity,3]==Main.GameUpdateCount+20,"native crystal children share the victim static deadline");
                    }
                    // The owner's counter is a real same-GUC dependency even
                    // though the source/position/selected NPC did not change.
                    var root=Get(scene,"Obstacles");p.yoyoHitCounter++;Require(!(bool)Get(root,"BirthInputsCurrent"),"same-GUC owner-global counter change revokes the finite proof");
                    checkedCases++;
                }
                Require(observed,"actual native representative proc observed within finite seed set");
            }
            Console.WriteLine("YOYO-FAMILY original OnHit/counter/victim exclusions cases="+checkedCases);
        }
    }
}
