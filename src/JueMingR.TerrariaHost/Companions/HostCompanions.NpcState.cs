using System;
using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class NpcState
        {
            internal NPC Body;internal int Type,NetId,Width,Height,LifeMax,Advances;internal byte Generation;
            internal float Scale,KnockBackResist,BattleCounter;internal bool AwakePrepared;
            internal Actor Source,SnapshotSource;internal long Snapshot;internal bool SnapshotComplete;
            internal float Link0,Link1,Link3;
            internal int RootSlot=-1,PreviousSlot=-1;internal bool DependenciesHeld,RootRefreshIssued;
            internal NPC Root,Previous;internal byte RootGeneration,PreviousGeneration;
            internal bool RootBlocked,BeforeRootBlock,WallDependency;
            internal bool PhaseReady,PhysicsKnown;internal float PhaseToken,ExplosionClock;internal int PhaseTarget=-1;
            internal bool BattleReady,ArmedHistory;internal int BattlePhase,ArmedDamage;internal float BattleSample;
        }
        private NpcState[] npcStates;
        private static readonly int[] ogreContactDurations={4,4,8,2,4,4,4,2,8,4};
        private NpcState ObservationState(NPC npc)
        {
            if(npcStates==null)npcStates=new NpcState[Main.maxNPCs];
            var state=npcStates[npc.whoAmI];
            if(state==null || !ReferenceEquals(state.Body,npc) || state.Type!=npc.type || state.NetId!=npc.netID || state.Generation!=npc.generation)
            {
                if(state!=null)ReleaseNpcDependencies(state);
                // Capture the decoder's own defaults, including difficulty and
                // netID scale. Never multiply an already prepared body again.
                state=new NpcState{Body=npc,Type=npc.type,NetId=npc.netID,Generation=npc.generation,Width=npc.width,Height=npc.height,LifeMax=npc.lifeMax,Scale=npc.scale,KnockBackResist=npc.knockBackResist};npcStates[npc.whoAmI]=state;
            }
            return state;
        }
        private static bool LocalNpcPhase(NPC npc)
        {return npc.type==471 || npc.aiStyle==17 || npc.aiStyle==80 || npc.aiStyle==85 || npc.aiStyle==87 || npc.aiStyle==88 || npc.aiStyle==90 || npc.aiStyle==91 || npc.aiStyle==107 || npc.aiStyle==126;}
        private static float NpcPhaseToken(NPC npc)
        {
            if(npc.type==471)return npc.ai[3]<0?-1:npc.ai[3]==1?1:0;
            if(npc.aiStyle==107)return (npc.type==572 || npc.type==573) && npc.ai[1]==2?2:npc.ai[0]>0?1:0;
            return npc.ai[0];
        }
        private bool PrepareNpcObservationState(NPC npc,bool decoded,Actor source=null,Vector2? rawAnchor=null)
        {
            var anchor=NPCID.Sets.SyncAnchor[npc.type];var anchorPoint=rawAnchor??npc.position+npc.Size*anchor;
            var state=ObservationState(npc);if(decoded){state.Advances=0;state.BattleCounter=npc.ai[2];}else state.Advances++;
            if(state.RootBlocked){npc.dontTakeDamage=state.BeforeRootBlock;state.RootBlocked=false;}
            if(decoded)
            {
                bool sourceChanged=!ReferenceEquals(state.Source,source);
                if(sourceChanged)state.ArmedHistory=false;
                if(npc.type==482)
                {
                    int phase=npc.ai[2]<0?-1:1;
                    bool restart=npc.ai[2]>=0 && npc.ai[2]<state.BattleSample;
                    if(sourceChanged || state.BattlePhase!=phase || restart)state.BattleReady=false;
                    state.BattlePhase=phase;state.BattleSample=npc.ai[2];
                }
                float token=NpcPhaseToken(npc);bool targetRelevant=npc.aiStyle==87 || npc.aiStyle==107 || npc.aiStyle==126;
                bool targetChanged=targetRelevant && state.PhaseTarget!=npc.target;
                if(LocalNpcPhase(npc) && (sourceChanged || state.PhaseToken!=token || targetChanged))
                {state.PhaseReady=false;if(sourceChanged || targetChanged || npc.aiStyle!=126)state.PhysicsKnown=false;}
                // A still-dormant statue has never executed the jump which
                // establishes pass-through. This is the native initial form,
                // not proof that an arbitrary fresh awake embedded body is
                // clear. Same-instance sleep->awake can retain this history.
                if(npc.aiStyle==126 && npc.ai[0]==0 && !state.AwakePrepared && !npc.noTileCollide)state.PhysicsKnown=true;
                state.PhaseToken=token;state.PhaseTarget=npc.target;
                if(npc.type==378)state.ExplosionClock=npc.ai[2];
                else if(npc.type==572 || npc.type==573)state.ExplosionClock=npc.ai[0];
                else state.ExplosionClock=npc.ai[1];
                state.Source=source;var flow=Flow(source);
                bool changed=state.Link0!=npc.ai[0] || state.Link1!=npc.ai[1] || state.Link3!=npc.ai[3];
                if(flow.Snapshot){state.Snapshot=flow.Completed;state.SnapshotSource=source;state.SnapshotComplete=false;}
                else if(source!=null && source.InitialNpc.Count>0){state.Snapshot=-1;state.SnapshotSource=source;state.SnapshotComplete=true;}
                else if(changed)state.SnapshotComplete=false;
                state.Link0=npc.ai[0];state.Link1=npc.ai[1];state.Link3=npc.ai[3];
            }
            bool local=LocalNpcPhase(npc);
            if(!decoded || !local)PrepareNpcObservationPhysics(npc);
            int width=state.Width,height=state.Height;float scale=state.Scale;
            if(npc.type==461){width=npc.ai[3]==-.10101f?34:18;height=npc.ai[3]==-.10101f?24:40;}
            else if(npc.type==417)height=npc.ai[0]==6f?32:52;
            else if(npc.type==388 && npc.ai[0]==3f || (npc.type==574 || npc.type==575) && npc.ai[0]==3f)width=height=192;
            if(npc.aiStyle==1 && NPCID.Sets.SlimeCanContainItems[npc.type])
            {
                float item=npc.ai[1];bool ore=item==364 || item==1104 || item==365 || item==1105 || item==366 || item==1106;
                if(ore || item==174)
                {
                    scale=state.Scale*1.2f;float factor=ore?scale:1.2f;width=(int)(state.Width*factor);height=(int)(state.Height*factor);
                    // These are the same persistent item-form combat fields,
                    // not a second slime action or another life mutation.
                    npc.damage=ore?npc.defDamage*3:npc.defDamage+20;npc.defense=npc.defDefense+(ore?30:14);
                    npc.lifeMax=state.LifeMax*(ore?3:2);npc.knockBackResist=ore?0:state.KnockBackResist/3f;
                    if(ore){npc.lavaImmune=true;npc.buffImmune[70]=npc.buffImmune[397]=npc.buffImmune[395]=npc.buffImmune[20]=false;}
                }
            }
            // 23 already contains the source's post-transformation position.
            // In particular, 461/417 must not shift its center/bottom twice.
            // The walker predicate reads its PRE-expansion body. Preserve that
            // current shape until the accepted explosion action below expands
            // it; all other forms still derive absolute defaults dimensions.
            if(decoded || (npc.type!=572 && npc.type!=573) || npc.ai[1]!=2f){npc.width=width;npc.height=height;}npc.scale=scale;
            // AI87's raw phase does not identify whether its publishing action
            // has already established the next action's flags/knockback. Keep
            // this instance's current local fields on23. Its first REAL NPC
            // world action prepares the pure inputs; only after that action
            // succeeds may combat borrow them. Same-phase23 does not restart
            // the local phase, and raw sample/revision are never relabelled.
            bool physicalKnown=state.PhysicsKnown;
            if(!decoded || !local)physicalKnown=PrepareRemainingNpcState(npc,state,decoded);
            PrepareNpcExplosion(npc,state,decoded);
            //23's coordinate is already the source's post-shape anchor. During
            // local shape progression preserve that same native anchor once;
            // never replay source center/bottom shifts on a decoded position.
            if(anchor!=Vector2.Zero)npc.position=anchorPoint-npc.Size*anchor;
            if(npc.type>=454 && npc.type<=459 || npc.type>=621 && npc.type<=623)
            {
                // These AI_006 families establish hit eligibility BEFORE their
                // local spawn fade decreases alpha.23 does not carry alpha;
                // retain the native receiver's local phase, not primary state.
                npc.dontTakeDamage=npc.alpha>0;
                int previous=NpcLink(npc.ai[1],npc.whoAmI);var before=previous>=0?npcStates[previous]?.Body:null;
                if(!decoded && (npc.type==454 || npc.type==621 || before!=null && before.active && before.alpha<85))npc.alpha=Math.Max(0,npc.alpha-42);
            }
            PrepareNpcBattleState(npc,state,decoded);
            PrepareNpcContactFrame(npc,state.Advances);
            PrepareNpcRoot(npc,state,decoded);
            if(decoded && !local)state.PhysicsKnown=physicalKnown;
            return physicalKnown;
        }
        private static void PrepareNpcBattleState(NPC npc,NpcState state,bool decoded)
        {
            if(npc.aiStyle==87)
            {
                if(!decoded)
                {
                    float phase=npc.ai[0];bool aerial=phase==4f || phase==4.1f || phase==5f || phase==6f || phase==2f && npc.velocity.Y!=0f;
                    npc.knockBackResist=aerial?0f:npc.GetKnockbackMultiplier_ScaledByDifficulty(.2f);
                    npc.dontTakeDamage=phase==3f;npc.reflectsProjectiles=phase==3f && Main.expertMode;
                }
            }
            else if(npc.type==378 && npc.ai[1]==5f)
            {
                // The entry publication sets the explosion timer BEFORE its
                // first expansion. Hit immunity starts with that expansion,
                // together with the shape/velocity preparation above.
                npc.dontTakeDamage=npc.ai[2]!=10f || state.Advances>0;
            }
            else if(npc.type==371 && npc.ai[0]==1f)npc.dontTakeDamage=true;
            else if(npc.type==482)
            {
                // Positive0..10 omits whether the preceding action was a
                // defense exit. Do not invent an invulnerability flag. Keep
                // this instance's local field; an unestablished phase is
                // excluded from outgoing hits until a real NPC action writes
                // its inputs. Contact remains active at received geometry.
                if(decoded)
                {
                    if(npc.ai[2]<0 || npc.ai[2]>10){npc.dontTakeDamage=npc.ai[2]>-120 && npc.ai[2]<0;state.BattleReady=true;}
                }
                else if(state.BattleCounter<0f)
                {npc.dontTakeDamage=true;state.BattleCounter+=1f;if(Math.Abs(npc.velocity.Y)>1f)state.BattleCounter+=10f;}
                else npc.dontTakeDamage=false;
            }
            else if(npc.type==690)
            {
                // The server wake packet is emitted after ai0 becomes1 but
                // BEFORE that action clears immunity. The first local awake
                // phase owns the clear, not a guessed old primary object.
                if(npc.ai[0]==0f)state.AwakePrepared=false;
                else if(!decoded)state.AwakePrepared=true;
                npc.immortal=npc.dontTakeDamage=!state.AwakePrepared;
            }
            else if(npc.type>=430 && npc.type<=436 || npc.type==494 || npc.type==495 || npc.type==591)
            {
                // ai2=1 is the entry action and still uses base damage. The
                // current stable swing uses1.5. A fresh terminal raw0 is not
                // uniquely qualified by23: retain that publication limitation
                // in evidence rather than claim exact server contact there.
                bool swing=decoded?npc.ai[2]>1f:state.BattleCounter!=0f;
                if(decoded && npc.ai[2]==0f && state.ArmedHistory)npc.damage=state.ArmedDamage;
                else npc.damage=(int)(npc.defDamage*(swing?1.5:1.0));
                if(!decoded || npc.ai[2]!=0f){state.ArmedDamage=npc.damage;state.ArmedHistory=true;}
                if(!decoded && swing)
                {state.BattleCounter++;if(state.BattleCounter>=20f || npc.velocity.Y!=0f || Main.IsItDay() && npc.position.Y<Main.worldSurface*16.0)state.BattleCounter=0f;}
                // This same necessary contact clock is read directly by native
                // GetMeleeCollisionData for the Armed extension/multiplier.
                // Advance the accepted attack only, never decide a new one.
                // A later23 overwrites ai2 from the real source; the revision
                // remains advanced and cannot masquerade as a fresh sample.
                if(!decoded)npc.ai[2]=state.BattleCounter;
            }
            else if(npc.type==419)
            {
                bool reflect;
                // The supported native producer enters cooldown at60 only
                // from its reflecting action. Positive actions then decrease
                // it; there is no native61 producer. Keep that CURRENT exit
                // publication true/3, then let the next real action clear it.
                if(decoded)reflect=npc.ai[2]<-7f || npc.ai[2]==60f;
                else
                {
                    float phase=state.BattleCounter;reflect=phase<-6f;
                    if(phase>0f)state.BattleCounter--;
                    else if(phase<0f){state.BattleCounter--;if(reflect && npc.velocity.Y==0f)state.BattleCounter=60f;}
                }
                npc.reflectsProjectiles=reflect;npc.takenDamageMultiplier=reflect?3f:1f;
            }
        }
        private static void PrepareNpcExplosion(NPC npc,NpcState state,bool decoded)
        {
            bool bomb=npc.type==378 && npc.ai[1]==5;
            bool bubble=npc.type==371 && npc.ai[0]==1;
            bool walker=(npc.type==572 || npc.type==573) && npc.ai[1]==2;
            bool immediate=npc.type==388 && npc.ai[0]==3 || (npc.type==574 || npc.type==575) && npc.ai[0]==3;
            if(!npc.active || !bomb && !bubble && !walker && !immediate)return;
            float clock=state.ExplosionClock+(bomb || bubble?-state.Advances:state.Advances);
            if(!decoded && (bomb?clock<=1:bubble?clock<=0:clock>=3))
            {
                // This ends ONLY the accepted local contact window. No loot,
                // HitEffect, Strike, packet or server-death acknowledgement.
                npc.active=false;npc.life=0;return;
            }
            if(walker)
            {
                if(clock>0)
                {if(!decoded && state.ExplosionClock==0 && state.Advances==1)npc.position+=npc.Size*.5f-new Vector2(96);npc.width=npc.height=192;}
                else npc.damage=npc.defDamage;
            }
            if((walker && clock>0) || immediate)
            {npc.damage=npc.GetAttackDamage_ScaledByDifficulty(80f);npc.alpha=255;if(!decoded)npc.velocity=Vector2.Zero;}
            if(!decoded){if(bomb)npc.ai[2]=clock;else if(walker)npc.ai[0]=clock;else npc.ai[1]=clock;}
        }
        private static void PrepareNpcContactFrame(NPC npc,int advances)
        {
            // These two native contact consumers read animation INDEX, not a
            // texture coordinate. Their legal attack clocks are synchronized;
            // advance only this necessary local phase, without AI/FindFrame
            // target decisions, sounds, attacks or random effects.
            if(npc.type==576 || npc.type==577)
            {
                int remaining=(int)npc.ai[0]-advances;npc.frame.Y=0;
                if(npc.ai[1]!=0 || npc.ai[0]!=(int)npc.ai[0] || npc.ai[0]<1 || npc.ai[0]>43 || remaining<=0)return;
                int elapsed=44-remaining,index=11;
                foreach(int duration in ogreContactDurations){if(elapsed<duration)break;elapsed-=duration;index++;}
                npc.frame.Y=Math.Min(20,index);npc.frameCounter=elapsed;
            }
            else if(npc.type==668)
            {
                float clock=npc.ai[1]+advances;npc.frame.Y=0;
                // Only frame15 adds a contact rectangle. Other frames use the
                // body's native cropped rectangle; state4 excludes extension.
                if(npc.ai[0]==1f && clock>=36 && clock<40 || npc.ai[0]==2f && clock>=4 && clock<8)npc.frame.Y=15;
            }
        }
    }
}
