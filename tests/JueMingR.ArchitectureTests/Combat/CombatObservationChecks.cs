using System;
using System.Reflection;

namespace JueMingR.ArchitectureTests
{
    internal static class CombatObservationChecks
    {
        internal static void Run()
        {
            var assembly=typeof(JueMingR.Features.Combat.CombatOptions).Assembly;
            var type=assembly.GetType("JueMingR.Features.Combat.ObservationOptions");
            Require(type!=null,"first-batch observation preferences must exist independently of combat attack switches");
            object value=Activator.CreateInstance(type,new object[]{false,false,false,false,false,25});
            Require(!(bool)type.GetProperty("Collision").GetValue(value) && !(bool)type.GetProperty("Path").GetValue(value),"both real displays default off");
            foreach(int radius in new[]{0,25,50})
            {
                object a=Activator.CreateInstance(type,new object[]{true,false,true,true,true,radius});
                Require((int)type.GetProperty("Radius").GetValue(a)==radius,"mouse radius survives round trip without controlling player mode");
                object b=type.GetMethod("Toggle").Invoke(a,new object[]{1});
                Require((bool)type.GetProperty("Collision").GetValue(b) && (bool)type.GetProperty("Path").GetValue(b),"path toggle preserves independent collision and shared preferences");
                Require(!(bool)type.GetProperty("Path").GetValue(a),"preference edits are immutable");
            }
            foreach(int radius in new[]{-1,51})
            {
                bool rejected=false;
                try{Activator.CreateInstance(type,new object[]{false,false,false,false,false,radius});}
                catch(TargetInvocationException e){rejected=e.InnerException is ArgumentOutOfRangeException;}
                Require(rejected,"invalid radius cannot silently normalize");
            }
        }
        private static void Require(bool condition,string text){if(!condition)throw new InvalidOperationException(text);}
    }
}
