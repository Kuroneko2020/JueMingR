using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // The approval is code-owned evidence, not a writable runtime trust store.
    // A changed rewriter or Tile manifest without a matching reviewed approval
    // safely disables reuse. Full audited generation still works during edits.
    internal static class NativeMaterialIdentity
    {
        internal static string Expected()
        {
            var assembly=typeof(NativeMaterialIdentity).Assembly;
            var actual=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var value in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
                if(value.Key.StartsWith("Prediction.Rule.",StringComparison.Ordinal))actual.Add(value.Key.Substring(16),value.Value);
            using(var stream=assembly.GetManifestResourceStream("JueMingR.Prediction.NativeMaterialApproval"))
            using(var reader=new StreamReader(stream??throw new InvalidDataException("Material approval resource absent.")))
            {
                if(reader.ReadLine()!="native-material-v1")return null;
                string expected=reader.ReadLine(),line;var seen=new HashSet<string>(StringComparer.Ordinal);
                while((line=reader.ReadLine())!=null)
                {var row=line.Split('|');string hash;if(row.Length!=2 || !seen.Add(row[0]) || !actual.TryGetValue(row[0],out hash) || hash!=row[1])return null;}
                return seen.Count==4 && actual.Count==4 && expected!=null && expected.Length==64?expected:null;
            }
        }
    }
}
