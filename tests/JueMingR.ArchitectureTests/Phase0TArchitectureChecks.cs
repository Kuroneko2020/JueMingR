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
                "JueMingR.Features.Biomes.BiomeDisplayViewModel",
                "JueMingR.Features.Biomes.BiomePreferenceCodec",
                "JueMingR.Features.Items.ItemAutomationSettings", "JueMingR.Features.Items.ItemAutomationCodec",
                "JueMingR.Features.Items.ItemActionKind", "JueMingR.Features.Items.ItemListKind",
                "JueMingR.Features.Items.ItemAutomationFeature",
                "JueMingR.Features.EntityLabels.EntityLabelKind", "JueMingR.Features.EntityLabels.NpcLabelMode",
                "JueMingR.Features.EntityLabels.EntityLabelStyle", "JueMingR.Features.EntityLabels.EntityLabelSettings",
                "JueMingR.Features.EntityLabels.EntityLabelCodec",
                "JueMingR.Features.EntityLabels.EntityLabelFeature", "JueMingR.Features.EntityLabels.EntityLabel",
                "JueMingR.Features.Notes.Note", "JueMingR.Features.Notes.NoteReading", "JueMingR.Features.Notes.Notebook",
                "JueMingR.Features.Notes.NotebookCodec", "JueMingR.Features.Notes.TextElements",
                "JueMingR.Features.Notes.NoteEditor", "JueMingR.Features.Notes.NotesTextLayout",
                "JueMingR.Features.Notes.NotesTextLine", "JueMingR.Features.Notes.NotesFeature",
                "JueMingR.Features.Notes.NotesWorkspace", "JueMingR.Features.Notes.NotesAction",
                "JueMingR.Features.Notes.NotesActionKind"
            };
            if (!expected.SetEquals(features.GetExportedTypes().Select(type => type.FullName)))
            {
                failures.Add("Features must export exactly the registered feature contracts.");
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
