using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using Terraria;

namespace NativeWorldTextProbe
{
    // One explicitly invoked research gate; the original updates and normal
    // selection/transport/Receive/cache consumer remain the tested chain.
    internal static class NativeCombatConditionalChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        internal static int RolePeak,HarpyConsumed;
        internal static int Roles(object native,bool active)
        {
            var field=native.GetType().GetField("exactNpcs",Flags)??native.GetType().GetField("npcs",Flags);
            return ((IEnumerable)field.GetValue(native)).Cast<int>().Count(slot=>!active || Main.npc[slot].active);
        }
        internal static void Run(object context,object native,NpcPredictionCache cache,Action step,string output)
        {
            CheckGeometryScope(native);
            NativeCombatEnvironmentChecks.Continuous(context,native,cache,step,output,true);
            int peak=RolePeak,harpy=HarpyConsumed;
            Console.WriteLine("CONDITIONAL-GATE fullAI-role-peak="+peak+" harpy-consumed-full-future-updates="+harpy);
            if(Environment.GetEnvironmentVariable("JUEMINGR_NPC_CONDITIONAL_ASSERT")=="1" && (peak>=9 || harpy<300))
                throw new InvalidOperationException("Candidate gate unmet: background fullAI roles must be below whole pool and unsupported-model Harpy must consume a sustained 120-step native future.");
        }
        internal static void CheckGeometryScope(object native)
        {
            var type=native.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.ConditionalNpcQuery");
            if(type==null)return;
            var begin=type.GetMethod("GeometryBegin",Flags);var end=type.GetMethod("GeometryEnd",Flags);var owner=type.GetField("geometryWriter",Flags);
            var first=new NPC();var second=new NPC();var failure=new InvalidOperationException("scope-test");
            object[] outer={first,null};begin.Invoke(null,outer);object[] inner={second,null};begin.Invoke(null,inner);
            if(!ReferenceEquals(owner.GetValue(null),second) || !ReferenceEquals(end.Invoke(null,new[]{inner[1],failure}),failure) || !ReferenceEquals(owner.GetValue(null),first))throw new InvalidOperationException("Nested geometry scope/exception failed.");
            end.Invoke(null,new[]{outer[1],null});
            if(owner.GetValue(null)!=null)throw new InvalidOperationException("Geometry scope leaked.");
            Console.WriteLine("CONDITIONAL-SCOPE nested and exception restoration checked.");
        }
    }
}
