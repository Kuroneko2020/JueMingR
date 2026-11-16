using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class PredictedNpc
        {internal NPC Body;internal byte Generation;}
        private static readonly MethodInfo cloneNpc=AccessTools.Method(typeof(object),"MemberwiseClone");
        private static readonly FieldInfo[] npcFields=typeof(NPC).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static readonly FieldInfo pendingList=AccessTools.Field(typeof(NPC),"pendingDamages");
        internal bool PredictionDead(Actor a,NPC real)
        {
            PredictedNpc view;if(a.NpcViews.TryGetValue(real.whoAmI,out view) && view.Generation==real.generation && (!view.Body.active || view.Body.life<=0))return true;
            if(real.realLife>=0 && real.realLife!=real.whoAmI){var root=Main.npc[real.realLife];return a.NpcViews.TryGetValue(root.whoAmI,out view) && view.Generation==root.generation && (!view.Body.active || view.Body.life<=0);}return false;
        }
        internal NPC PredictionNpc(Actor a,NPC real)
        {
            PredictedNpc view;
            if(!a.NpcViews.TryGetValue(real.whoAmI,out view) || view.Generation!=real.generation || view.Body.type!=real.type)
            {
                var body=(NPC)cloneNpc.Invoke(real,null);
                // Only confirmed hit candidates acquire a view. Prediction
                // arrays are private; immune is deliberately shared with the
                // team's observation: its slots identify players and the one
                // world-stage observation update ages it for the whole team.
                foreach(var field in npcFields)if(field.FieldType.IsArray && field.Name!="immune")
                {var array=field.GetValue(real) as Array;if(array!=null)field.SetValue(body,array.Clone());}
                view=new PredictedNpc{Body=body,Generation=real.generation};a.NpcViews[real.whoAmI]=view;
            }
            else if(!ReferenceEquals(view.Body,real))
            {
                int life=view.Body.life;bool active=view.Body.active;
                bool phase=PendingPhase(a,real.whoAmI,real.generation);
                var ai=phase?(float[])view.Body.ai.Clone():null;bool invulnerable=view.Body.dontTakeDamage;
                // Fresh shared observations own geometry, defense, debuffs and
                // native per-player immunity aging. Only unconfirmed health is
                // private; a view is not another NPC simulation/history.
                foreach(var field in npcFields)
                {
                    var value=field.GetValue(real);var array=value as Array;
                    if(array==null || field.Name=="immune"){field.SetValue(view.Body,value);continue;}
                    var destination=field.GetValue(view.Body) as Array;
                    if(destination==null || destination.Length!=array.Length)field.SetValue(view.Body,array.Clone());else Array.Copy(array,destination,array.Length);
                }
                view.Body.life=life;view.Body.active=active;
                if(phase){Array.Copy(ai,view.Body.ai,ai.Length);view.Body.dontTakeDamage=invulnerable;}
            }
            return view.Body;
        }
        internal void RegisterPendingStrike(Actor a,object pending)
        {
            var list=(IList)pendingList.GetValue(null);
            // realLife recursion runs this postfix twice for one native object.
            // Membership/removal, not a guessed request tuple, admits it once.
            if(!list.Contains(pending))return;list.Remove(pending);a.PendingStrikes.Enqueue(pending);
        }
        private static bool PendingPhase(Actor a,int slot,byte generation)
        {
            foreach(var pending in a.PendingStrikes)
            {var type=pending.GetType();if((short)AccessTools.Field(type,"index").GetValue(pending)==slot && (byte)AccessTools.Field(type,"generation").GetValue(pending)==generation && (bool)AccessTools.Field(type,"phaseChange").GetValue(pending))return true;}
            return false;
        }
        private void StrikeAcknowledged(Actor a)
        {
            // 162 acknowledges processing of this connection's request only.
            // It never applies damage or implies that the server accepted it.
            if(a.PendingStrikes.Count!=0)a.PendingStrikes.Dequeue();
        }
        private void ObservePredictedNpc(Actor a,byte[] packet,int start,int length)
        {
            if(length<23)return;int slot=packet[start+1];PredictedNpc view;if(!a.NpcViews.TryGetValue(slot,out view))return;
            if(view.Generation!=packet[start+2]){a.NpcViews.Remove(slot);return;}
            using(var s=new MemoryStream(packet,start+3,length-3,false))using(var r=new BinaryReader(s))
            {
                s.Position+=18;byte flags=r.ReadByte(),flags2=r.ReadByte();
                var ai=new float[NPC.maxAI];for(int i=0;i<NPC.maxAI;i++)if((flags&(1<<(i+2)))!=0)ai[i]=r.ReadSingle();
                if(r.ReadInt16()!=view.Body.netID){a.NpcViews.Remove(slot);return;}if((flags2&1)!=0)r.ReadByte();if((flags2&4)!=0)r.ReadSingle();
                int life=view.Body.lifeMax;
                if((flags&128)==0){int size=r.ReadByte();life=size==2?r.ReadInt16():size==4?r.ReadInt32():r.ReadSByte();}
                int predicted=0;bool phase=false;
                foreach(var pending in a.PendingStrikes)
                {
                    var type=pending.GetType();if((short)AccessTools.Field(type,"index").GetValue(pending)!=slot || (byte)AccessTools.Field(type,"generation").GetValue(pending)!=view.Generation)continue;
                    predicted+=(int)AccessTools.Field(type,"damage").GetValue(pending);phase|=(bool)AccessTools.Field(type,"phaseChange").GetValue(pending);
                }
                if(life<=0){view.Body.life=life;view.Body.active=false;}
                else if(!phase){view.Body.life=life-predicted;view.Body.active=true;Array.Copy(ai,view.Body.ai,ai.Length);}
            }
        }
    }
}
