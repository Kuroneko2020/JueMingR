using System;
using HarmonyLib;
using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        // One bounded native ABI table for the whole team, never one world per
        // actor. Only currently relevant server observations become active.
        // The human table remains the original primary-stream native table.
        private NPC[] observedNpcs;
        private uint[] observedTicks;private long[] observedRevisions,advancedRevisions;private long nextObservationRevision;
        private uint observationStep=UInt32.MaxValue;
        private int observationDecodeSlot=-1;
        internal bool ReadingObservation {get;private set;}
        internal bool DecodingObservationSlot(int slot)
        {return ReadingObservation && observationDecodeSlot==slot && ReferenceEquals(Main.npc,observedNpcs);}
        private readonly MessageBuffer observationDecoder=new MessageBuffer{whoAmI=256};
        private delegate void NpcGravity(NPC npc,out float maxFallSpeed);
        private static readonly Action<NPC> resetNpcBuffs=AccessTools.MethodDelegate<Action<NPC>>(AccessTools.Method(typeof(NPC),"UpdateNPC_BuffFlagsReset")),clearNpcBuffs=AccessTools.MethodDelegate<Action<NPC>>(AccessTools.Method(typeof(NPC),"UpdateNPC_BuffClearExpiredBuffs")),moveNpc=AccessTools.MethodDelegate<Action<NPC>>(AccessTools.Method(typeof(NPC),"UpdateCollision"));
        private static readonly NpcGravity npcGravity=AccessTools.MethodDelegate<NpcGravity>(AccessTools.Method(typeof(NPC),"UpdateNPC_UpdateGravity"));
        private static readonly System.Reflection.FieldInfo gravityField=AccessTools.Field(typeof(NPC),"gravity");
        private void EnsureNpcObservation()
        {
            if(observedNpcs!=null)return;
            observedNpcs=new NPC[Main.maxNPCs];for(int i=0;i<observedNpcs.Length;i++)observedNpcs[i]=new NPC{whoAmI=i,active=false};
            observedTicks=new uint[observedNpcs.Length];observedRevisions=new long[observedNpcs.Length];advancedRevisions=new long[observedNpcs.Length];
        }
        internal long NpcObservationRevision(NPC npc){return Main.netMode==1 && observedNpcs!=null && ReferenceEquals(observedNpcs[npc.whoAmI],npc)?observedRevisions[npc.whoAmI]:0;}
        internal bool FreshNpcObservation(NPC npc){return Main.netMode!=1 || observedNpcs!=null && ReferenceEquals(observedNpcs[npc.whoAmI],npc) && observedTicks[npc.whoAmI]+1==Main.GameUpdateCount && advancedRevisions[npc.whoAmI]!=observedRevisions[npc.whoAmI];}
        internal long NpcSampleTick(NPC npc){return Main.netMode==1?observedTicks[npc.whoAmI]:(long)Main.GameUpdateCount-1;}
        internal NPC[] EnterNpcObservation()
        {
            if(Main.netMode!=1 || !HasActors)return null;
            EnsureNpcObservation();if(ReferenceEquals(Main.npc,observedNpcs))return null;
            var old=Main.npc;Main.npc=observedNpcs;return old;
        }
        internal void LeaveNpcObservation(NPC[] previous){if(previous!=null)Main.npc=previous;}
        private bool NeededNpc(Microsoft.Xna.Framework.Vector2 point)
        {
            foreach(var actor in actors.Values)if(actor.Ready && !actor.Closing && Microsoft.Xna.Framework.Vector2.DistanceSquared(actor.Body.Center,point)<2200*2200)return true;
            return false;
        }
        private void RetireNpcInterest(int slot,bool leftInterest=true)
        {
            ReleaseNpcDependencies(slot);
            observedNpcs[slot].active=false;
            // Interest eviction is not a consumed native inactive/death state.
            // A later ready actor needs a usable initial baseline again.
            WorldLease lease;if(npcSources.TryGetValue(slot,out lease))
            {
                if(lease.NpcBaseline || lease.InterestRetired){lease.InterestRetired=true;if(leftInterest)lease.InterestInside=false;}
                lease.NpcBaseline=false;lease.AwaitBaseline=true;
            }
        }
        private void RememberFilteredNpc(int slot,Vector2 point)
        {
            WorldLease lease;if(!npcSources.TryGetValue(slot,out lease)){lease=new WorldLease();npcSources.Add(slot,lease);}
            // Metadata only: do not decode a distant NPC or fabricate its
            // revision/position just to make the existing-body edge fire.
            // At most one actual filtered candidate exists per native slot.
            lease.FilteredCandidate=true;lease.FilteredPoint=point;
            // Keep never-consumed and genuinely retired baselines distinct:
            // the first later usable23 may still seed the original Ready path.
            lease.InterestInside=false;lease.AwaitBaseline=true;
        }
        private static void PrepareNpcObservationPhysics(NPC npc)
        {
            // 23 carries AI state, not these AI_002-established physics flags.
            // Prepare the decoded instance for ALL shared consumers, including
            // basic aim and native contact, without running another AI action.
            switch(npc.aiStyle)
            {
                case 2:
                    npc.noGravity=true;
                    if(npc.type==170 || npc.type==171 || npc.type==180)npc.noTileCollide=npc.ai[1]!=0f;
                    break;
                case 14:case 44:case 49:npc.noGravity=true;break;
                case 23:case 57:case 61:case 86:npc.noGravity=npc.noTileCollide=true;break;
                case 17:npc.noGravity=npc.ai[0]!=0f;break;
                case 50:npc.noTileCollide=npc.type!=261;break;
                case 80:npc.noTileCollide=npc.ai[0]==2f;break;
                case 85:npc.noTileCollide=npc.ai[0]==2f;break;
                case 91:npc.noGravity=npc.ai[0]!=-1f;npc.noTileCollide=npc.ai[0]==2f;break;
            }
            if(npc.type==461 || npc.type==586)npc.noGravity=npc.ai[3]==-.10101f;
            else if(npc.type==469)npc.noGravity=npc.ai[2]==1f;
            else if(npc.type==471)
            {
                npc.noGravity=npc.noTileCollide=npc.ai[3]<0f;
                npc.defense=npc.ai[3]<0f?(int)(npc.defDefense*1.1):npc.defDefense+(npc.ai[3]==1f?10:0);
            }
        }
        internal void AdvanceNpcObservation()
        {
            if(Main.netMode!=1 || observedNpcs==null || observationStep==Main.GameUpdateCount)return;observationStep=Main.GameUpdateCount;
            //23 supplies a native receiver's input, not a full AI snapshot.
            // Its defaults/history and necessary local contact/movement phases
            // can enter another swing/jump, followed by ONE physics tail.
            // Never run a second complete NPC.Update/AI, DOT/loot or network
            // producer. Advance this team table once per actual world step.
            var previous=Main.npc;Main.npc=observedNpcs;bool reading=ReadingObservation;ReadingObservation=true;
            bool stair=Collision.stair,fall=Collision.stairFall,honey=Collision.honey,shimmer=Collision.shimmer,sloping=Collision.sloping,mine=Collision.landMine,up=Collision.up,down=Collision.down;
            float previousGravity=(float)gravityField.GetValue(null);
            try{foreach(var npc in observedNpcs)
            {
                if(!npc.active){CheckNpcInterestReentry(npc);continue;}
                bool interested=NeededNpc(npc.Center);if(!interested)ReleaseNpcDependencies(npc.whoAmI);if(!interested && !RequiredNpcSlot(npc.whoAmI)){RetireNpcInterest(npc.whoAmI);continue;}
                advancedRevisions[npc.whoAmI]=observedRevisions[npc.whoAmI];
                if(NPC.offSetDelayTime>0 || Main.multiplayerNPCSmoothingRange<=0 || NPCID.Sets.NoMultiplayerSmoothingByType[npc.type] || NPCID.Sets.NoMultiplayerSmoothingByAI[npc.aiStyle] || npc.townNPC && npc.ai[0]==25f)npc.netOffset=Vector2.Zero;
                else if(Terraria.Testing.DebugOptions.FakeNetOffset!=Vector2.Zero)npc.netOffset=Terraria.Testing.DebugOptions.FakeNetOffset;
                else if(npc.netOffset!=Vector2.Zero)
                {
                    float distance=npc.netOffset.Length(),range=Main.multiplayerNPCSmoothingRange;
                    if(distance>range){npc.netOffset=npc.netOffset/distance*range;distance=range;}
                    float speed=2f+distance/range*2f;npc.netOffset-=Vector2.Normalize(npc.netOffset)*speed;
                    if(npc.netOffset.Length()<speed)npc.netOffset=Vector2.Zero;
                    if(npc.townNPC && Vector2.Distance(npc.position,new Vector2(npc.homeTileX*16+8-npc.width/2,npc.homeTileY*16-npc.height-.1f))<1f)npc.netOffset=Vector2.Zero;
                }
                for(int i=0;i<npc.immune.Length;i++)if(npc.immune[i]>0)npc.immune[i]--;
                resetNpcBuffs(npc);npc.UpdateNPC_BuffSetFlags();clearNpcBuffs(npc);
                int x=(int)(npc.Center.X/16),y=(int)(npc.Center.Y/16);
                if(x<2 || y<2 || x>=Main.maxTilesX-2 || y>=Main.maxTilesY-2 || Main.tile[x,y]==null)continue;
                if(Main.sectionManager!=null && !Main.sectionManager.TileLoaded(x,y))continue;
                bool local=LocalNpcPhase(npc);if(local)npcStates[npc.whoAmI].PhaseReady=false;
                // AI126 uses the gravity value established BEFORE its local
                // movement action as well as the common tail. Compute that
                // producer once; do not add the physical tail a second time.
                float maximum=0;bool actionGravity=npc.aiStyle==126;if(actionGravity)npcGravity(npc,out maximum);
                bool physicalKnown=PrepareNpcObservationState(npc,false);
                if(!npc.active)continue;
                if(!actionGravity)npcGravity(npc,out maximum);if(!npc.noGravity)npc.velocity.Y=Math.Min(maximum,npc.velocity.Y+(float)gravityField.GetValue(null));
                if(npc.noTileCollide){npc.oldPosition=npc.position;npc.position+=npc.velocity;}else moveNpc(npc);
                FinishNpcLocalContact(npc);
                // Native UpdateNPC retires this input AFTER its action and
                // collision. AI003 must consume it once, not keep suppressing
                // every later unstuck attempt between network samples.
                if(npc.aiStyle==3)npc.justHit=false;
                npcStates[npc.whoAmI].PhysicsKnown=physicalKnown;
                if(local)npcStates[npc.whoAmI].PhaseReady=physicalKnown && NpcBaselineCurrent(npc.whoAmI);
                if(npc.type==482)npcStates[npc.whoAmI].BattleReady=NpcBaselineCurrent(npc.whoAmI);
            }}
            finally{Main.npc=previous;ReadingObservation=reading;gravityField.SetValue(null,previousGravity);Collision.stair=stair;Collision.stairFall=fall;Collision.honey=honey;Collision.shimmer=shimmer;Collision.sloping=sloping;Collision.landMine=mine;Collision.up=up;Collision.down=down;}
        }
        internal void DecodeNpcObservation(Actor source,byte[] packet,int start,int length)
        {DecodeNpcObservationAt(source,packet,start,length,Main.GameUpdateCount);}
        private void DecodeNpcObservationAt(Actor source,byte[] packet,int start,int length,uint sampleTick)
        {
            if(!HasActors || ReadingObservation || length<3)return;
            EnsureNpcObservation();int slot=packet[start]==54?BitConverter.ToInt16(packet,start+1):packet[start+1];if(slot<0 || slot>=observedNpcs.Length)return;
            if(packet[start]==23 && length>=19 && !RequiredNpcSlot(slot) && !RequiredNpcPacket(packet,start,length))
            {var point=new Vector2(BitConverter.ToSingle(packet,start+3),BitConverter.ToSingle(packet,start+7));if(!NeededNpc(point)){RetireNpcInterest(slot,false);RememberFilteredNpc(slot,point);return;}}
            var previous=Main.npc;var decoding=Decoding;int previousSlot=observationDecodeSlot;Main.npc=observedNpcs;ReadingObservation=true;Decoding=source;observationDecodeSlot=packet[start]==23?slot:-1;
            try
            {
                Buffer.BlockCopy(packet,start,observationDecoder.readBuffer,0,length);observationDecoder.ResetReader();int id;observationDecoder.GetData(0,length,out id);
                if(id==23)
                {
                    AcceptObservedNpcHitGeneration(slot,observedNpcs[slot].generation,previous);
                    PrepareNpcObservationState(observedNpcs[slot],true,source,new Vector2(BitConverter.ToSingle(packet,start+3),BitConverter.ToSingle(packet,start+7)));
                    // The native full-health bit means the CURRENT form's
                    // maximum, not the pre-AI SetDefaults maximum. Numeric
                    // injured health is already decoded and is never scaled.
                    if(length>21 && (packet[start+21]&128)!=0)observedNpcs[slot].life=observedNpcs[slot].lifeMax;
                    observedTicks[slot]=sampleTick;observedRevisions[slot]=++nextObservationRevision;
                    WorldLease lease;if(npcSources.TryGetValue(slot,out lease) && ReferenceEquals(lease.Source,source) && !lease.Waiting)
                    {lease.NpcBaseline=true;lease.AwaitBaseline=lease.InterestRetired=lease.InactiveBaseline=lease.RequiredBaseline=lease.FilteredCandidate=false;lease.InterestInside=true;}
                }
            }
            finally{observationDecodeSlot=previousSlot;Decoding=decoding;ReadingObservation=false;Main.npc=previous;}
        }
        internal void PrimaryNpcApplied(byte[] packet,int start,int length)
        {
            if(ReadingObservation || !HasActors || length<3)return;int id=packet[start];
            if(id==23 || id==28)
            {if(RouteNpc(null,packet,start,length))DecodeNpcObservation(null,packet,start,length);}
            else if(id==54 && observedNpcs!=null)
            {
                int slot=BitConverter.ToInt16(packet,start+1);
                WorldLease lease;
                if(slot>=0 && slot<observedNpcs.Length && observedNpcs[slot].active && observedNpcs[slot].generation==Main.npc[slot].generation && npcSources.TryGetValue(slot,out lease) && lease.NpcBaseline && lease.Source==null && !lease.Waiting && !lease.AwaitBaseline)DecodeNpcObservation(null,packet,start,length);
            }
        }
    }
}
