using System;
using System.Linq;
using System.Text;
using System.Threading;
using JueMingR.Features.Combat;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class ObservationMarkerSettingsChecks
    {
        internal static void Run()
        {
            const string legacy="{\"format\":\"JueMingR.CombatObservation\",\"version\":1,\"collision\":true,\"path\":true,\"clearLine\":true,\"mouseCenter\":true,\"dummy\":true,\"radius\":42}";
            var storage=new Storage{Bytes=Encoding.UTF8.GetBytes(legacy)};
            using(var settings=new ObservationSettings(storage))
            {
                Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Ready && !settings.Value.Marker && settings.Value.Collision && settings.Value.Path && settings.Value.ClearLine && settings.Value.MouseCenter && settings.Value.Dummy && settings.Value.Radius==42,"Old legal document preserves every field and missing marker defaults OFF.");
                Require(!settings.Value.Aim,"Missing aim in the original version-one document defaults OFF.");
                var next=settings.Value.Toggle(5).Toggle(6).WithRadius(17).Toggle(0);Require(next.Aim && next.Marker && !next.Collision && next.Path && !settings.Value.Marker,"Immutable copies retain aim, marker and other committed values.");
                storage.Block=true;Require(settings.Set(next),"Aim/marker save is accepted through existing document worker.");Require(settings.Busy && !settings.Value.Aim && !settings.Value.Marker && !settings.Set(next.Toggle(6)),"Busy does not mutate committed aim/marker or admit another writer.");storage.Release.Set();Until(()=>{settings.Poll();return !settings.Busy;});Require(settings.CompletionSucceeded && settings.Value.Aim && settings.Value.Marker,"Successful save commits aim and marker.");
            }
            using(var read=new ObservationSettings(storage)){Until(()=>{read.Poll();return read.Loaded;});Require(read.Ready && read.Value.Aim && read.Value.Marker && !read.Value.Collision && read.Value.Path && read.Value.Radius==17,"Saved aim/marker and preserved fields survive a new settings owner.");}
            foreach(string suffix in new[]{",\"marker\":true,\"marker\":false",",\"unknown\":1",",\"marker\":\"true\"",",\"marker\":{}",",\"aim\":true,\"aim\":false",",\"aim\":\"true\""})
            {
                var bytes=Encoding.UTF8.GetBytes(legacy.Substring(0,legacy.Length-1)+suffix+"}");var invalid=new Storage{Bytes=bytes};using(var settings=new ObservationSettings(invalid)){Until(()=>{settings.Poll();return settings.Loaded;});Require(settings.Protected && !settings.Ready && !settings.Set(new ObservationOptions(marker:true)) && invalid.Writes==0 && invalid.Bytes.SequenceEqual(bytes),"Duplicate/unknown/wrong-type document is protected byte-for-byte.");}
            }
            var failed=new Storage{Bytes=(byte[])storage.Bytes.Clone(),Fail=true};using(var settings=new ObservationSettings(failed)){Until(()=>{settings.Poll();return settings.Ready;});var bytes=(byte[])failed.Bytes.Clone();Require(settings.Set(settings.Value.Toggle(5).Toggle(6)),"Failed write is accepted before its actual result.");Until(()=>{settings.Poll();return !settings.Busy;});Require(!settings.CompletionSucceeded && settings.Value.Aim && settings.Value.Marker && settings.Message!=null && failed.Bytes.SequenceEqual(bytes),"Failed aim/marker save keeps committed values, original bytes and existing feedback semantics.");}
        }
        private sealed class Storage : IPreferenceStorage
        {
            internal byte[] Bytes;internal int Writes;internal bool Block,Fail;internal readonly ManualResetEventSlim Release=new ManualResetEventSlim(false);
            public PreferenceReadResult Read(){return new PreferenceReadResult(Bytes==null?PreferenceReadStatus.Missing:PreferenceReadStatus.Loaded,Bytes,"fixture",null);}
            public PreferenceWriteResult Write(string identity,byte[] bytes){Writes++;if(Block)Release.Wait(3000);if(Fail)return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure,null,"fixture-write");Bytes=(byte[])bytes.Clone();return new PreferenceWriteResult(PreferenceWriteStatus.Saved,"fixture",null);}
            public void Dispose(){}
        }
        private static void Until(Func<bool> test){Require(SpinWait.SpinUntil(test,3000),"Settings worker reached its actual bounded completion.");}
        private static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    }
}
