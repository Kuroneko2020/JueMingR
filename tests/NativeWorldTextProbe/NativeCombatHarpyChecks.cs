using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHarpyChecks
    {
        private static bool watchBeeBirth;
        private static readonly HashSet<Projectile> beeParents=new HashSet<Projectile>();
        private static readonly List<Projectile> actualBees=new List<Projectile>();
        private static int beeKills;
        private static void BeeKill(Projectile __instance)
        {if(watchBeeBirth && __instance.active && __instance.type==469 && beeParents.Contains(__instance))beeKills++;}
        private static void BeeBorn(IEntitySource __0,int __result)
        {
            var source=__0 as EntitySource_Parent;
            if(!watchBeeBirth || source==null || !beeParents.Contains(source.Entity as Projectile) || __result<0 || __result>=Main.maxProjectiles)return;
            var q=Main.projectile[__result];if(q.type==181 || q.type==566)actualBees.Add(q);
        }
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostHarpyAttack",true);var arrowType=model.GetNestedType("Arrow",BindingFlags.NonPublic);var stateType=model.GetNestedType("State",BindingFlags.NonPublic);var clockType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);var phaseType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var obstacleType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);var snapshots=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true);
            bool wind=Main.windPhysics;int motors=0,steps=0,contacts=0;var savedRandom=Main.rand;
            var audit=new Harmony("JueMingR.Tests.HarpyEquipment");foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHarpyChecks).GetMethod("BeeKill",Flags)));
            audit.Patch(typeof(Projectile).GetMethod("NewProjectile",Flags,null,new[]{typeof(IEntitySource),typeof(float),typeof(float),typeof(float),typeof(float),typeof(int),typeof(int),typeof(float),typeof(int),typeof(float),typeof(float),typeof(float),typeof(NewProjectileModifier)},null),postfix:new HarmonyMethod(typeof(NativeCombatHarpyChecks).GetMethod("BeeBorn",Flags)));
            try
            {
                if(Environment.GetEnvironmentVariable("JMR_HARPY_ORACLE_ONLY")=="bee"){BeeFamilies(context,assembly,model,terrainType,clockType,phaseType,snapshots,obstacleType);return;}
                // Real player accessory routine chooses among real owned
                // arrows. Its cooldown=1 branch only decrements; equal owner
                // distances preserve slot order. Native AI/Update, not a mock
                // motor, is the independent continuation being compared.
                foreach(int type in new[]{1,2,278,469,485,710})foreach(int accessory in new[]{6166,6180,6181})foreach(int cooldown in new[]{0,1})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);Main.windPhysics=false;Main.rand=new Terraria.Utilities.UnifiedRandom(7123);Equip(p,accessory);p.position=new Vector2(700,646);p.accHarpyCharmCooldown=cooldown;p.inventory[54].SetDefaults(type==278?1334:40);p.inventory[54].stack=999;
                    var n=Target(2,new Vector2(1500,440));var timeline=Timeline(assembly,n,Main.GameUpdateCount);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[type],timeline,0,false,true},null);
                    int firstSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1200,450),new Vector2(0,6),type,0,0,p.whoAmI);int secondSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1200,450),new Vector2(0,6),type,0,0,p.whoAmI);
                    var lease=Activator.CreateInstance(stateType,Flags,null,new object[]{p,ammo,timeline,obstacle},null);var targets=Get(lease,"targets");var list=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(arrowType));foreach(var row in (IEnumerable)Get(lease,"arrows"))list.Add(CallResult(row,"Copy"));int privateCooldown=cooldown;
                    for(int tick=1;tick<=35;tick++)
                    {
                        var args=new object[]{list,targets,timeline,tick-1,terrain,p.Center,p.accSeraphNecklace,p.hasPhoenixQuiver,privateCooldown,lease};Require((bool)model.GetMethod("Charm",Flags).Invoke(null,args),"Harpy player-stage LOS dependencies are complete");privateCooldown=(int)args[8];typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(p,null);Require(p.accHarpyCharmCooldown==privateCooldown,"native Harpy cooldown phase agrees: "+type+"/"+accessory+"/"+cooldown+"/"+tick);
                        if(tick==1)Require(new Projectile.AI_001_Features(Main.projectile[firstSlot].ai[2]).HarpyCharmed==(cooldown==0) && !new Projectile.AI_001_Features(Main.projectile[secondSlot].ai[2]).HarpyCharmed,"native equal-distance contention and cooldown=1 do not charm two arrows");
                        for(int at=0;at<list.Count;at++)
                        {
                            var row=list[at];var q=Main.projectile[at==0?firstSlot:secondSlot];if(!q.active)continue;int updates=q.extraUpdates+1;for(int sub=0;sub<updates && (bool)Get(row,"Alive");sub++){Require((bool)model.GetMethod("Step",Flags).Invoke(null,new object[]{row,targets,timeline,tick,terrain}),"known Harpy motor retains terrain");model.GetMethod("QuiverAfterMovement",Flags).Invoke(null,new object[]{row,p.magicQuiver});}q.Update(q.whoAmI);
                            Require(q.active==(bool)Get(row,"Alive") && (!q.active || Vector2.DistanceSquared(q.Center,(Vector2)Get(row,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(row,"Velocity"))<.0001f && q.ai[2]==(float)Get(row,"Bits") && q.extraUpdates==(int)Get(row,"ExtraUpdates") && q.type==(int)Get(row,"Type")),"actual Harpy motor/captured bit stages agree: type="+type+" accessory="+accessory+" cooldown="+cooldown+" tick="+tick+" native="+q.Center+"/"+q.velocity+"/"+q.ai[2]+" model="+Get(row,"Center")+"/"+Get(row,"Velocity")+"/"+Get(row,"Bits"));steps++;
                        }
                    }
                    motors++;
                }
                foreach(int liquid in new[]{0,2})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);Main.windPhysics=false;Equip(p,6181);p.position=new Vector2(700,646);p.accHarpyCharmCooldown=0;p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;
                    for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                    var n=Target(2,new Vector2(1500,350));var timeline=Timeline(assembly,n,Main.GameUpdateCount);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1200,350),Vector2.UnitY*6,2,0,0,p.whoAmI);var q=Main.projectile[slot];Require(q.type==2 && q.wet && q.extraUpdates==0,"real Phoenix water oracle starts with wet native FireArrow birth clock");
                    var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[2],timeline,0,false,true},null);var lease=Activator.CreateInstance(stateType,Flags,null,new object[]{p,ammo,timeline,obstacle},null);var targets=Get(lease,"targets");var list=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(arrowType));foreach(var captured in (IEnumerable)Get(lease,"arrows"))list.Add(CallResult(captured,"Copy"));var row=list[0];int cooldown=0;
                    for(int tick=1;tick<=20;tick++)
                    {
                        var args=new object[]{list,targets,timeline,tick-1,terrain,p.Center,p.accSeraphNecklace,p.hasPhoenixQuiver,cooldown,lease};Require((bool)model.GetMethod("Charm",Flags).Invoke(null,args),"wet Phoenix charm has complete LOS dependencies");cooldown=(int)args[8];typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(p,null);int updates=q.extraUpdates+1;for(int sub=0;sub<updates;sub++){Require((bool)model.GetMethod("Step",Flags).Invoke(null,new object[]{row,targets,timeline,tick,terrain}),"wet Phoenix transformed motor is modeled");model.GetMethod("QuiverAfterMovement",Flags).Invoke(null,new object[]{row,p.magicQuiver});}q.Update(slot);
                        var features=new Projectile.AI_001_Features(q.ai[2]);Require(q.active && q.type==1 && (int)Get(row,"Type")==1 && features.HarpyCharmed && features.PhoenixQuivered && q.ai[2]==(float)Get(row,"Bits") && q.extraUpdates==(int)Get(row,"ExtraUpdates") && p.accHarpyCharmCooldown==cooldown && Vector2.DistanceSquared(q.Center,(Vector2)Get(row,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(row,"Velocity"))<.0001f,"water TransformType retains real Phoenix charm/features/clock: liquid="+liquid+" tick="+tick);steps+=updates;
                    }
                    motors++;
                }
                // Arrow components stay inside the native birth limit while
                // diagonal speed exceeds their collision body. The slope
                // phase must subdivide movement before ordinary arrow Kill.
                foreach(int type in new[]{1,2,278,469,485,710})foreach(byte slope in new byte[]{1,2,3,4})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);Main.windPhysics=false;Equip(p,6181);p.accHarpyCharmCooldown=90;
                    for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();for(int y=15;y<=45;y++)foreach(int x in new[]{40,80}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    for(int x=40;x<=80;x++)foreach(int y in new[]{15,45}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope(slope);
                    var n=Target(2,new Vector2(1500,100));var timeline=Timeline(assembly,n,Main.GameUpdateCount);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),936,390,15,8,type,0,0,p.whoAmI);var q=Main.projectile[slot];var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,q,timeline,0,false,true},null);var lease=Activator.CreateInstance(stateType,Flags,null,new object[]{p,ammo,timeline,obstacle},null);var row=((IEnumerable)Get(lease,"arrows")).Cast<object>().Single();var targets=Get(lease,"targets");int ticks=0;
                    for(int tick=1;tick<=40 && q.active;tick++)
                    {int updates=q.extraUpdates+1;for(int sub=0;sub<updates && (bool)Get(row,"Alive");sub++){Require((bool)model.GetMethod("Step",Flags).Invoke(null,new object[]{row,targets,timeline,tick,terrain}),"fast arrow segmented slope keeps finite terrain");model.GetMethod("QuiverAfterMovement",Flags).Invoke(null,new object[]{row,p.magicQuiver});}q.Update(slot);ticks++;Require(q.active==(bool)Get(row,"Alive") && (!q.active || Vector2.DistanceSquared(q.Center,(Vector2)Get(row,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(row,"Velocity"))<.0001f),"fast owned arrow native segmented slope/death agrees: "+type+"/"+slope+"/"+tick+" native="+q.Center+"/"+q.velocity+" model="+Get(row,"Center")+"/"+Get(row,"Velocity"));steps+=updates;}
                    Require(!q.active && ticks>0,"fast owned arrow reaches actual solid retirement");motors++;
                }
                foreach(int accessory in new[]{6166,6181})foreach(bool nextWorld in new[]{false,true})foreach(int delay in new[]{0,4})foreach(bool contention in new[]{false,true})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);Main.windPhysics=false;Equip(p,accessory);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;p.accHarpyCharmCooldown=0;p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;
                    var n=Target(2,new Vector2(1500,646));var timeline=Timeline(assembly,n,nextWorld?Main.GameUpdateCount:Main.GameUpdateCount-1);if(contention)Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1200,550),new Vector2(0,6),1,10,0,p.whoAmI);
                    var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,nextWorld?"CompletedWorld":"BeforeNpc"),delay},null);var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[(int)Get(ammo,"Projectile")],timeline,delay,!nextWorld,nextWorld},null);var origin=p.RotatedRelativePoint(p.MountedCenter);var args=new object[]{p,ammo,timeline,origin,clock,terrain,obstacle,null};var contact=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,args);var lease=args[7];
                    Require(contact!=null && contact.Confidence==AttackConfidence.Conditional,"enabled Harpy retains a finite damage plan including player phase/delay/contention: "+nextWorld+"/"+delay+"/"+contention+" stop="+Get(lease,"Stop"));Require((bool)Get(lease,"Current") && (bool)Get(lease,"MembersCurrent"),"fresh Harpy dependency lease is current");
                    int newborn=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(100,100),Vector2.UnitX,1,10,0,p.whoAmI);Require(!(bool)Get(lease,"Current") && (bool)Get(lease,"MembersCurrent"),"new arrow invalidates consume snapshot without making Draw scan new scene");Main.projectile[newborn].active=false;Require((bool)Get(lease,"Current"),"restored arrow set restores independent snapshot");p.accHarpyCharmCooldown=1;Require(!(bool)Get(lease,"MembersCurrent"),"accessory cooldown invalidates bounded presentation lease");p.accHarpyCharmCooldown=0;n.chaseable=false;Require(!(bool)Get(lease,"Current"),"native chase qualification changes retire complete lease");n.chaseable=true;
                    Projectile primary=null;int first=0;
                    for(int frame=0;frame<120;frame++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();if(nextWorld || frame>0)typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(p,null);
                        if(frame==delay){Vector2 velocity=(new Vector2(contact.AimX,contact.AimY)-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");newborn=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,(int)Get(ammo,"Projectile"),50,0,p.whoAmI);primary=Main.projectile[newborn];Require(!new Projectile.AI_001_Features(primary.ai[2]).HarpyCharmed,"native player-stage charm precedes primary birth");}
                        n.UpdateNPC(n.whoAmI);for(int slot=0;slot<Main.maxProjectiles;slot++){var q=Main.projectile[slot];if(!q.active)continue;int life=n.life;q.Update(slot);if(ReferenceEquals(q,primary) && n.life<life && first==0)first=frame+1;}
                        if(first>0 || primary!=null && !primary.active)break;
                    }
                    Require(first==contact.Tick,"Harpy native first primary Damage agrees with phase/delay/old-arrow contention: "+nextWorld+"/"+delay+"/"+contention+" expected="+contact.Tick+" first="+first);Console.WriteLine("HARPY contact nextWorld="+nextWorld+" delay="+delay+" contention="+contention+" tick="+first+" candidates="+Get(lease,"CandidateCount")+" motor="+Get(lease,"MotorSteps")+" charmQueries="+Get(lease,"CharmChecks"));contacts++;
                }
                PhoenixFamilies(context,assembly,model,terrainType,clockType,phaseType,snapshots,obstacleType);
                BeeFamilies(context,assembly,model,terrainType,clockType,phaseType,snapshots,obstacleType);
            }
            finally{watchBeeBirth=false;beeParents.Clear();actualBees.Clear();audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.rand=savedRandom;foreach(var item in Main.LocalPlayer.armor)item.TurnToAir();Main.LocalPlayer.ResetEffects();}
            Console.WriteLine("PASS Harpy native player clock/contention/AI001 motor="+motors+" cases/"+steps+" steps; exact primary first Damage="+contacts+"; consume/presentation invalidation");
        }
        private static void PhoenixFamilies(object context,Assembly assembly,Type model,Type terrainType,Type clockType,Type phaseType,Type snapshots,Type obstacleType)
        {
            int derived=0,burst=0,negative=0;
            foreach(int accessory in new[]{6166,6181})foreach(int scene in Enumerable.Range(0,6))
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3859,0,0);Main.windPhysics=false;Equip(p,accessory);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;p.controlUseItem=false;p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;
                // Keep the owner's real floor; only the projectile arena is
                // clear. Full Player.Update below owns movement and Charm.
                for(int x=50;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var npc in Main.npc)npc.active=false;
                bool wall=scene==2 || scene==4,immune=scene==1,expiry=scene==5;var selected=Target(2,wall?new Vector2(1042,390):new Vector2(970,410));selected.noTileCollide=true;
                // 710 uses local immunity, so owner immunity still gates it.
                // 100 expires inside this 120-outer oracle; use 200 for the
                // continuous negative, and preserve 100 as a separate expiry
                // case rather than pretending it blocks the whole interval.
                NPC front=null;if(!wall){front=Target(1,new Vector2(982,340));front.noTileCollide=true;if(immune || expiry)front.immune[p.whoAmI]=immune?200:100;}
                if(wall)for(int y=15;y<=45;y++){Main.tile[65,y].active(true);Main.tile[65,y].type=1;}
                if(scene>=3)for(int i=0;i<7;i++){var q=Main.projectile[i];q.SetDefaults(1);q.whoAmI=i;q.active=true;q.owner=254;q.tileCollide=false;q.damage=0;q.Center=new Vector2(800,200);}
                if(expiry)
                {
                    // R15 proved this geometry can hit selected directly at
                    // 88 while front is still immune. Observe the full native
                    // 100-update expiry first; then compare the born family
                    // at its actual launch state, with a legal front OnHit.
                    for(int prior=0;prior<100;prior++)
                    {
                        NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center,false);NativeQuickItemChecks.BeginWorldStep();p.controlUseItem=false;p.Update(p.whoAmI);front.UpdateNPC(1);selected.UpdateNPC(2);
                        Require(front.immune[p.whoAmI]==99-prior,"native owner immunity expires on exactly the hundredth outer update");
                    }
                }
                Vector2 origin=wall?new Vector2(950,410):new Vector2(950,350),velocity=wall?new Vector2(10,0):new Vector2(6,0);int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,710,20,0,p.whoAmI);var parent=Main.projectile[parentSlot];var key=parent.key;if(scene>=3)for(int i=0;i<5;i++)Main.projectile[i].active=false;
                var timeline=Timeline(assembly,selected,Main.GameUpdateCount);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"CompletedWorld"),0},null);var expected=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{parent},timeline,clock,terrain});int first=0,frontHits=0,children=0,firstFront=0;bool firstChild=false;
                for(int outer=1;outer<=120;outer++)
                {
                    NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center,false);NativeQuickItemChecks.BeginWorldStep();p.controlUseItem=false;p.Update(p.whoAmI);if(front!=null)front.UpdateNPC(1);selected.UpdateNPC(2);
                    for(int slot=0;slot<Main.maxProjectiles;slot++)
                    {
                        var q=Main.projectile[slot];if(!q.active || q.owner!=p.whoAmI)continue;var beforeKey=q.key;int life=selected.life,frontLife=front?.life??0,frontImmune=front?.immune[p.whoAmI]??0;q.Update(slot);
                        if(front!=null && front.life<frontLife){Require(frontImmune==0,"native710 qualified front OnHit occurs only after owner immunity is clear");if(firstFront==0)firstFront=outer;frontHits++;foreach(var child in Main.projectile.Where(a=>a.active && a.owner==p.whoAmI && a.type==710 && !a.key.Equals(key))){Require(child.ai[1]==-1 && child.localNPCImmunity[1]==-1,"Harpy native Betsy children inherit actual front OnHit local immunity");children++;}}
                        if(first==0 && selected.life<life){first=outer;firstChild=!beforeKey.Equals(key);}
                    }
                    if(first>0 || outer>=90 && !Main.projectile.Any(a=>a.active && a.owner==p.whoAmI))break;
                }
                Console.WriteLine("HARPY PHOENIX gate accessory="+accessory+" scene="+scene+" native="+first+" model="+expected?.Tick+" firstFront="+firstFront+" front="+frontHits+" children="+children+" immuneRemaining="+(front?.immune[p.whoAmI]??0));
                Require(first==(expected?.Tick??0),"equipped Harpy/Phoenix born parent-child-Kill first Damage agrees: accessory="+accessory+" scene="+scene+" native="+first+" model="+expected?.Tick+" child="+firstChild+" front="+frontHits);
                // Equipment changes the parent's later path: a legal primary
                // contact and a genuine miss both retain this OnHit negative.
                // The preceding native/model equality still checks first Damage.
                if(immune){Require(!firstChild && frontHits==0 && children==0,"Harpy owner-immune front creates no OnHit child; later primary contact remains governed by native/model equality");negative++;}
                else if(wall){Require(first>0 && !firstChild && frontHits==0,"Harpy native 710 wall Kill has useful burst Damage");burst++;}
                else{Require(first>0 && firstChild && frontHits==1 && children==5,"Harpy native 710 front OnHit has five useful copied-local children in either slot order");if(expiry)Require(firstFront>0 && front.immune[p.whoAmI]==0,"observed100-owner-immunity expiry precedes the new family's legal child-producing OnHit");derived++;}
                Console.WriteLine("HARPY PHOENIX family accessory="+accessory+" scene="+scene+" parentSlot="+parentSlot+" native="+first+" model="+expected?.Tick+" child="+firstChild+" front="+frontHits+" children="+children);
            }
            foreach(int accessory in new[]{6166,6181})foreach(int delay in new[]{0,4})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3859,0,0);Main.windPhysics=false;Equip(p,accessory);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;p.controlUseItem=false;p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;p.accHarpyCharmCooldown=0;var selected=Target(2,new Vector2(1100,646));selected.noTileCollide=true;
                var timeline=Timeline(assembly,selected,Main.GameUpdateCount);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});Require((int)Get(ammo,"Projectile")==710,"native PhantomPhoenix conversion retains 710 with real Harpy equipment");var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"CompletedWorld"),delay},null);var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[710],timeline,delay,false,true},null);Vector2 origin=p.RotatedRelativePoint(p.MountedCenter);var args=new object[]{p,ammo,timeline,origin,clock,terrain,obstacle,null};var expected=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,args);Require(expected!=null,"equipped Harpy has a useful six-primary Phoenix hand plan: "+accessory+"/"+delay+" stop="+Get(args[7],"Stop"));int first=0,births=0;
                for(int frame=0;frame<120;frame++)
                {
                    NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center,false);NativeQuickItemChecks.BeginWorldStep();p.controlUseItem=false;p.Update(p.whoAmI);
                    if(frame==delay){NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(expected.AimX,expected.AimY),true);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});births=Main.projectile.Count(a=>a.active && a.owner==p.whoAmI);Require(births==6,"native Harpy PhantomPhoenix emits all six unequal-speed primaries");foreach(var q in Main.projectile.Where(a=>a.active && a.owner==p.whoAmI))Require(!new Projectile.AI_001_Features(q.ai[2]).HarpyCharmed,"native player Charm precedes all six primary births");}
                    selected.UpdateNPC(2);for(int slot=0;slot<Main.maxProjectiles;slot++){var q=Main.projectile[slot];if(!q.active || q.owner!=p.whoAmI)continue;int life=selected.life;q.Update(slot);if(first==0 && selected.life<life)first=frame+1;}if(first>0)break;
                }
                Require(first>0 && first==expected.Tick,"equipped Harpy six-primary actual first Damage agrees including launch delay: "+accessory+"/"+delay+" native="+first+" expected="+expected.Tick);Console.WriteLine("HARPY PHOENIX six-primary accessory="+accessory+" delay="+delay+" births="+births+" native="+first+" model="+expected.Tick);
            }
            Require(derived==6 && burst==4 && negative==2,"Harpy compound family retains useful child/burst roles, actual immunity expiry and continuous immune counterexamples");
        }
        private static void BeeFamilies(object context,Assembly assembly,Type model,Type terrainType,Type clockType,Type phaseType,Type snapshots,Type obstacleType)
        {
            int cases=0,hits=0,lowerCases=0,ambientCases=0;
            foreach(int accessory in new[]{6166,6181})foreach(bool lower in new[]{false,true})foreach(int scene in Enumerable.Range(0,9))
            {
                var p=BeeReset(context,accessory);Main.rand=new Terraria.Utilities.UnifiedRandom(7230+scene);p.accHarpyCharmCooldown=200;
                // Strong bees come from a real second equipped accessory.
                if(scene==2 || scene==4){p.armor[4].SetDefaults(3333);p.Update(p.whoAmI);Require(p.strongBees,"native HiveBackpack grants strong-bee birth premise");}
                var selected=Target(2,scene==5?new Vector2(880,330):scene==8?new Vector2(1290,330):new Vector2(1040,330));selected.noTileCollide=true;
                if(scene>=1 && scene<=4)for(int x=55;x<=62;x++)for(int y=18;y<=28;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:scene==3?1:3);}
                if(scene==5)for(int y=12;y<=45;y++){Main.tile[60,y].active(true);Main.tile[60,y].type=1;}
                NPC front=null;if(scene==7){front=Target(1,new Vector2(958,330));front.noTileCollide=true;front.immune[p.whoAmI]=200;}
                if(lower)FillSlots(7);
                int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene==8?1200:950,350,12,0,469,90,0,p.whoAmI);var parent=Main.projectile[parentSlot];
                if(lower)for(int i=0;i<5;i++)Main.projectile[i].active=false;
                if(scene==0 || scene==2 || scene==8)parent.timeLeft=1;
                if(scene==6)parent.position.X=Main.leftWorld-1;
                if(scene==4)parent.velocity.Y=0;
                if(scene==8)
                {
                    p.accHarpyCharmCooldown=0;NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center,false);NativeQuickItemChecks.BeginWorldStep();p.Update(p.whoAmI);selected.UpdateNPC(2);
                    var features=new Projectile.AI_001_Features(parent.ai[2]);Require(features.HarpyCharmed && features.PhoenixQuivered==(accessory==6181),"real equipped player-stage Charm precedes this captured469 tail-death parent");
                }
                var before=Timeline(assembly,selected,Main.GameUpdateCount);var futureTerrain=Activator.CreateInstance(terrainType,true);Call(futureTerrain,"Reset");
                var beforeClock=Activator.CreateInstance(clockType,Flags,null,new object[]{before,Enum.Parse(phaseType,"Projectiles"),0},null);
                var unborn=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{parent},before,beforeClock,futureTerrain});
                if(scene==0)Require(unborn!=null && unborn.Confidence==AttackConfidence.Representative,"captured tail-death parent retains a useful explicitly representative future Bee family");
                beeParents.Clear();actualBees.Clear();beeParents.Add(parent);beeKills=0;watchBeeBirth=true;
                try{parent.Update(parentSlot);}finally{watchBeeBirth=false;}
                var children=actualBees.ToArray();
                if(scene==6){Require(beeKills==0 && children.Length==0 && unborn==null,"native pre-AI world retirement has no Kill, child or invented family Contact");cases++;continue;}
                Require(!parent.active && beeKills==1 && children.Length>=3 && children.Length<=6,"real BeeArrow Update reaches one causal Kill and the native3..6 birth denominator: "+accessory+"/"+lower+"/"+scene+" kills="+beeKills+" children="+children.Length);
                Require(children.All(member=>member.whoAmI!=parentSlot && member.owner==p.whoAmI && member.ranged && !member.magic && member.penetrate==2 && member.immunityIdentity==181),"parent remains occupied through native child allocation; BeeArrow children keep ranged/static181/penetrate2");
                Require(lower?children.Any(member=>member.whoAmI<parentSlot):children.All(member=>member.whoAmI>parentSlot),"native first-free birth slots distinguish lower deferred children from current-outer upper children");
                if(scene==7)Require(front.life<10000,"real penetrate1 BeeArrow bypasses owner immunity and its front OnHit causes the death family");
                var timeline=Timeline(assembly,selected,Main.GameUpdateCount);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"Projectiles"),0},null);
                var expected=(AttackContact)model.GetMethod("KnownAfterSlot",Flags).Invoke(null,new object[]{p,children,timeline,clock,terrain,parentSlot});
                int first=BeeContinue(context,p,selected,front,children,parentSlot);
                Require(first==(expected?.Tick??-1),"actual Harpy Bee family first source Damage agrees with current-outer slot order/liquid/static immunity: "+accessory+"/"+lower+"/"+scene+" native="+first+" model="+expected?.Tick);
                if(scene==1 || scene==3)Require(first==-1,"native water/lava-killed children cannot contribute a useful Contact");
                if(scene==0)Require(first>=0,"dry native tail-death children have actual useful Damage in either slot order");
                if(first>=0)hits++;if(lower)lowerCases++;cases++;
                Console.WriteLine("HARPY BEE actual accessory="+accessory+" scene="+scene+" lower="+lower+" parent="+parentSlot+" children="+children.Length+" native="+first+" model="+expected?.Tick);
            }
            // A source member must share the actual owner's history with both
            // earlier ordinary arrows and ambient bees; ambient Damage itself
            // never becomes this family's Contact.
            foreach(bool beeFirst in new[]{false,true})foreach(int immune in new[]{0,8,200})
            {
                var p=BeeReset(context,6166);p.accHarpyCharmCooldown=200;var selected=Target(2,new Vector2(950,330));selected.noTileCollide=true;selected.immune[p.whoAmI]=immune;
                int firstType=beeFirst?181:5,secondType=beeFirst?5:181;
                int a=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,1,0,firstType,60,0,p.whoAmI),b=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,1,0,secondType,60,0,p.whoAmI);
                var source=Main.projectile[b];if(source.type==181)source.ai[0]=30;
                var timeline=Timeline(assembly,selected,Main.GameUpdateCount);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"CompletedWorld"),0},null);
                var expected=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{source},timeline,clock,terrain});int first=-1,ambient=0;
                for(int outer=1;outer<=120;outer++)
                {
                    NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center,false);NativeQuickItemChecks.BeginWorldStep();p.controlUseItem=false;p.Update(p.whoAmI);selected.UpdateNPC(2);
                    for(int slot=0;slot<Main.maxProjectiles;slot++){var q=Main.projectile[slot];if(!q.active)continue;int life=selected.life;var key=q.key;q.Update(slot);if(selected.life<life){if(q==source && q.key.Equals(key) && first<0)first=outer;else ambient++;}}
                    if(first>=0 || !source.active)break;
                }
                Require(first==(expected?.Tick??-1),"ambient Bee/JesterArrow shared owner/static event cannot be attributed to the later source: "+beeFirst+"/"+immune+" native="+first+" model="+expected?.Tick+" ambient="+ambient);
                if(immune==200)Require(first==-1 && ambient==0,"continuous owner immunity blocks both legal receiver streams");
                if(immune==0 && beeFirst)Require(first==1 && ambient>0,"earlier native Bee static hit clears owner immunity before the later real arrow source");ambientCases++;
            }
            BeeHand(context,assembly,model,terrainType,clockType,phaseType,snapshots);
            BeeWorkload(context,assembly,model,terrainType,clockType,phaseType,snapshots,obstacleType);
            Require(hits>0 && lowerCases>0 && ambientCases==6,"real causal family matrix includes useful source Damage, lower slots and immunity counterexamples");
            Console.WriteLine("PASS HARPY BEE families="+cases+" useful="+hits+" lower="+lowerCases+" ambient="+ambientCases);
        }
        private static Player BeeReset(object context,int accessory)
        {
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),2888,0,0);Main.windPhysics=false;Equip(p,accessory);p.position=new Vector2(700,646);p.velocity=Vector2.Zero;p.controlUseItem=false;p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;
            for(int x=50;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var npc in Main.npc)npc.active=false;for(int i=0;i<Main.maxNPCs;i++)Projectile.perIDStaticNPCImmunity[181,i]=0;return p;
        }
        private static void FillSlots(int count)
        {for(int i=0;i<count;i++){var q=Main.projectile[i];q.SetDefaults(1);q.whoAmI=i;q.active=true;q.owner=254;q.tileCollide=false;q.damage=0;q.Center=new Vector2(800,200);}}
        private static int BeeContinue(object context,Player p,NPC selected,NPC front,Projectile[] children,int afterSlot)
        {
            var keys=new HashSet<ProjectileKey>(children.Select(member=>member.key));int first=-1;
            for(int outer=0;outer<120;outer++)
            {
                if(outer>0){NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center,false);NativeQuickItemChecks.BeginWorldStep();p.controlUseItem=false;p.Update(p.whoAmI);if(front!=null)front.UpdateNPC(1);selected.UpdateNPC(2);}
                for(int slot=outer==0?afterSlot+1:0;slot<Main.maxProjectiles;slot++)
                {var q=Main.projectile[slot];if(!q.active)continue;int life=selected.life;bool source=keys.Contains(q.key);q.Update(slot);if(source && selected.life<life && first<0)first=outer;}
                if(first>=0 || !children.Any(member=>member.active && keys.Contains(member.key)))break;
            }
            return first;
        }
        private static void BeeHand(object context,Assembly assembly,Type model,Type terrainType,Type clockType,Type phaseType,Type snapshots)
        {
            foreach(int accessory in new[]{6166,6181})
            {
                var p=BeeReset(context,accessory);p.accHarpyCharmCooldown=200;Main.rand=new Terraria.Utilities.UnifiedRandom(7341);var selected=Target(2,new Vector2(900,430));selected.noTileCollide=true;for(int y=15;y<=45;y++){Main.tile[65,y].active(true);Main.tile[65,y].type=1;}
                var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});Require((int)Get(ammo,"Projectile")==469,"real BeeKnees+ordinary arrows resolves the causal469 role");
                NativeToolExecutionChecks.Sample(context,Get(context,"Input"),p.Center+new Vector2(800,-350),true);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var parent=Main.projectile.Single(member=>member.active && member.owner==p.whoAmI && member.type==469);
                Require(parent.damage==(int)Get(ammo,"Damage"),"Harpy BeeArrow actual PickAmmo damage equals the captured hand damage used by death children");
                beeParents.Clear();actualBees.Clear();beeParents.Add(parent);beeKills=0;watchBeeBirth=true;int deathOuter=0;AttackContact expected=null;int first=-1;
                try
                {
                    for(int outer=1;outer<=90 && deathOuter==0;outer++)
                    {
                        NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center,false);NativeQuickItemChecks.BeginWorldStep();p.controlUseItem=false;p.Update(p.whoAmI);selected.UpdateNPC(2);
                        for(int slot=0;slot<Main.maxProjectiles;slot++)
                        {
                            var q=Main.projectile[slot];if(!q.active)continue;q.Update(slot);
                            if(beeKills==0)continue;deathOuter=outer;var children=actualBees.ToArray();Require(children.Length>=3 && children.Length<=6 && selected.life==10000,"real hand parent reaches terrain Kill before selected primary Damage");
                            var timeline=Timeline(assembly,selected,Main.GameUpdateCount);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"Projectiles"),0},null);
                            expected=(AttackContact)model.GetMethod("KnownAfterSlot",Flags).Invoke(null,new object[]{p,children,timeline,clock,terrain,slot});watchBeeBirth=false;first=BeeContinue(context,p,selected,null,children,slot);break;
                        }
                    }
                }
                finally{watchBeeBirth=false;}
                Require(deathOuter>0 && first>=0 && first==(expected?.Tick??-1),"native equipped Harpy BeeKnees causal terrain-death family has actual useful Damage at its predicted born interval: "+accessory+" death="+deathOuter+" first="+first+" model="+expected?.Tick);
                Console.WriteLine("HARPY BEE hand accessory="+accessory+" deathOuter="+deathOuter+" children="+actualBees.Count+" useful="+first);
            }
        }
        private static void BeeWorkload(object context,Assembly assembly,Type model,Type terrainType,Type clockType,Type phaseType,Type snapshots,Type obstacleType)
        {
            var p=BeeReset(context,6166);p.accHarpyCharmCooldown=200;var selected=Target(2,new Vector2(1040,330));selected.dontTakeDamage=true;selected.noTileCollide=true;
            // A real wall forces some directions through the death factory;
            // the selected NPC still cannot receive any source damage.
            for(int y=12;y<=45;y++){Main.tile[60,y].active(true);Main.tile[60,y].type=1;}
            var timeline=Timeline(assembly,selected,Main.GameUpdateCount);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"CompletedWorld"),0},null);var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[469],timeline,0,false,true},null);
            var bee=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBeeAttack",true);bee.GetMethod("ResetCounters",Flags).Invoke(null,null);var args=new object[]{p,ammo,timeline,p.RotatedRelativePoint(p.MountedCenter),clock,terrain,obstacle,null};var contact=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,args);var lease=args[7];
            int candidates=(int)Get(lease,"CandidateCount"),motors=(int)Get(lease,"MotorSteps"),beeSteps=(int)Get(lease,"BeeSteps"),births=(int)Get(lease,"BeeBirths"),captures=(int)bee.GetField("TargetCaptures",Flags).GetValue(null);
            Require(contact==null && candidates>0 && candidates<=41 && births>0 && beeSteps>0 && motors<=41*4*120*4 && captures==1 && (int)Get(lease,"CapturedArrowSlots")==Main.maxProjectiles && (int)Get(lease,"CapturedNpcSlots")==Main.maxNPCs,"hard-miss Harpy Bee preparation captures the real scene/chase once and retains bounded complete candidate/motor work");
            Require((bool)Get(lease,"Current") && (bool)Get(lease,"MembersCurrent"),"private family replay leaves the native preparation premises intact");
            int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,2,0,181,30,0,p.whoAmI);Require(!(bool)Get(lease,"Current") && (bool)Get(lease,"MembersCurrent"),"new real Bee invalidates full consume capture while Draw's identity lease does not scan the scene");Main.projectile[slot].active=false;
            var foreign=Main.projectile[slot];foreign.SetDefaults(181);foreign.whoAmI=slot;foreign.owner=254;foreign.active=true;foreign.Center=new Vector2(950,350);foreign.damage=30;
            var nextTerrain=Activator.CreateInstance(terrainType,true);Call(nextTerrain,"Reset");var nextObstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[469],timeline,0,false,true},null);var pending=new object[]{p,ammo,timeline,p.RotatedRelativePoint(p.MountedCenter),clock,nextTerrain,nextObstacle,null};
            Require(model.GetMethod("Solve",Flags).Invoke(null,pending)==null && (bool)Get(pending[7],"ForeignBeeCompetition") && (PredictionStop)Get(pending[7],"Stop")==PredictionStop.UnsupportedMechanism,"cross-owner static181 competition remains explicitly pending instead of writing foreign hits into local owner history");foreign.active=false;
            Console.WriteLine("HARPY BEE WORKLOAD captures="+captures+" candidates="+candidates+" births="+births+" motors="+motors+" beeSteps="+beeSteps+" receive="+Get(lease,"ReceivePasses"));
        }
        private static void Equip(Player p,int accessory)
        {foreach(var item in p.armor)item.TurnToAir();p.armor[3].SetDefaults(accessory);p.controlUseItem=false;p.Update(p.whoAmI);Require(p.accHarpyCharm && p.accSeraphNecklace==(accessory==6180) && p.hasPhoenixQuiver==(accessory==6181) && p.magicQuiver==(accessory==6181),"real native equipped Harpy/Seraph/Phoenix effects are captured");}
        private static object CallResult(object instance,string name){return instance.GetType().GetMethod(name,Flags).Invoke(instance,null);}
        private static NPC Target(int slot,Vector2 position)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.life=n.lifeMax=10000;n.aiStyle=-1;n.noGravity=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.defense=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);return n;}
        private static NpcTrajectory Timeline(Assembly assembly,NPC n,ulong sampleTick)
        {var selection=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);var key=(NpcIdentity)selection.GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,Wet=n.wet};var points=Enumerable.Range(0,181).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,(long)sampleTick,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
    }
}
