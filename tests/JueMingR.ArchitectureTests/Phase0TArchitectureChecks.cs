using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JueMingR.ArchitectureTests
{
    internal static class Phase0TArchitectureChecks
    {
        internal static void Check(IList<string> failures)
        {
            Assembly features = Assembly.Load("JueMingR.Features");
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "JueMingR.Features.Biomes.BiomeDisplayFeature",
                "JueMingR.Features.Biomes.BiomeDisplayViewModel"
            };
            if (!expected.SetEquals(features.GetExportedTypes().Select(type => type.FullName)))
            {
                failures.Add("Features must export exactly the approved Phase 0-T biome feature and ViewModel.");
            }

            string[] forbiddenAssemblyPrefixes =
            {
                "Terraria",
                "ReLogic",
                "Microsoft.Xna.Framework",
                "0Harmony"
            };
            foreach (Assembly assembly in new[] { typeof(JueMingR.Platform.Operations.IGameOperationRequest).Assembly, features })
            {
                if (assembly.GetReferencedAssemblies().Any(reference =>
                    forbiddenAssemblyPrefixes.Any(prefix =>
                        reference.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
                {
                    failures.Add(assembly.GetName().Name + " must remain host-neutral.");
                }
            }
        }
    }
}
