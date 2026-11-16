using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using JueMingR.Platform.Companions;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionNpcStateChecks
    {
        private static Player geometryBody;private static NPC geometryNpc;private static int geometryDepth,geometryRays;private static Vector2 geometryRayPoint;
        private static void GeometryControl(object a){if(ReferenceEquals(Get(a,"Body"),geometryBody))geometryDepth++;}
        private static Exception GeometryFinal(object a,Exception __exception){if(ReferenceEquals(Get(a,"Body"),geometryBody))geometryDepth--;return __exception;}
        private static void GeometryRay(Vector2 __3){if(geometryDepth>0){geometryRays++;geometryRayPoint=__3;}}
        private static void NativeClubStep(NPC source)
        {
            // The headless ABI fixture has no NPC texture asset. Native
            // FindFrame's dedServ path runs the same Ogre index/counter branch
            // without that rendering prerequisite; it still runs exactly once
            // inside the complete original NPC.UpdateNPC, never a test frame.
            bool dedicated=Main.dedServ;Main.dedServ=true;try{source.UpdateNPC(Slot);}finally{Main.dedServ=dedicated;}
        }
        internal static void Geometry(object host,Player first,Player second,object a,object b,Action<int> publish,bool club=false)
        {
            var human=Main.LocalPlayer;var players=new[]{human,first,second};var positions=players.Select(p=>p.position).ToArray();var velocities=players.Select(p=>p.velocity).ToArray();var old=Main.npc[Slot];
            var pa=(CompanionProfile)Get(a,"Profile");var pb=(CompanionProfile)Get(b,"Profile");var ba=pa.Behavior;var bb=pb.Behavior;var brains=new[]{Get(a,"Behavior"),Get(b,"Behavior")};
            var items=new[]{first.inventory[0],first.inventory[54]};int selected=first.selectedItem,life=first.statLife,max=first.statLifeMax,max2=first.statLifeMax2,immuneTime=first.immuneTime;bool immune=first.immune;var cooldowns=(int[])first.hurtCooldowns.Clone();var water=new List<Tuple<int,int,Tile>>();
            var patch=new Harmony("JueMingR.Tests.CompanionCurrentGeometry");geometryBody=first;geometryDepth=geometryRays=0;
            patch.Patch(AccessTools.Method(host.GetType(),"Control"),prefix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(GeometryControl)),finalizer:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(GeometryFinal)));
            patch.Patch(AccessTools.Method(typeof(Collision),"CanHitLine",new[]{typeof(Vector2),typeof(int),typeof(int),typeof(Vector2),typeof(int),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCompanionNpcStateChecks),nameof(GeometryRay)));
            try
            {
                Call(host,"ClearWorldSources");human.position=new Vector2(1000,918);first.position=new Vector2(1050,918);second.position=new Vector2(1250,918);
                Call(host,"SetBehavior",pa.Id,new CompanionBehavior{Follow=false,AttackEnabled=true,PreferOwner=false});Call(host,"SetBehavior",pb.Id,new CompanionBehavior{Follow=false,AttackEnabled=false});
                foreach(var actor in new[]{a,b}){Set(actor,"Behavior",Activator.CreateInstance(brains[0].GetType(),true));Set(actor,"Target",null);Set(actor,"HasAim",false);}
                first.inventory[0]=new Item(ItemID.FlintlockPistol);first.inventory[54]=new Item();first.selectedItemState.Select(0);first.statLife=first.statLifeMax=first.statLifeMax2=1000;
                NPC source;int before,humanLife,secondLife;
                if(!club){for(int x=66;x<=75;x++)for(int y=50;y<60;y++){water.Add(Tuple.Create(x,y,Main.tile[x,y]));Main.tile[x,y]=new Tile();Main.tile[x,y].liquid=255;}
                source=Spawn(461,120);source.velocity=Vector2.Zero;source.UpdateNPC(Slot);source.UpdateNPC(Slot);
                Require(source.wet && source.ai[3]==-.10101f && source.width==34 && source.height==24,"actual liquid collision then native wet AI produces persistent34x24 before serialization");
                publish(Slot);geometryNpc=((NPC[])Get(host,"observedNpcs"))[Slot];var wet=geometryNpc;
                Require(wet.width==34 && wet.height==24 && wet.position==source.position && wet.Center==source.Center,"real23 product decode restores current wet size without re-moving the source's received top-left");
                publish(Slot);Require(ReferenceEquals(geometryNpc,((NPC[])Get(host,"observedNpcs"))[Slot]) && wet.position==source.position && wet.Center==source.Center,"same wet23 is geometry-idempotent");
                first.position=new Vector2(wet.Right.X-1,wet.Top.Y+1-first.height);first.velocity=Vector2.Zero;first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);before=first.statLife;humanLife=human.statLife;secondLife=second.statLife;
                NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);human.Update(human.whoAmI);first.Update(first.whoAmI);second.Update(second.whoAmI);
                var legacy=new Rectangle((int)wet.position.X,(int)wet.position.Y,18,40);
                Require(ReferenceEquals(Get(a,"Target"),wet) && (Vector2)Get(a,"Aim")==wet.Center+wet.netOffset && geometryRays>0 && geometryRayPoint==wet.Center,"actual basic aim and reachability ray consume the decoded current-size center");
                Require(!first.Hitbox.Intersects(legacy) && first.statLife<before && human.statLife==humanLife && second.statLife==secondLife && ReferenceEquals(Main.npc[Slot],source),"native Player.Update contact actually hurts at the wet-width edge missed by defaults while human/other role health and original table stay intact");
                Console.WriteLine("NPC GEOMETRY wet native="+source.Hitbox+" actualContact="+before+"->"+first.statLife+" basic="+Get(a,"Aim")+" rays="+geometryRays);
                first.position=new Vector2(850,918);source.position=new Vector2(1240,880);source.velocity=Vector2.Zero;source.UpdateNPC(Slot);source.UpdateNPC(Slot);Require(!source.wet && source.ai[3]!=-.10101f && source.width==18 && source.height==40,"native dry exit restores the source's normal dimensions");
                publish(Slot);Require(ReferenceEquals(geometryNpc,((NPC[])Get(host,"observedNpcs"))[Slot]) && geometryNpc.width==18 && geometryNpc.height==40 && geometryNpc.position==source.position,"same identity real23 revokes wet size without a second center shift");
                return;}
                human.position=new Vector2(1000,918);first.position=new Vector2(1000,700);second.position=new Vector2(1250,918);source=Spawn(576,121);source.position=new Vector2(1100,960-source.height);source.velocity=Vector2.Zero;bool entered=false;
                human.position=new Vector2(source.Center.X-90-human.width/2,918);
                for(int step=0;step<44;step++){NativeClubStep(source);entered|=source.ai[0]==43 && source.ai[1]==0;if(entered && source.ai[0]==26)break;}
                Console.WriteLine("NPC CLUB source="+source.Hitbox+" target="+source.target+" ai="+String.Join(",",source.ai)+" frame="+source.frame.Y+" entered="+entered+" human="+human.position+" velocity="+source.velocity);
                Require(entered && source.ai[0]==26 && source.ai[1]==0 && source.frame.Y==15,"unmodified native Ogre AI enters its44-action melee and FindFrame reaches the real contact15 phase");publish(Slot);geometryNpc=((NPC[])Get(host,"observedNpcs"))[Slot];
                Require(geometryNpc.frame.Y==source.frame.Y,"current native contact frame is prepared from the actual synchronized legal attack clock");
                var victim=new Rectangle((int)source.Center.X-70,(int)source.Center.Y-5,first.width,first.height);int setter=0;float multiplier=1;var expected=source.Hitbox;NPC.GetMeleeCollisionData(victim,Slot,ref setter,ref multiplier,ref expected);
                Require(!source.Hitbox.Intersects(victim) && expected.Intersects(victim) && setter==2 && multiplier==1.75f,"original GetMeleeCollisionData itself identifies a real extended-only Ogre club contact");
                var previous=(NPC[])Call(host,"EnterNpcObservation");try{int actualSetter=0;float actualMultiplier=1;var actual=geometryNpc.Hitbox;NPC.GetMeleeCollisionData(victim,Slot,ref actualSetter,ref actualMultiplier,ref actual);Require(actual==expected && actualSetter==setter && actualMultiplier==multiplier,"native contact reads the prepared shared frame and yields the same extension/multiplier/immunity slot");}finally{Call(host,"LeaveNpcObservation",(object)previous);}
                first.position=new Vector2(victim.X,victim.Y);first.velocity=Vector2.Zero;first.immune=false;first.immuneTime=0;Array.Clear(first.hurtCooldowns,0,first.hurtCooldowns.Length);before=first.statLife;humanLife=human.statLife;secondLife=second.statLife;
                NativeQuickItemChecks.BeginWorldStep();Call(host,"Update",0UL);first.Update(first.whoAmI);Require(first.statLife<before && human.statLife==humanLife && second.statLife==secondLife && ReferenceEquals(Main.npc[Slot],source),"actual ContactThenGrapple/Update_NPCCollision/Hurt reaches the Ogre club extension and restores original human table");
                long stamp=(long)Call(host,"NpcSampleTick",geometryNpc);for(int step=1;step<=16;step++){NativeQuickItemChecks.BeginWorldStep();NativeClubStep(source);Call(host,"AdvanceNpcObservation");Require(geometryNpc.frame.Y==source.frame.Y && (long)Call(host,"NpcSampleTick",geometryNpc)==stamp,"no23 necessary contact phase progresses with actual world actions instead of freezing club contact");}
                Require(geometryNpc.frame.Y>=19,"Ogre shared contact window actually ends without another23");Console.WriteLine("NPC GEOMETRY Ogre club nativeHurt="+before+"->"+first.statLife+" no23Steps=16 finalFrame="+geometryNpc.frame.Y);
            }
            finally
            {
                geometryBody=null;geometryNpc=null;geometryDepth=0;patch.UnpatchAll(patch.Id);foreach(var tile in water)Main.tile[tile.Item1,tile.Item2]=tile.Item3;Main.npc[Slot]=old;Call(host,"ClearWorldSources");for(int i=0;i<players.Length;i++){players[i].position=positions[i];players[i].velocity=velocities[i];}
                first.inventory[0]=items[0];first.inventory[54]=items[1];first.selectedItemState.Select(selected);first.statLife=life;first.statLifeMax=max;first.statLifeMax2=max2;first.immune=immune;first.immuneTime=immuneTime;Array.Copy(cooldowns,first.hurtCooldowns,cooldowns.Length);Call(host,"SetBehavior",pa.Id,ba);Call(host,"SetBehavior",pb.Id,bb);Set(a,"Behavior",brains[0]);Set(b,"Behavior",brains[1]);foreach(var actor in new[]{a,b}){Set(actor,"Target",null);Set(actor,"HasAim",false);}
            }
        }
    }
}
