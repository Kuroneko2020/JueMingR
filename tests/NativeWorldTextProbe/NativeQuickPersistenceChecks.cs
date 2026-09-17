using System;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeQuickPersistenceChecks
    {
        internal static void Run(object context,QuickItemEntry entry)
        {
            object quick=Get(context,"QuickItems"),keys=Get(Get(context,"Shell"),"hotkeys");
            var original=(QuickItemSettings)Get(quick,"Settings");var bindings=(HotkeyBindings)Get(keys,"Bindings");var registry=(HotkeyRegistry)Get(keys,"Registry");
            object favorite=Get(context,"KeepFavorited");
            try
            {
                Set(favorite,"failed",true);
                Require(!(bool)Get(quick,"FavoriteControlsEnabled") && (bool)Get(quick,"ControlsEnabled"),"favorite capability failure does not disable quick controls");
                bool before=original.KeepFavorited;Call(quick,"ToggleFavorite");Require(original.KeepFavorited==before && !original.Busy,"failed favorite cannot report enabled");
                Set(favorite,"failed",false);Set(quick,"Available",false);
                Require((bool)Get(quick,"FavoriteControlsEnabled") && !(bool)Get(quick,"ControlsEnabled"),"quick capability failure does not disable healthy favorite");
            }
            finally{Set(favorite,"failed",false);Set(quick,"Available",true);Set(quick,"Message",null);}
            // Fault only the storage port. Actual Host publication, domain
            // worker and shared binding/conflict compilation remain production.
            var storage=new ControlledStore(QuickItemDocument.Encode(original.Current));
            using(var temporary=new QuickItemSettings(storage))
            {
                NativeQuickUseMatrix.Until(()=>{temporary.Poll();return temporary.Loaded;});
                Set(quick,"Settings",temporary);Set(quick,"published",-1L);Call(quick,"Poll");
                try
                {
                    string reason;storage.Fail=true;Require((bool)Call(quick,"Delete",entry.Id),"controlled domain deletion accepted");
                    NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(!temporary.CanExecute(entry.Id) && temporary.Current.Find(entry.Id)!=null,"failed domain deletion stays paused with persisted entry present");
                    HotkeyChord chord;HotkeyChord.TryParse("J",out chord,out reason);
                    Require(bindings.Validate("items.keep-favorited.toggle",chord)!=null,"failed domain deletion keeps its saved chord reserved for other actions");
                    Require(registry.Find(entry.ActionId)!=null && bindings.Get(entry.ActionId)!=null && !bindings.Protected,"paused persistent identity retains shared conflict reservation");
                    Require(!registry.Find(entry.ActionId).CanConfigure && bindings.Validate(entry.ActionId,chord)!=null,"paused identity cannot change its binding while domain save is unconfirmed");
                    storage.Fail=false;Require(temporary.TryChange(original.Current,entry.Id,out reason),"explicit recovery admitted");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(temporary.CanExecute(entry.Id) && bindings.Get(entry.ActionId)!=null && !bindings.Protected,"explicit recovery cannot create a duplicate binding or protect unrelated actions");
                    storage.Fail=true;Call(quick,"ToggleQuick");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(!temporary.Enabled && temporary.Current.Enabled,"failed quick-off remains suspended");
                    storage.Fail=false;Call(quick,"ToggleFavorite");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(temporary.KeepFavorited && !temporary.Enabled,"independent favorite save cannot revive a quick toggle suspended by failure");
                    Call(quick,"ToggleQuick");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(temporary.Enabled,"explicit quick enable can recover its own failed toggle");
                    storage.Fail=true;Call(quick,"ToggleFavorite");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    storage.Fail=false;Call(quick,"ToggleQuick");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(!temporary.KeepFavorited && !temporary.Enabled,"quick save cannot revive a favorite toggle suspended by failure");
                    Call(quick,"ToggleFavorite");NativeQuickUseMatrix.Until(()=>{Call(quick,"Poll");return !temporary.Busy;});
                    Require(temporary.KeepFavorited,"explicit favorite enable can recover its own failed toggle");
                }
                finally{Set(quick,"Settings",original);Set(quick,"published",-1L);Call(quick,"Poll");}
            }
            Console.WriteLine("PASS: actual Host failed deletion, shared conflict reservation and explicit recovery; unrelated hotkeys remain editable.");
        }
        private sealed class ControlledStore:IPreferenceStorage
        {
            private byte[] bytes;internal bool Fail;
            internal ControlledStore(byte[] bytes){this.bytes=bytes;}
            public PreferenceReadResult Read(){return new PreferenceReadResult(PreferenceReadStatus.Loaded,bytes,"fixture",null);}
            public PreferenceWriteResult Write(string identity,byte[] value)
            {if(Fail)return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,null,"isolated storage failure");bytes=value;return new PreferenceWriteResult(PreferenceWriteStatus.Saved,"fixture",null);}
            public void Dispose(){}
        }
    }
}
