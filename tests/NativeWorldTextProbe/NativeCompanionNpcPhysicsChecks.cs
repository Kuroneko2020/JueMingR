using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using JueMingR.Platform.Companions;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionNpcPhysicsChecks
    {
        private const int Slot=22;
        private static Player body;private static NPC expected;
        private static NPC[] table;
        private static int controlDepth,rays,contacts,prepares,sharedAi;
        private static bool rayTable,contactTable;private static Vector2 rayPoint;private static Rectangle contactRect;
        private static void ControlBefore(object a){if(ReferenceEquals(Get(a,"Body"),body))controlDepth++;}
        private static Exception ControlFinal(object a,Exception __exception){if(ReferenceEquals(Get(a,"Body"),body))controlDepth--;return __exception;}
        private static void Ray(Vector2 __3,bool __result){if(controlDepth>0){rays++;rayPoint=__3;rayTable=__result && ReferenceEquals(Main.npc[Slot],expected);}}
        private static void Contact(Player __instance){if(ReferenceEquals(__instance,body)){contacts++;contactTable=ReferenceEquals(Main.npc[Slot],expected);contactRect=__instance.Hitbox;}}
        private static void Prepared(NPC npc){if(npc.whoAmI==Slot)prepares++;}
        private static void Ai(){if(ReferenceEquals(Main.npc,table))sharedAi++;}
        private static NPC Spawn(int type,byte generation)
        {var n=(NPC)AccessTools.Method(typeof(NPC),"NewNPCInstanceInSlot").Invoke(null,new object[]{Slot,generation});n.SetDefaults(type);n.active=true;return n;}
        private static NPC Publish(object host,NPC source,Action<int> publish)
        {
            uint tick=Main.GameUpdateCount;int count=prepares;publish(Slot);table=(NPC[])Get(host,"observedNpcs");var n=table[Slot];
            Require(ReferenceEquals(Main.npc[Slot],source) && !ReferenceEquals(n,source) && n.generation==source.generation && n.aiStyle==2 && n.noGravity,"native AI -> real23 -> product decoding prepares the actual shared instance and retains the human NPC");
            Require((long)Call(host,"NpcSampleTick",n)==tick && prepares==count+1,"physics preparation neither restamps23 nor repeats decode");return n;
        }
        internal static void Run(object host,Player first,Player second,object a,object b,Action<int> publish)
        {
            var human=Main.LocalPlayer;var bodies=new[]{human,first,second};var positions=bodies.Select(p=>p.position).ToArray();var velocities=bodies.Select(p=>p.velocity).ToArray();
            var pa=(CompanionProfile)Get(a,"Profile");var pb=(CompanionProfile)Get(b,"Profile");var ba=pa.Behavior;var bb=pb.Behavior;var brainA=Get(a,"Behavior");var brainB=Get(b,"Behavior");
            var items=new[]{first.inventory[0],first.inventory[54]};int selected=first.selectedItem,life=first.statLife,immuneTime=first.immuneTime;bool immune=first.immune;var cooldowns=(int[])first.hurtCooldowns.Clone();
            var old=Main.npc[Slot];double surface=Main.worldSurface;var tiles=new List<Tuple<int,int,Tile>>();
            var patch=new Harmony("JueMingR.Tests.CompanionNpcPhysics");body=first;controlDepth=rays=contacts=prepares=sharedAi=0;
            patch.Patch(AccessTools.Method(host.GetType(),"Control"),prefix:new HarmonyMethod(typeof(NativeCompanionNpcPhysicsChecks),nameof(ControlBefore)),finalizer:new HarmonyMethod(typeof(NativeCompanionNpcPhysicsChecks),nameof(ControlFinal)));
            patch.Patch(AccessTools.Method(typeof(Collision),"CanHitLine",new[]{typeof(Vector2),typeof(int),typeof(int),typeof(Vector2),typeof(int),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCompanionNpcPhysicsChecks),nameof(Ray)));
            patch.Patch(AccessTools.Method(typeof(Player),"Update_NPCCollision"),prefix:new HarmonyMethod(typeof(NativeCompanionNpcPhysicsChecks),nameof(Contact)));
            patch.Patch(AccessTools.Method(host.GetType(),"PrepareNpcObservationPhysics"),postfix:new HarmonyMethod(typeof(NativeCompanionNpcPhysicsChecks),nameof(Prepared)));
            patch.Patch(AccessTools.Method(typeof(NPC),"AI"),prefix:new HarmonyMethod(typeof(NativeCompanionNpcPhysicsChecks),nameof(Ai)));
            try
            {
                Main.worldSurface=40;Call(host,"ClearWorldSources");
                human.position=new Vector2(1200,918);first.position=new Vector2(1000,918);second.position=new Vector2(1240,918);foreach(var p in bodies)p.velocity=Vector2.Zero;
                foreach(var actor in new[]{a,b}){Set(actor,"Target",null);Set(actor,"AimTimeline",null);Set(actor,"AimTerrain",null);Set(actor,"HasAim",false);Set(actor,"Behavior",Activator.CreateInstance(brainA.GetType(),true));}
                Call(host,"SetBehavior",pa.Id,new CompanionBehavior{Follow=false,AttackEnabled=true,PreferOwner=false});Call(host,"SetBehavior",pb.Id,new CompanionBehavior{Follow=false,AttackEnabled=false});
                // Real no-ammo input leaves basic aiming/ray qualification
                // active while native ItemCheck cannot shoot this receiver.
                first.inventory[0]=new Item(ItemID.FlintlockPistol);first.inventory[54]=new Item();first.selectedItemState.Select(0);
                NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);foreach(var p in bodies)p.Update(p.whoAmI);
                var source=Spawn(NPCID.DemonEye,30);source.position=new Vector2(1100,878);source.velocity=new Vector2(4*(2-source.scale),1.5f*(2-source.scale));
                source.UpdateNPC(Slot);Require(source.noGravity,"original AI_002 actually establishes source noGravity before serialization");expected=Publish(host,source,publish);
                var falling=new NPC();falling.SetDefaults(NPCID.DemonEye);falling.position=source.position;falling.velocity=source.velocity;Require(!falling.noGravity,"legacy counterfactual starts with the real SetDefaults physical flags");
                var gravity=AccessTools.Field(typeof(NPC),"gravity");var updateGravity=AccessTools.Method(typeof(NPC),"UpdateNPC_UpdateGravity");long stamp=(long)Call(host,"NpcSampleTick",expected),revision=(long)Call(host,"NpcObservationRevision",expected);
                for(int step=1;step<=6;step++)
                {
                    int rayCount=rays,contactCount=contacts;NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);human.Update(human.whoAmI);first.Update(first.whoAmI);second.Update(second.whoAmI);
                    Require(ReferenceEquals(Get(a,"Target"),expected) && (Vector2)Get(a,"Aim")==expected.Center+expected.netOffset && rays>rayCount && rayTable && rayPoint==expected.Center,"actual basic aim and native reachability ray consume this shared flying geometry");
                    Require(contacts==contactCount+1 && contactTable && ReferenceEquals(Main.npc[Slot],source),"actual Player.Update contact stage borrows the same table then returns the human table");
                    source.UpdateNPC(Slot);int calls=prepares;Call(host,"AdvanceNpcObservation");var point=expected.position;Call(host,"AdvanceNpcObservation");
                    Require(prepares==calls+1 && expected.position==point && expected.position==source.position && expected.velocity==source.velocity,"six no23 flying steps follow actual native geometry, once per world step for two actors");
                    Require((long)Call(host,"NpcSampleTick",expected)==stamp && (long)Call(host,"NpcObservationRevision",expected)==revision,"physical preparation preserves the true sample age/revision");
                    float previous=(float)gravity.GetValue(null);try{object[] args={0f};updateGravity.Invoke(falling,args);falling.velocity.Y=Math.Min((float)args[0],falling.velocity.Y+(float)gravity.GetValue(null));falling.position+=falling.velocity;}finally{gravity.SetValue(null,previous);}
                    Console.WriteLine("NPC PHYSICS no23 age="+step+" native="+source.position+" observed="+point+" legacyGravity="+falling.position);
                }
                Require(falling.position.Y-expected.position.Y>1,"finite clear flight distinguishes the old false-gravity path by more than a pixel");
                first.position=new Vector2(expected.position.X,(float)Math.Floor(expected.position.Y)+1-first.height);first.velocity=Vector2.Zero;first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);
                int before=first.statLife,humanLife=human.statLife,contactBefore=contacts;Require(!first.creativeGodMode && !first.npcTypeNoAggro[expected.type],"contact witness is a real vulnerable fixture player");
                NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);human.Update(human.whoAmI);first.Update(first.whoAmI);second.Update(second.whoAmI);
                var legacyRect=new Rectangle((int)falling.position.X,(int)falling.position.Y,expected.width,expected.height);
                Require(contacts==contactBefore+1 && contactTable && !contactRect.Intersects(legacyRect) && first.statLife<before && human.statLife==humanLife && ReferenceEquals(Main.npc[Slot],source),"ContactThenGrapple -> real Update_NPCCollision -> Hurt damages the corrected edge contact which the old falling geometry would miss, without touching human life/table");
                Console.WriteLine("NPC PHYSICS native contact life="+before+"->"+first.statLife+" observed="+expected.position+" oldWouldMiss=True basicRays="+rays);
                first.position=new Vector2(800,918);first.velocity=Vector2.Zero;
                for(int x=71;x<=71;x++)for(int y=50;y<=65;y++){tiles.Add(Tuple.Create(x,y,Main.tile[x,y]));Main.tile[x,y]=new Tile();Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                foreach(int type in new[]{170,171,180})
                {
                    source=Spawn(type,(byte)(40+type-170));NPC previous=null;
                    foreach(bool ghost in new[]{false,true})
                    {
                        source.position=new Vector2(71*16-source.width-6,880);source.velocity=new Vector2(4,0);source.ai[0]=ghost?299:0;source.ai[1]=0;
                        source.UpdateNPC(Slot);Require(source.target==human.whoAmI && source.noGravity && source.noTileCollide==ghost && (source.ai[1]!=0)==ghost,"original pig branch actually produces normal/threshold ghost source states");
                        expected=Publish(host,source,publish);Require(expected.noTileCollide==ghost && (previous==null || ReferenceEquals(previous,expected)),"same-generation real23 updates the conditional collision flag in both shared states");previous=expected;
                        var ai=(float[])expected.ai.Clone();int target=expected.target;NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);Call(host,"AdvanceNpcObservation");
                        Require(expected.noTileCollide==ghost && (ghost?expected.position.X+expected.width>71*16:expected.position.X+expected.width<=71*16) && expected.ai.SequenceEqual(ai) && expected.target==target,"current synced phase alone controls real collision/passage without advancing another AI decision");
                        Console.WriteLine("NPC PHYSICS type="+type+" generation="+expected.generation+" ai1="+expected.ai[1]+" ghost="+ghost+" right="+(expected.position.X+expected.width));
                    }
                    source.position=new Vector2(1170,880);source.velocity=Vector2.Zero;source.UpdateNPC(Slot);Require(source.ai[1]==0 && !source.noTileCollide,"original clear-space ghost exit produces the reverse synchronized state");
                    expected=Publish(host,source,publish);Require(ReferenceEquals(previous,expected) && !expected.noTileCollide,"same-instance ghost to normal real23 revokes passage");
                }
                var retired=expected;source=Spawn(170,70);source.position=new Vector2(71*16-source.width-6,880);source.velocity=new Vector2(4,0);source.ai[0]=299;source.UpdateNPC(Slot);expected=Publish(host,source,publish);
                Require(!ReferenceEquals(retired,expected) && expected.generation==70 && expected.noGravity && expected.noTileCollide,"new generation prepares the actually decoded new instance from its own real ghost state");
                Require(sharedAi==0,"shared observation never runs a second NPC.AI");Console.WriteLine("NPC PHYSICS sharedAI=0; no new budget/cache/scan; bounded per-active-NPC flag preparation");
            }
            finally
            {
                body=null;expected=null;table=null;controlDepth=0;patch.UnpatchAll(patch.Id);Main.worldSurface=surface;foreach(var t in tiles)Main.tile[t.Item1,t.Item2]=t.Item3;Main.npc[Slot]=old;Call(host,"ClearWorldSources");
                for(int i=0;i<bodies.Length;i++){bodies[i].position=positions[i];bodies[i].velocity=velocities[i];}first.inventory[0]=items[0];first.inventory[54]=items[1];first.selectedItemState.Select(selected);first.statLife=life;first.immune=immune;first.immuneTime=immuneTime;Array.Copy(cooldowns,first.hurtCooldowns,cooldowns.Length);
                Call(host,"SetBehavior",pa.Id,ba);Call(host,"SetBehavior",pb.Id,bb);Set(a,"Behavior",brainA);Set(b,"Behavior",brainB);foreach(var actor in new[]{a,b}){Set(actor,"Target",null);Set(actor,"AimTimeline",null);Set(actor,"AimTerrain",null);Set(actor,"HasAim",false);}
            }
        }
    }
}
