using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace NativeWorldTextProbe
{
    // Selection is checked at the real dispatcher, and completion is emitted
    // only after its business assertions return. argv alone is not leaf proof.
    internal static class NativeScopeDispatch
    {
        private static string requested, expected, entered;
        internal static void PrepareOwnedChild(System.Diagnostics.ProcessStartInfo start)
        {
            // An internal child is a step of its parent's assertion, not that
            // formal invocation. Its real exit remains the parent's input;
            // it must not inherit or sign the parent's argv/execution token.
            start.EnvironmentVariables.Remove("JUEMINGR_WORKLOAD_SCOPE_CONTEXT");
            start.EnvironmentVariables.Remove("JUEMINGR_WORKLOAD_ASSERTION_CONTEXT");
        }
        internal static void Begin(string repository, string scope)
        {
            requested=scope; expected=entered=null;
            var selectors=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var leaves=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string line in File.ReadAllLines(Path.Combine(repository,"eng","Workload.NativeDispatch.tsv")))
            {
                if(line.Length==0 || line[0]=='#')continue;
                string[] row=line.Split('\t');
                if(row.Length==2 && row[0]=="selector") { if(!selectors.Add(row[1]))throw new InvalidDataException("Duplicate native selector."); }
                else if(row.Length==3 && row[0]=="leaf")leaves.Add(row[1],row[2]);
                else throw new InvalidDataException("Malformed native dispatch contract.");
            }
            leaves.TryGetValue(scope,out expected);
            string context=Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT");
            bool formal=context!=null;
            if(formal && context.Split('|')[0]!="native-"+scope.Replace(':','-'))
                throw new ArgumentException("Formal check name and native scope differ.");
            // Named exclusive scopes own their complete family. The generic
            // explicit NpcProduction/strategy specialist still permits its
            // deliberate environment axis when no formal runner requested it.
            if(formal || expected!=null)
                foreach(DictionaryEntry pair in Environment.GetEnvironmentVariables())
                {
                    string key=(string)pair.Key;
                    if(key.StartsWith("JUEMINGR_",StringComparison.OrdinalIgnoreCase) &&
                        (key.EndsWith("ONLY",StringComparison.OrdinalIgnoreCase) || key.EndsWith("SCOPE",StringComparison.OrdinalIgnoreCase) || key.EndsWith("PHASE",StringComparison.OrdinalIgnoreCase) || key.EndsWith("PHASES",StringComparison.OrdinalIgnoreCase) || key.EndsWith("SINGLE",StringComparison.OrdinalIgnoreCase)) && !selectors.Contains(key))
                        throw new ArgumentException("Unknown inherited workload selector: "+key);
                    if(selectors.Contains(key))Environment.SetEnvironmentVariable(key,null);
                }
        }
        internal static string Enter(string leaf)
        {
            if(expected!=null && (entered!=null || expected!=leaf))throw new InvalidOperationException("Native scope "+requested+" expected "+expected+" but entered "+leaf);
            entered=leaf;
            return leaf;
        }
        internal static void EnterSelected(string leaf)
        {
            // The existing specialist remains usable without a formal named
            // leaf. An explicit family must enter at its actual Run dispatch,
            // where a different family is rejected by the same exact guard.
            if(expected!=null)Enter(leaf);
        }
        internal static void RunExclusiveEffectFamily(string family, Action run)
        {
            // These are the actual inner shortcuts of the explicitly named
            // families. A named scope owns the unchanged complete family Run;
            // restore the caller's process state even when its assertions fail.
            string previous=Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY");
            string[] keys={"JMR_PIRANHA_MOTOR_SCENE","JMR_PHOENIX_ORACLE_ONLY","JMR_ATTACHED_SHOT_ONLY","JMR_PHANTASM_BOUNDARY_TRACE","JMR_BETSY_FLAME_ORACLE_ONLY","JMR_ARC_SWING_LEAF","JMR_ARC_SWING_ITEM","JMR_THORN_LEAF","JMR_THORN_ITEM"};
            var values=new string[keys.Length];
            for(int i=0;i<keys.Length;i++){values[i]=Environment.GetEnvironmentVariable(keys[i]);Environment.SetEnvironmentVariable(keys[i],null);}
            try{Environment.SetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY",family);run();}
            finally{Environment.SetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY",previous);for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],values[i]);}
        }
        internal static void Complete()
        {
            if(expected==null)return;
            if(entered!=expected)throw new InvalidOperationException("Native scope returned without its required leaf: "+requested);
            string context=Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT");
            if(context!=null)Console.WriteLine("JMR-LEAF-V1|"+context+"|"+entered);
            else Console.WriteLine("NATIVE-LEAF completed request="+requested+" actual="+entered);
        }
    }
}
