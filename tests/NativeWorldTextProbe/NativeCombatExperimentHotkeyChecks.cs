using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Combat;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using static NativeWorldTextProbe.NativeInformationChecks;
namespace NativeWorldTextProbe
{
    internal static class NativeCombatExperimentHotkeyChecks
    {
        internal static void Reveal(object context)
        {
            var shell=Get(context,"Shell");var keys=Get(shell,"hotkeys");var registry=(HotkeyRegistry)Get(keys,"Registry");var owner=Get(context,"CombatExperiments");
            var ids=new[]{"combat.hard-lock","combat.fast-fire","combat.projectile-through-wall"};
            var tokens=new[]{"J","K","L"};var rows=ids.Select((id,i)=>new KeyValuePair<string,string>(id,tokens[i])).ToArray();
            var saved=new Storage(new HotkeyDocument(rows));
            var clash=new Storage(new HotkeyDocument(new[]{rows[0],new KeyValuePair<string,string>("combat.auto-click","J")}));
            using(var bindings=new HotkeyBindings(registry,saved))
            using(var conflicts=new HotkeyBindings(registry,clash))
            {
                NativeQuickItemChecks.Until(()=>{bindings.Poll();conflicts.Poll();return bindings.Loaded && conflicts.Loaded;});
                foreach(string id in ids)Require(registry.Find(id)==null && bindings.Get(id)==null,"locked hidden disk binding owns no registered/effective/conflict slot: "+id);
                HotkeyChord chord;string reason;Require(HotkeyChord.TryParse("J",out chord,out reason),"parse real chord");
                Require(!conflicts.Protected && conflicts.Get("combat.auto-click")?.Text=="J" && bindings.Validate("combat.auto-click",chord)==null,"locked orphan preserves ordinary saved binding and its editable chord slot");
                long revision=registry.Revision;for(int i=0;i<32;i++)Call(keys,"PollHiddenRegistration");
                Require(registry.Revision==revision && saved.Writes==0 && clash.Writes==0,"stable lock does not publish or save");
                NativeHiddenEntryChecks.RevealForComposition(context);Call(keys,"PollHiddenRegistration");
                for(int i=0;i<ids.Length;i++)Require(registry.Find(ids[i])!=null && bindings.Get(ids[i])?.Text==tokens[i],"unlock restores exact preserved hidden binding: "+ids[i]);
                Require(conflicts.Get(ids[0])==null && conflicts.Get("combat.auto-click")==null && conflicts.Protected,"unlock applies normal true explicit-conflict protection");
                revision=registry.Revision;for(int i=0;i<32;i++)Call(keys,"PollHiddenRegistration");
                Require(registry.Revision==revision && saved.Writes==0 && clash.Writes==0,"stable unlock does not re-register or rewrite either document");
                var settings=(CombatExperimentSettings)Get(owner,"Settings");NativeQuickItemChecks.Until(()=>{Call(owner,"Poll");return settings.Ready;});int original=settings.Value.EnabledMask;
                try
                {
                    var input=new HotkeyInput();var sample=new bool[HotkeyChord.KeyCount];
                    for(int field=0;field<3;field++)
                    {
                        Array.Clear(sample,0,sample.Length);input.Update(sample,true);sample[74+field]=true;input.Update(sample,true);
                        int before=settings.Value.EnabledMask;bindings.Dispatch(input,HotkeyContext.Gameplay,true,ids[field]);
                        Require(settings.Busy && settings.Value.EnabledMask==before,"actual registered command waits for its reliable save");
                        NativeQuickItemChecks.Until(()=>{Call(owner,"Poll");return !settings.Busy;});
                        Require(settings.CompletionSucceeded && settings.Value.EnabledMask==(before^(1<<field)),"registered physical edge commits the same UI preference owner: "+ids[field]);
                        long command=settings.AcceptedCommandId;input.Update(sample,true);bindings.Dispatch(input,HotkeyContext.Gameplay,true,ids[field]);
                        Require(settings.AcceptedCommandId==command,"same held edge cannot resubmit hidden action");
                    }
                }
                finally
                {
                    if(settings.Ready && settings.Value.EnabledMask!=original){settings.Set(new CombatExperimentOptions(original));NativeQuickItemChecks.Until(()=>{Call(owner,"Poll");return !settings.Busy;});}
                }
            }
            Console.WriteLine("PASS hidden action registry: locked disk entries inert, ordinary slot preserved, five-click unlock restores exact IDs, real conflict protected, registered dispatch commits each switch.");
        }
        private sealed class Storage:IPreferenceStorage
        {
            private readonly byte[] bytes;internal int Writes;
            internal Storage(HotkeyDocument document){bytes=HotkeyDocument.Encode(document);}
            public PreferenceReadResult Read(){return new PreferenceReadResult(PreferenceReadStatus.Loaded,bytes,"saved",null);}
            public PreferenceWriteResult Write(string identity,byte[] contents){Writes++;throw new InvalidOperationException("Registration must not write bindings");}
            public void Dispose(){}
        }
    }
}
