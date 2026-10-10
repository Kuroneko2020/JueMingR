using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // A cluster is one bounded causal family, not six independent directions
    // solved without preceding damage or native projectile-slot ordering.
    // The unborn six-fragment fan uses the middle of the original speed/fuse
    // ranges and a fixed rotation. It never consumes the live random stream.
    internal static class HostClusterAttack
    {
        internal static bool Handles(int type){return type==776 || type==777 || type==778 || type==803;}
        private sealed class Member
        {
            internal Projectile Shot;
            internal HostExplosiveAttack.State State;
            internal HostAttackObstacles Receivers;
            internal int Slot;
        }
        internal static Vector2 FragmentVelocity(float phase,float ring,float speed)
        {return (phase+ring*MathHelper.TwoPi).ToRotationVector2()*speed-Vector2.UnitY;}
        private static bool Born(Projectile shot,PredictionTerrain terrain,Vector2 center,Vector2 velocity,out HostExplosiveAttack.State state)
        {
            state=new HostExplosiveAttack.State(shot,center,velocity);bool wet;byte kind;
            if(!terrain.ProjectileWet(center.X,center.Y,shot.width,shot.height,out wet,out kind) || wet && kind==3)return false;
            state.Wet=!shot.ignoreWater && wet;state.Honey=state.Wet && kind==2;return true;
        }
        private static bool Fragments(Player player,Member parent,PredictionTerrain terrain,HostProjectileBirthSlots slots,List<Member> family)
        {
            int type=HostExplosiveAttack.ChildType(parent.Shot.type);if(type==0 || parent.Shot.owner!=Main.myPlayer)return true;
            for(float ring=0;ring<1;ring+=1f/6f)
            {
                int slot;if(!slots.TryAllocate(out slot))return false;
                var shot=new Projectile();shot.SetDefaults(type);shot.owner=player.whoAmI;shot.damage=parent.Shot.damage/2;
                HostExplosiveAttack.State state;if(!Born(shot,terrain,parent.State.Center,FragmentVelocity(0,ring,5),out state))return false;
                state.Left=45;
                // Cluster death does not call CopyLocalNPCImmunityTo. Only
                // owner/static history is shared with preceding family hits.
                family.Add(new Member{Shot=shot,State=state,Slot=slot,Receivers=parent.Receivers.ForkForSpawn(shot)});
            }
            return true;
        }
        private static AttackContact Damage(Member member,NpcTrajectory timeline,Vector2 aim,int tick,int outer,int sub,bool burst,AttackConfidence confidence,out bool known)
        {
            known=true;if(member.Shot.damage<=0)return null;
            int width=burst?HostExplosiveAttack.BurstSide(member.Shot.type,member.Shot.width):member.Shot.width,height=burst?width:member.Shot.height;
            int hits=member.Receivers.OtherHits;
            bool pass=member.Receivers.PassShapeOuter(2,tick,outer,member.State.Center.X,member.State.Center.Y,width,height,burst && member.Receivers.Remaining<=0);
            if(!pass && member.Receivers.Remaining!=0){known=false;return null;}
            if(pass && member.Receivers.SelectedAllows(outer))
            {
                var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,member.State.Center.X,member.State.Center.Y,width,height,confidence);
                if(contact!=null)return contact;
            }
            if(!burst && member.Receivers.OtherHits>hits && member.State.Left>3)member.State.Left=3;
            return null;
        }
        private static AttackContact Family(Player player,List<Member> family,PredictionTerrain terrain,HostProjectileBirthSlots slots,NpcTrajectory timeline,Vector2 aim,int age,int delay,HostExplosiveAttack.Candidate[] targets,AttackConfidence confidence)
        {
            int limit=Math.Min(120,timeline.Count-age-1);
            for(int outer=1;outer<=limit;outer++)
            {
                int last=-1,visited=0;
                while(visited++<7)
                {
                    Member member=null;
                    foreach(var candidate in family)if(candidate.State.Alive && candidate.Slot>last && (member==null || candidate.Slot<member.Slot))member=candidate;
                    if(member==null)break;last=member.Slot;
                    for(int sub=0;sub<=member.Shot.extraUpdates && member.State.Alive;sub++)
                    {
                        Vector2? target;int tick=age+outer;
                        if(!HostExplosiveAttack.Target(member.Shot,member.State,timeline,tick,terrain,targets,out target) || !HostExplosiveAttack.Step(member.Shot,terrain,ref member.State,target))return null;
                        bool known;AttackContact contact;
                        if(member.State.Alive)
                        {
                            contact=Damage(member,timeline,aim,tick,delay+outer,sub,false,confidence,out known);if(!known || contact!=null)return contact;
                            if(member.Receivers.Remaining==0 || member.State.Left<=0){member.State.Alive=false;member.State.Burst=true;}
                        }
                        if(member.State.Burst)
                        {
                            contact=Damage(member,timeline,aim,tick,delay+outer,sub,true,confidence,out known);if(!known || contact!=null)return contact;
                            // Native allocates children while the dying slot
                            // remains active; releasing it earlier changes the
                            // very first child update and therefore hit time.
                            if(!Fragments(player,member,terrain,slots,family))return null;
                            member.State.Burst=false;
                        }
                        if(!member.State.Alive)slots.Release(member.Slot);
                    }
                }
            }
            return null;
        }
        internal static AttackContact ReplayFamily(Player player,Projectile[] shots,NpcTrajectory timeline,PredictionTerrain terrain,Vector2 aim,int age,bool beforeNpc)
        {
            if(shots.Length==0 || shots.Length>6)return null;
            // The native causal unit is one parent or its six actual children.
            // A caller cannot combine unrelated parents into this bound.
            if(shots.Length>1)foreach(var shot in shots)if(Handles(shot.type))return null;
            var root=new HostAttackObstacles(player,shots[0],timeline,0,beforeNpc,!beforeNpc);root.BeginCandidate();var slots=root.CaptureBirthSlots().Copy();var family=new List<Member>(7);bool unborn=false;
            foreach(var shot in shots)
            {
                if(!shot.active || !Handles(shot.type) && shot.type!=779 && shot.type!=862)return null;
                family.Add(new Member{Shot=shot,State=new HostExplosiveAttack.State(shot),Slot=shot.whoAmI,Receivers=root.ForkForSpawn(shot)});
                unborn|=Handles(shot.type);
            }
            return Family(player,family,terrain,slots,timeline,aim,age,0,HostExplosiveAttack.CaptureTargets(shots[0],root),unborn?AttackConfidence.Representative:AttackConfidence.Conditional);
        }
        internal static AttackContact Solve(Player player,Projectile sample,NpcTrajectory timeline,PredictionTerrain terrain,HostAttackObstacles obstacles,Vector2 origin,Vector2 velocity,Vector2 aim,int age,int delay,HostExplosiveAttack.Candidate[] targets)
        {
            var slots=obstacles.CaptureBirthSlots().Copy();int slot;if(!slots.TryAllocate(out slot))return null;
            var shot=new Projectile();shot.SetDefaults(sample.type);shot.owner=player.whoAmI;shot.damage=player.GetWeaponDamage(player.HeldItem);
            HostExplosiveAttack.State state;if(!Born(shot,terrain,origin,velocity,out state))return null;
            var family=new List<Member>(7){new Member{Shot=shot,State=state,Slot=slot,Receivers=obstacles.ForkForSpawn(shot)}};
            return Family(player,family,terrain,slots,timeline,aim,age,delay,targets,AttackConfidence.Representative);
        }
    }
}
