using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class ReportHooks : IDisposable
    {
        private static ReportHooks current;
        private readonly CombatReports reports;
        private readonly Harmony harmony=new Harmony("JueMingR.Combat.Reports");
        private readonly List<MethodBase> targets=new List<MethodBase>();
        private AccessTools.FieldRef<NetworkText,byte> mode;
        private AccessTools.FieldRef<NetworkText,string> text;
        private AccessTools.FieldRef<NetworkText,NetworkText[]> args;
        internal bool Ready {get;private set;}
        internal Exception Error {get;private set;}
        internal ReportHooks(CombatReports reports)
        {
            this.reports=reports;
            try
            {
                if(current!=null)throw new InvalidOperationException("Report hooks already installed.");current=this;
                mode=AccessTools.FieldRefAccess<NetworkText,byte>("_mode");text=AccessTools.FieldRefAccess<NetworkText,string>("_text");args=AccessTools.FieldRefAccess<NetworkText,NetworkText[]>("_substitutions");
                Patch(AccessTools.DeclaredMethod(typeof(NPCDamageTracker),"StopTracking"),nameof(Stopped));
                Patch(AccessTools.DeclaredMethod(typeof(MessageBuffer),"GetData",new[]{typeof(int),typeof(int),typeof(int).MakeByRefType()}),nameof(Message));
                Patch(AccessTools.DeclaredMethod(typeof(ChatHelper),"DisplayMessage",new[]{typeof(NetworkText),typeof(Color),typeof(byte)}),nameof(Displayed));
                Ready=true;
            }
            catch(Exception e){Error=e;Dispose();}
        }
        private void Patch(MethodInfo method,string after)
        {if(method==null)throw new MissingMethodException("Report observation ABI");targets.Add(method);harmony.Patch(method,postfix:new HarmonyMethod(typeof(ReportHooks),after));}
        private void Failed(Exception error){Error=error;Ready=false;reports.Reset();}
        private static void Stopped(bool __runOriginal)
        {var self=current;if(!__runOriginal || self==null || !self.Ready)return;try{self.reports.RecentChanged();}catch(Exception e){self.Failed(e);}}
        private static void Message(MessageBuffer __instance,int __0,int __1,int __2,bool __runOriginal)
        {var self=current;if(!__runOriginal || self==null || !self.Ready)return;try{self.reports.Message(__instance,__0,__1,__2);}catch(Exception e){self.Failed(e);}}
        private static void Displayed(NetworkText __0,byte __2,bool __runOriginal)
        {
            var self=current;if(!__runOriginal || self==null || !self.Ready || __2!=byte.MaxValue || __0==null)return;
            try
            {
            if(!self.reports.CanObserve)return;
            if(self.mode(__0)!=1)return;var values=self.args(__0);
            if(values==null || values.Length==0 || values[0]==null)return;var title=values[0];
            if(self.mode(title)!=2 || !string.Equals(self.text(title),"BossDamageCommand.Title",StringComparison.Ordinal))return;
            var names=self.args(title);if(names==null || names.Length!=1 || names[0]==null || self.mode(names[0])!=2)return;
            self.reports.Observed();
            }
            catch(Exception e){self.Failed(e);}
        }
        public void Dispose()
        {Ready=false;foreach(var target in targets){try{harmony.Unpatch(target,HarmonyPatchType.All,harmony.Id);}catch(Exception e){if(Error==null)Error=e;}}targets.Clear();if(ReferenceEquals(current,this))current=null;}
    }
}
