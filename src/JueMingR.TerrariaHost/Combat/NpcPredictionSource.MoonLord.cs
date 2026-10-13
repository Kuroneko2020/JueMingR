using System;
using System.Runtime.CompilerServices;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        private readonly bool[] moonRawRequired=new bool[NpcPredictionCache.Capacity];
        private readonly PredictionMoonLeech[] moonLeechValues=new PredictionMoonLeech[Main.maxProjectiles];
        private PredictionMoonLeeches moonLeeches;
        private static readonly int[][] MoonSamplePhases={new[]{0,1,2,0,3},new[]{1,0,3,0,2},new[]{3,0,2,3,1}};
        private static readonly int[][] MoonSampleClocks={new[]{50,70,330,60,90},new[]{70,50,90,60,330},new[]{180,30,435,180,375}};
        private static bool MoonLordMotor(NPC n)
        {return n!=null && (n.type==398 && n.aiStyle==77 || n.type==397 && n.aiStyle==78 || n.type==396 && n.aiStyle==79 || MoonLeechMotor(n));}
        private static bool MoonLeechMotor(NPC n){return n!=null && n.type==401 && n.aiStyle==82;}
        private static NPC MoonRaw(int slot)
        {return slot>=0 && slot<Main.maxNPCs && slot<Main.npc.Length?Main.npc[slot]:null;}
        private static NpcIdentity MoonIdentity(int slot,long session)
        {var n=MoonRaw(slot);return n==null?default(NpcIdentity):CombatSelection.Identity(n,session);}

        // Read supplies immutable current facts; PositionObservation may retain
        // its existing numbered relation but cannot add a Life>0 owner gate.
        private static void CaptureMoonLordSample(NPC n,ref NpcMotionState state,long session)
        {
            if(!MoonLordMotor(n))return;
            state.MoonExternalEffectAt=state.MoonGlobalRetireAt=int.MaxValue;
            if(MoonLeechMotor(n))
            {
                var head=Finite(n.ai[0])?MoonRaw((int)Math.Abs(n.ai[0])-1):null;
                state.PositionRelation=5;state.PositionOwnerCaptured=head!=null;
                state.PositionOwnerValid=head!=null && head.active && head.type==396;
                state.PositionOwner=state.PositionOwnerValid?CombatSelection.Identity(head,session):default(NpcIdentity);
                state.MoonPart0=head==null?default(NpcIdentity):MoonIdentity((int)head.ai[3],session);
                // AI1 is a bit-cast key, including legal NaN float bit patterns.
                // Never numeric-cast it, normalize it, or borrow a new slot.
                ProjectileKey key=(ProjectileKey)n.ai[1];state.MoonLeechKey=(int)key;
                Projectile anchor;bool valid=key.TryGetActive(out anchor) && anchor.type==456;
                state.MoonLeechAnchorCaptured=true;state.MoonLeechAnchorValid=valid;
                state.MoonLeechAnchorX=valid?anchor.Center.X:0;state.MoonLeechAnchorY=valid?anchor.Center.Y:0;
                return;
            }
            state.MoonHandApertureCaptured=n.type==397;state.MoonHandAperture=n.type==397?n.frameCounter:0;
            if(n.type==398)
            {
                state.MoonPart0=MoonIdentity((int)n.localAI[0],session);
                state.MoonPart1=MoonIdentity((int)n.localAI[1],session);
                state.MoonPart2=MoonIdentity((int)n.localAI[2],session);
                return;
            }
            int ownerSlot=(int)n.ai[3];var owner=MoonRaw(ownerSlot);
            state.PositionRelation=n.type==396?3:4;state.PositionParameter=n.type==397?(n.ai[2]==0?-1:1):0;
            state.PositionOwnerCaptured=owner!=null;
            state.PositionOwnerValid=owner!=null && owner.active && owner.type==398;
            state.PositionOwner=state.PositionOwnerValid?CombatSelection.Identity(owner,session):default(NpcIdentity);
        }

        private void CaptureMoonLordDependencies(NPC actor,long session,ref int queued,ref bool captured,ref bool complete)
        {
            if(!captured){Array.Clear(moonRawRequired,0,moonRawRequired.Length);captured=true;}
            if(MoonLeechMotor(actor))
            {
                var head=Finite(actor.ai[0])?MoonRaw((int)Math.Abs(actor.ai[0])-1):null;
                if(head==null){complete=false;return;}
                if(!head.active || head.type!=396)return; // Native known retirement.
                if(!QueuePlantMotor(head.whoAmI,ref queued))complete=false;
                int rawCoreSlot=(int)head.ai[3];var rawCore=MoonRaw(rawCoreSlot);
                if(rawCore==null){complete=false;return;}
                moonRawRequired[rawCoreSlot]=true;
                if(rawCore.active && rawCore.type==398)
                {if(!QueuePlantMotor(rawCoreSlot,ref queued))complete=false;}
                else if(!visited[rawCoreSlot])
                {if(queued>=pending.Length)complete=false;else{visited[rawCoreSlot]=true;pending[queued++]=rawCoreSlot;}}
                // Raw core identity can alias/point outside the body family.
                // AI82 snapshots deficits of the first active sidehands before
                // healing; all eligible sidehands can become first after retire.
                for(int hand=0;hand<Main.maxNPCs && hand<Main.npc.Length;hand++)
                {var row=Main.npc[hand];if(row!=null && row.active && row.type==397 && row.ai[3]==rawCoreSlot && (row.ai[2]==0 || row.ai[2]==1) && !QueuePlantMotor(hand,ref queued))complete=false;}
                Projectile anchor;var key=(ProjectileKey)actor.ai[1];
                if(key.TryGetActive(out anchor) && anchor.type==456 && Finite(anchor.ai[0]))
                {
                    var anchorHead=MoonRaw((int)Math.Abs(anchor.ai[0])-1);
                    if(anchorHead==null)complete=false;
                    else if(anchorHead.active && anchorHead.type==396)
                    {if(!QueuePlantMotor(anchorHead.whoAmI,ref queued))complete=false;}
                    else moonRawRequired[anchorHead.whoAmI]=true;
                }
                return;
            }
            var core=actor;
            if(actor.type!=398)
            {
                core=MoonRaw((int)actor.ai[3]);
                // Known active/type-invalid owner is a native retirement. No
                // arbitrary replacement or unrelated boss motor is acquired.
                if(core==null){complete=false;return;}
                if(!core.active || core.type!=398)return;
            }
            if(!QueuePlantMotor(core.whoAmI,ref queued))complete=false;
            // Existing clots genuinely mutate this controller's life/aperture.
            // Acquire those actors, not all projectiles or unrelated NPC motors.
            for(int clotSlot=0;clotSlot<Main.maxNPCs && clotSlot<Main.npc.Length;clotSlot++)
            {
                var clot=Main.npc[clotSlot];if(clot==null || !clot.active || !MoonLeechMotor(clot) || !Finite(clot.ai[0]))continue;
                var head=MoonRaw((int)Math.Abs(clot.ai[0])-1);
                if(head!=null && head.active && head.type==396 && head.ai[3]==core.whoAmI && !QueuePlantMotor(clotSlot,ref queued))complete=false;
            }
            // Native intro creates fresh slots at clock60. Until that actual
            // birth, default localAI zeros are not existing part dependencies.
            if(core.localAI[3]==0 || core.ai[0]==-1 || core.ai[0]==2 || core.ai[0]==3)return;
            for(int part=0;part<3;part++)
            {
                int slot=(int)core.localAI[part];var raw=MoonRaw(slot);
                if(raw==null || slot>=moonRawRequired.Length){complete=false;continue;}
                moonRawRequired[slot]=true;
                int expected=part<2?397:396;
                bool motor=raw.active && (raw.type==expected || core.ai[0]==1);
                // Phase0 wrong-type rows are only native invalidity facts.
                // Phase1 teleport genuinely moves active raw foreign slots.
                if(motor){if(!QueuePlantMotor(slot,ref queued))complete=false;}
                else if(!visited[slot])
                {if(queued>=pending.Length)complete=false;else{visited[slot]=true;pending[queued++]=slot;}}
            }
        }

        private void FinishMoonLordDependencies(long session,ref int count,bool captured,bool complete)
        {
            if(!captured)return;
            // The ordinary pending loop deliberately skips inactive NPCs.
            // Phase0 and teleport read raw slots nevertheless, so preserve
            // those full inactive rows before sorting and role assignment.
            for(int slot=0;slot<moonRawRequired.Length;slot++)if(moonRawRequired[slot])
            {
                bool present=false;for(int i=0;i<count;i++)if(states[i].Identity.Slot==slot){present=true;break;}
                if(present)continue;var raw=MoonRaw(slot);
                if(raw==null || count>=states.Length){complete=false;continue;}
                states[count++]=Read(raw,session);visited[slot]=true;
            }
            bool effects=Main.npc.Length>=Main.maxNPCs && Main.projectile.Length>=Main.maxProjectiles;
            for(int i=0;i<count;i++)
            {
                var row=states[i];var native=MoonRaw(row.Identity.Slot);
                if(!motionSlots[row.Identity.Slot] || !MoonLordMotor(native))continue;
                row.MoonRelationsCaptured=true;row.MoonRelationsComplete=complete;row.MoonEffectsCaptured=effects;
                // Fully captured existing AI82 actors are now advanced in the
                // same slot loop. Only unacquired actual clots retain a deadline.
                row.MoonExternalEffectAt=FirstUnmodeledMoonLeechConsumer(native,sampleHorizon);
                row.MoonExternalEffectAt=Math.Min(row.MoonExternalEffectAt,FirstMoonAttachedConsumer(row,native,sampleHorizon));
                row.MoonGlobalRetireAt=FirstMoonForeignRetire(native,sampleHorizon);
                states[i]=row;
            }
        }

        private PredictionMoonLeeches CaptureMoonLeechInputs(long session,int count)
        {
            int used=0;
            int clearWriter;int clearAt=FirstMoonAnchorClear(sampleHorizon,out clearWriter);
            for(int row=0;row<count;row++)
            {
                if(!motionSlots[states[row].Identity.Slot] || !MoonLeechMotor(MoonRaw(states[row].Identity.Slot)))continue;
                Projectile q;var key=(ProjectileKey)states[row].MoonLeechKey;
                if(!key.TryGetActive(out q) || q.type!=456)continue;
                bool duplicate=false;for(int i=0;i<used;i++)if(moonLeechValues[i].Key==(int)q.key){duplicate=true;break;}
                if(duplicate)continue;
                int headSlot=Finite(q.ai[0])?(int)Math.Abs(q.ai[0])-1:-1,playerSlot=Finite(q.ai[1])?(int)q.ai[1]:-1;
                var head=MoonRaw(headSlot);var p=playerSlot>=0 && playerSlot<Main.player.Length?Main.player[playerSlot]:null;
                bool ownerActive=q.owner==255 || q.owner>=0 && q.owner<Main.player.Length && Main.player[q.owner]!=null && Main.player[q.owner].active;
                bool settings=q.aiStyle==85 && q.damage==0 && q.hostile && !q.friendly && !q.tileCollide && !q.minion && !q.sentry && q.extraUpdates>=0 && q.extraUpdates<=15 && q.owner>=0 && q.owner<=255 && Finite(q.ai[0]) && q.ai[0]!=0 && Finite(q.ai[1]);
                moonLeechValues[used++]=new PredictionMoonLeech(q,q.whoAmI,(int)q.key,q.owner,q.timeLeft,q.extraUpdates,q.penetrate,head!=null,head==null?default(NpcIdentity):CombatSelection.Identity(head,session),p!=null,p,playerSlot,p==null?default(MotionRect):new MotionRect(p.position.X,p.position.Y,p.width,p.height),p!=null && p.active,p!=null && p.dead,p!=null && p.creativeGodMode,ownerActive,new MotionRect(q.position.X,q.position.Y,q.width,q.height),q.velocity.X,q.velocity.Y,q.ai[0],q.localAI[0],q.localAI[1],settings,q.ignoreWater,q.wet,clearAt,clearWriter);
            }
            Array.Sort(moonLeechValues,0,used,MoonLeechSlotComparer.Instance);
            bool same=moonLeeches!=null && moonLeeches.Count==used && moonLeeches.Left==Main.leftWorld && moonLeeches.Right==Main.rightWorld && moonLeeches.Top==Main.topWorld && moonLeeches.Bottom==Main.bottomWorld;
            if(same)for(int i=0;i<used;i++)if(!moonLeechValues[i].Same(moonLeeches[i])){same=false;break;}
            if(!same)moonLeeches=new PredictionMoonLeeches(moonLeechValues,used,Main.leftWorld,Main.rightWorld,Main.topWorld,Main.bottomWorld);
            return moonLeeches;
        }
        private sealed class MoonLeechSlotComparer : System.Collections.Generic.IComparer<PredictionMoonLeech>
        {internal static readonly MoonLeechSlotComparer Instance=new MoonLeechSlotComparer();public int Compare(PredictionMoonLeech a,PredictionMoonLeech b){return a.Slot.CompareTo(b.Slot);}}
        private int FirstUnmodeledMoonLeechConsumer(NPC actor,int maximum)
        {
            // The original deadline algorithm is retained for actual clots
            // outside this closure; use its exact consumer-slot adjustment.
            int first=int.MaxValue;
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var clot=Main.npc[slot];if(clot==null || !clot.active || !MoonLeechMotor(clot) || motionSlots[slot])continue;
                var head=Finite(clot.ai[0])?MoonRaw((int)Math.Abs(clot.ai[0])-1):null;
                if(head==null || !head.active || head.type!=396 || Main.netMode==1)continue;
                int coreSlot=(int)head.ai[3];bool affects=actor.whoAmI==head.whoAmI || actor.whoAmI==coreSlot;
                if(!affects && actor.type==397 && actor.ai[3]==coreSlot)
                {
                    int left=-1,right=-1;for(int hand=0;hand<Main.maxNPCs && hand<Main.npc.Length;hand++)
                    {var p=Main.npc[hand];if(p==null || !p.active || p.type!=397 || p.ai[3]!=coreSlot)continue;if(left<0 && p.ai[2]==0)left=hand;if(right<0 && p.ai[2]==1)right=hand;}
                    affects=actor.whoAmI==left || actor.whoAmI==right;
                }
                if(!affects)continue;float clock=clot.ai[2];if(!Finite(clock)){first=1;continue;}
                for(int action=1;action<=maximum && action<first;action++)
                {clock=MoonStore(clock+1);if(clock>=90){first=Math.Min(first,action+(slot>actor.whoAmI?1:0));break;}}
            }
            return first;
        }

        private static int FirstMoonLeechConsumer(NPC actor,int maximum)
        {
            int first=int.MaxValue;
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var clot=Main.npc[slot];if(clot==null || !clot.active || clot.type!=401 || clot.aiStyle!=82)continue;
                var head=MoonRaw((int)Math.Abs(clot.ai[0])-1);
                if(head==null || !head.active || head.type!=396)continue;
                int coreSlot=(int)head.ai[3];bool affects=actor.whoAmI==head.whoAmI || actor.whoAmI==coreSlot;
                // AI82 heals only the FIRST live hand for each side, according
                // to the head's raw core slot, not the core's localAI array.
                if(!affects && actor.type==397 && actor.ai[3]==coreSlot)
                {
                    int left=-1,right=-1;for(int hand=0;hand<Main.maxNPCs && hand<Main.npc.Length;hand++)
                    {var p=Main.npc[hand];if(p==null || !p.active || p.type!=397 || p.ai[3]!=coreSlot)continue;if(left<0 && p.ai[2]==0)left=hand;if(right<0 && p.ai[2]==1)right=hand;if(left>=0 && right>=0)break;}
                    affects=actor.whoAmI==left || actor.whoAmI==right;
                }
                if(!affects || Main.netMode==1)continue;
                float clock=clot.ai[2];if(!Finite(clock)){first=1;continue;}
                for(int action=1;action<=maximum && action<first;action++)
                {
                    clock=MoonStore(clock+1);if(clock<90)continue;
                    // Heal occurs in the clot's NPC slot, unlike a projectile
                    // tail effect. An earlier consumer sees it this same frame.
                    first=Math.Min(first,action+(slot>actor.whoAmI?1:0));break;
                }
            }
            return first;
        }

        private static int FirstMoonAnchorClear(int maximum,out int writerSlot)
        {
            int first=int.MaxValue;writerSlot=-1;
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var core=Main.npc[slot];
                if(core==null || !core.active || core.type!=398 || core.aiStyle!=77 || core.localAI[3]==0 || core.ai[0]!=2 && core.ai[0]!=3)continue;
                float clock=core.ai[1],goal=core.ai[0]==2?60:40;
                // This exact finite field write clears all existing456.
                // Clocks already beyond, or fractionally skipping the mark,
                // do not imply another clear. No foreign AI is simulated.
                if(!Finite(clock))continue;
                for(int action=1;action<=maximum && action<=first;action++)
                {
                    clock=MoonStore(clock+1);
                    if(clock==goal){if(action<first || action==first && (writerSlot<0 || slot<writerSlot)){first=action;writerSlot=slot;}break;}
                    if(clock>goal)break;
                }
            }
            return first;
        }

        private int FirstMoonForeignRetire(NPC actor,int maximum)
        {
            if(actor.type==398 && actor.ai[0]==1)return int.MaxValue;
            int first=int.MaxValue;
            for(int slot=0;slot<Main.maxNPCs && slot<Main.npc.Length;slot++)
            {
                var core=Main.npc[slot];if(core==null || !core.active || core.type!=398 || core.aiStyle!=77 || motionSlots[slot])continue;
                if(actor.type==401)
                {
                    // Observed2/3 clearing is an exact captured event. A
                    // normal foreign core may enter withdrawal at its tail;
                    // bound only that still-unknown future key consumer.
                    if(core.localAI[3]!=0 && (core.ai[0]==0 || core.ai[0]==1))first=Math.Min(first,41+(slot>actor.whoAmI?1:0));
                    continue;
                }
                // A foreign core that is already withdrawing is a real global
                // writer. Normal phases can enter withdrawal only at this
                // action's tail, hence cannot clear parts before action61.
                // Record that earliest possible consumer instead of simulating
                // the foreign core's unrelated player/geometry prerequisites.
                int action=int.MaxValue;
                if(core.ai[0]==3)
                {
                    float clock=core.ai[1];if(!Finite(clock))action=1;
                    else for(int f=1;f<=maximum;f++){clock=MoonStore(clock+1);if(clock>=60){action=f;break;}}
                }
                else if(core.localAI[3]!=0 && (core.ai[0]==0 || core.ai[0]==1))action=61;
                if(action!=int.MaxValue)first=Math.Min(first,action+(slot>actor.whoAmI?1:0));
            }
            return first;
        }

        private static int FirstMoonAttachedConsumer(NpcMotionState sample,NPC actor,int maximum)
        {
            if(actor.type!=396 && actor.type!=397)return int.MaxValue;
            int first=int.MaxValue;
            for(int slot=0;slot<Main.maxProjectiles && slot<Main.projectile.Length;slot++)
            {
                var q=Main.projectile[slot];if(q==null || !q.active || q.owner!=Main.myPlayer || q.type<0 || q.type>=ProjectileID.Sets.IsAnNPCAttachedExplosive.Length || !ProjectileID.Sets.IsAnNPCAttachedExplosive[q.type] || !q.IsAttachedTo(actor))continue;
                // Locked set contains Daybreak636. Other configurations have
                // no certified lifetime/profile and stop at first consumer.
                if(q.type!=636 || q.extraUpdates<0 || q.extraUpdates>15 || !Finite(q.localAI[0]) || !Finite(q.velocity.X) || !Finite(q.velocity.Y))return 1;
                if(q.position.X<=Main.leftWorld || q.position.X+q.width>=Main.rightWorld || q.position.Y<=Main.topWorld || q.position.Y+q.height>=Main.bottomWorld)continue;
                var n=sample;float fuse=q.localAI[0];int remaining=q.timeLeft,updates=q.extraUpdates+1;
                for(int action=1;action<=maximum && action<first;action++)
                {
                    bool pop=actor.type==397?n.MoonHandAperture==19 && !n.Health.DontTakeDamage:n.L3==13 && !n.Health.DontTakeDamage;
                    if(pop){first=action;break;}
                    MoonSampleAperture(ref n);
                    // Native attached AI kills on its current closed aperture,
                    // 600-update fuse, expiry, penetration or inactive owner.
                    // We bound the first possible effect, without replaying
                    // 953's random newborn explosion or assigning its identity.
                    bool kill=n.Health.DontTakeDamage || q.owner<255 && (q.owner<0 || q.owner>=Main.player.Length || Main.player[q.owner]==null || !Main.player[q.owner].active);
                    for(int sub=0;sub<updates && !kill;sub++)
                    {fuse=MoonStore(fuse+1);kill=fuse>=60*5*updates;if(!kill){remaining--;kill=remaining<=0 || q.penetrate==0;}}
                    if(kill){first=action;break;}
                }
            }
            return first;
        }
        // Sampling projects only the exact aperture/loop clock, never a second
        // NPC world or motor. These rows mirror locked MoonLordAttacksArray;
        // the original scalar aperture oracle protects this sampler separately.
        private static void MoonSampleAperture(ref NpcMotionState n)
        {
            bool hand=n.EffectiveType==397;int row=hand?(n.A2==0?0:1):2;float time=0,duration=0;
            if(!hand || n.A0!=-2)
            {
                n.A1++;int phase=0,start=0;while(phase<5 && MoonSampleClocks[row][phase]+start<=n.A1){start+=MoonSampleClocks[row][phase++];}
                if(phase==5){phase=0;start=0;n.A1=0;}
                time=(int)n.A1-start;duration=MoonSampleClocks[row][phase];if(hand || n.A0>=0)n.A0=MoonSamplePhases[row][phase];
            }
            bool dead=n.A0==-2 || !hand && n.A0==-3;
            n.Health.DontTakeDamage=dead || (hand?n.MoonHandAperture>=21:n.L3>=15);
            int target=0;
            if(n.A0==0)target=hand?21:15;
            else if(n.A0==1 && !hand && time>=duration-15)target=15;
            else if(n.A0==2)
            {if(!hand)target=15;else if(time>=30 && time<210 || time>=282 && time<287)target=7;else if(time>=287 && time<292)target=14;else if(time>=292)target=21;}
            if(hand){if(target>n.MoonHandAperture)n.MoonHandAperture++;if(target<n.MoonHandAperture)n.MoonHandAperture--;n.MoonHandAperture=Math.Max(0,Math.Min(21,n.MoonHandAperture));}
            else{if(target>n.L3)n.L3++;if(target<n.L3)n.L3--;}
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float MoonStore(float value){return value;}
    }
}
