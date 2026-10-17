using System;
using System.Reflection;
using System.Text;
using JueMingR.Features.Combat;

namespace JueMingR.ArchitectureTests
{
    internal static class ObservationAimSettingsChecks
    {
        internal static void Run()
        {
            var aim=typeof(ObservationOptions).GetProperty("Aim");
            Require(aim!=null,"Standard aim switch is absent; radius and path cannot stand in for it.");
            Require(!(bool)aim.GetValue(new ObservationOptions()),"New aim switch defaults OFF.");
            const string old="{\"format\":\"JueMingR.CombatObservation\",\"version\":1,\"collision\":true,\"path\":true,\"clearLine\":true,\"mouseCenter\":true,\"dummy\":true,\"radius\":42}";
            var decode=typeof(ObservationSettings).GetMethod("Decode",BindingFlags.NonPublic|BindingFlags.Static);
            var encode=typeof(ObservationSettings).GetMethod("Encode",BindingFlags.NonPublic|BindingFlags.Static);
            foreach(string suffix in new[]{"",",\"marker\":true",",\"aim\":true",",\"marker\":true,\"aim\":false"})
            {
                var value=(ObservationOptions)decode.Invoke(null,new object[]{Encoding.UTF8.GetBytes(old.Substring(0,old.Length-1)+suffix+"}")});
                Require((bool)aim.GetValue(value)==suffix.Contains("\"aim\":true"),"Only explicit accepted aim=true enables aim; old legal preferences survive.");
                Require(value.Path && value.Collision && value.ClearLine && value.MouseCenter && value.Dummy && value.Radius==42,"Additive switch preserves shared and independent display preferences.");
                var next=value.Toggle(6).WithRadius(0).Toggle(0);
                Require((bool)aim.GetValue(next)!=(bool)aim.GetValue(value) && next.Path && !next.Collision && next.Marker==value.Marker,"All immutable setting copies preserve independent aim state.");
                var round=(ObservationOptions)decode.Invoke(null,new object[]{encode.Invoke(null,new object[]{next})});
                Require((bool)aim.GetValue(round)==(bool)aim.GetValue(next) && round.Marker==next.Marker && round.Radius==0,"Saved aim state round-trips in the existing owner.");
            }
            foreach(string suffix in new[]{",\"aim\":true,\"aim\":false",",\"aim\":\"true\"",",\"aim\":{}",",\"unknown\":true"})
            {
                bool rejected=false;try{decode.Invoke(null,new object[]{Encoding.UTF8.GetBytes(old.Substring(0,old.Length-1)+suffix+"}")});}
                catch(TargetInvocationException e){rejected=e.InnerException is JueMingR.Platform.Settings.PreferenceFormatException;}
                Require(rejected,"Invalid additive fields retain existing protection rather than silently enabling aim.");
            }
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
