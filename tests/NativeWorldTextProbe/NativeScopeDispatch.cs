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
