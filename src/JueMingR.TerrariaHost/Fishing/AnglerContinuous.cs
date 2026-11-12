using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Fishing
{
    // Owns only the completion of one real vanilla submission. Rewards, fish,
    // today's list and saving remain owned by vanilla and PlayerRename.
    internal sealed class AnglerContinuous : IDisposable
    {
        internal sealed class Submission
        {
            internal Player Player;
            internal NPC Angler;
            internal object World;
            internal int NetworkMode;
            internal long Generation,ManualRevision;
            internal string Name;
            internal int Talk,Quest,Completed;
        }
        private readonly HostFishing host;
        private readonly Harmony harmony=new Harmony("JueMingR.AnglerContinuous");
        private static AnglerContinuous current;
        private Submission pending;
        internal bool Available {get;private set;}
        internal bool Pending {get{return pending!=null;}}
        internal string Status {get;private set;}
        internal AnglerContinuous(HostFishing host)
        {
            this.host=host;
            try
            {
                var target=typeof(Main).GetMethod("NPCChatText_DoAnglerQuest",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
                if(target==null || target.ReturnType!=typeof(void) || target.GetMethodBody()==null)throw new MissingMethodException("native angler submission");
                current=this;
                harmony.Patch(target,prefix:new HarmonyMethod(typeof(AnglerContinuous),nameof(Before)),finalizer:new HarmonyMethod(typeof(AnglerContinuous),nameof(After)));
                Available=true;
            }
            catch(Exception){Dispose();Status="渔夫任务连续提交暂不可用，原版交任务不受影响。";host.Report(Status);}
        }
        private bool Enabled
        {get{return Available && host.Available && host.Settings.Loaded && !host.Settings.Protected && host.Settings.Value.AnglerContinuous && !(host.Settings.Busy && host.Settings.Requested!=null && !host.Settings.Requested.AnglerContinuous);}}
        private bool Valid(Submission value)
        {
            var p=host.Player;
            return Enabled && p!=null && p.active && !p.dead && !Main.gameMenu && ReferenceEquals(p,Main.LocalPlayer) && ReferenceEquals(p,value.Player) &&
                host.Tools.Runtime.Generation==value.Generation && ReferenceEquals(Main.ActiveWorldFileData,value.World) && Main.netMode==value.NetworkMode && p.name==value.Name && host.Rename.ManualRevision==value.ManualRevision && p.talkNPC==value.Talk &&
                value.Talk>=0 && value.Talk<Main.npc.Length && ReferenceEquals(Main.npc[value.Talk],value.Angler) && value.Angler.active && value.Angler.type==NPCID.Angler && Main.anglerQuest==value.Quest;
        }
        private Submission Capture()
        {
            if(!Enabled)return null;
            var p=host.Player;
            if(p==null || p.talkNPC<0 || p.talkNPC>=Main.npc.Length || Main.anglerQuest<0 || Main.anglerQuest>=Main.anglerQuestItemNetIDs.Length)return null;
            var value=new Submission{Player=p,Angler=Main.npc[p.talkNPC],Generation=host.Tools.Runtime.Generation,ManualRevision=host.Rename.ManualRevision,
                World=Main.ActiveWorldFileData,NetworkMode=Main.netMode,Name=p.name,Talk=p.talkNPC,Quest=Main.anglerQuest,Completed=p.anglerQuestsFinished};
            return value.Angler!=null && Valid(value)?value:null;
        }
        private static void Before(out Submission __state)
        {
            __state=null;
            try{__state=current?.Capture();}
            catch(Exception)
            {
                if(current!=null){current.Clear();current.Available=false;current.Status="渔夫任务连续提交暂不可用，原版交任务不受影响。";current.host.Report(current.Status);}
            }
        }
        private static Exception After(Exception __exception,bool __runOriginal,Submission __state)
        {
            // GetAnglerReward is too early: the OUTER method still records the
            // old name and sends vanilla 75. Only its normal completion owns an
            // event; a failed/skipped native method never becomes a retry.
            if(__exception==null && __runOriginal && __state!=null && current!=null)
            {
                try{current.Completed(__state);}
                catch(Exception){current.Clear();current.host.Report("任务已提交，自动改名未完成；未自动重试。");}
            }
            return __exception;
        }
        private void Completed(Submission value)
        {
            if(!Valid(value) || value.Player.anglerQuestsFinished!=value.Completed+1 || !Main.anglerQuestFinished)return;
            pending=value;Update();
        }
        internal void Update()
        {
            var value=pending;if(value==null)return;
            if(!Valid(value)){Clear();return;}
            var result=host.Rename.RenameAutomatic();
            if(result.Deferred && !result.Started)return;
            // Native Rename may already have changed the name despite a failed
            // readback or SSC skip. Consume once before interpreting that result;
            // an unknown or partial save must never increment the suffix again.
            pending=null;
            if(result.RuntimeNameApplied && ReferenceEquals(host.Player,value.Player) && host.Tools.Runtime.Generation==value.Generation && value.Player.talkNPC==value.Talk &&
                ReferenceEquals(Main.npc[value.Talk],value.Angler) && value.Angler.active)
            {
                Main.npcChatCornerItem=0;Main.npcChatText=Lang.AnglerQuestChat(false);
            }
            if(result.Message!=null)
            {
                host.Report(result.Message);
                var feedback=host.Tools.Feedback;
                feedback?.Show("fishing.angler-continuous",result.Message,false,()=>ReferenceEquals(host.Player,value.Player),feedback.Capture());
            }
            // PlayerRename emitted one message4 after the native 75. Neither
            // serialization nor this local eligibility refresh is server ACK.
        }
        internal void Clear(){pending=null;}
        public void Dispose()
        {
            Clear();Available=false;
            foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);
            if(ReferenceEquals(current,this))current=null;
        }
    }
}
