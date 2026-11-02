using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace JueMingR.TerrariaHost.WorldGenPreview
{
    // All discovery is installation-only and bounded to the native preview and
    // Controller closure types. No UI references or global language hooks are
    // retained. The caller owns patch rollback; the one-entry progress projection
    // belongs to the current UI and is released on its native deactivation.
    internal static class WorldGenPreviewLocalization
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const string UiName = "Terraria.GameContent.UI.States.UIWorldGenDebug";
        private const string ControllerName = "Terraria.WorldBuilding.WorldGenerator+Controller";
        private static string progressSource;
        private static string progressDisplay;
        private static Func<bool> isPreviewContext;

        internal static void Install(Harmony harmony, Assembly target, Func<bool> ownsPreviewContext)
        {
            if (harmony == null) throw new ArgumentNullException(nameof(harmony));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (ownsPreviewContext == null) throw new ArgumentNullException(nameof(ownsPreviewContext));
            // The feature's sole lifecycle owner supplies a static predicate;
            // this module does not infer ownership from a UI type or debug flag.
            isPreviewContext = ownsPreviewContext;
            Type ui = target.GetType(UiName, true);
            Type controller = target.GetType(ControllerName, true);
            Type passElement = ui.GetNestedType("GenPassElement", BindingFlags.NonPublic);
            if (passElement == null) throw new MissingMemberException(UiName, "GenPassElement");
            var patches = new List<MethodBase>();
            int stepLabels = 0, stepTitles = 0, progressConsumers = 0, feedbackConsumers = 0;
            foreach (Type scope in new[] { ui, controller })
            {
                foreach (Type type in TypeTree(scope))
                {
                    foreach (MethodBase method in Methods(type))
                    {
                        var code = PatchProcessor.GetOriginalInstructions(method);
                        bool changes = false;
                        foreach (CodeInstruction instruction in code)
                        {
                            string translated;
                            if (instruction.opcode == OpCodes.Ldstr && WorldGenPreviewText.TryLiteral((string)instruction.operand, out translated)) changes = true;
                            if (IsStepRead(method, instruction))
                            {
                                changes = true;
                                if (method.IsConstructor) stepLabels++; else stepTitles++;
                            }
                            if (IsProgressRead(method, instruction)) { changes = true; progressConsumers++; }
                            if (IsFeedbackCall(method, instruction)) { changes = true; feedbackConsumers++; }
                        }
                        if (changes) patches.Add(method);
                    }
                }
            }
            MethodInfo quickload = target.GetType("Terraria.Testing.ChatCommands.ToolkitDebugCommands", true).GetMethod("QuickLoadRegenCommand", Flags);
            if (quickload == null) throw new MissingMethodException("QuickLoadRegenCommand");
            int quickloadMessages = PatchProcessor.GetOriginalInstructions(quickload).Count(c => c.opcode == OpCodes.Ldstr && (string)c.operand == "Quick Load configuration set. Hold shift while loading to clear it.");
            MethodInfo restore = target.GetType("Terraria.WorldBuilding.WorldGenSnapshot", true).GetMethod("Restore", Flags);
            if (restore == null || PatchProcessor.GetOriginalInstructions(restore).Count(c => IsFeedbackCall(restore, c)) != 1)
                throw new MissingMethodException("worldgen-preview-snapshot-feedback");
            // Fail before advertising the entry if a required locked-version
            // display seam moved. Never alter a similarly named internal key.
            if (stepLabels != 1 || stepTitles != 3 || progressConsumers != 1 || feedbackConsumers != 1 || quickloadMessages != 1)
                throw new InvalidOperationException("worldgen-preview-localization-seams:" + stepLabels + "/" + stepTitles + "/" + progressConsumers + "/" + feedbackConsumers + "/" + quickloadMessages);
            patches.Add(quickload);
            patches.Add(restore);
            var rewrite = new HarmonyMethod(typeof(WorldGenPreviewLocalization).GetMethod(nameof(Rewrite), BindingFlags.NonPublic | BindingFlags.Static));
            foreach (MethodBase method in patches) harmony.Patch(method, transpiler: rewrite);
            harmony.Patch(ui.GetMethod("OnDeactivate", Flags), postfix: new HarmonyMethod(typeof(WorldGenPreviewLocalization).GetMethod(nameof(ClearProgress), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static IEnumerable<Type> TypeTree(Type type)
        {
            yield return type;
            foreach (Type nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                foreach (Type child in TypeTree(nested)) yield return child;
        }

        private static IEnumerable<MethodBase> Methods(Type type)
        {
            foreach (ConstructorInfo constructor in type.GetConstructors(Flags))
                if (constructor.GetMethodBody() != null) yield return constructor;
            foreach (MethodInfo method in type.GetMethods(Flags))
                if (!method.ContainsGenericParameters && method.GetMethodBody() != null) yield return method;
        }

        private static bool IsStepRead(MethodBase method, CodeInstruction instruction)
        {
            var field = instruction.operand as FieldInfo;
            return method.DeclaringType.FullName == UiName + "+GenPassElement" && (method.IsConstructor || method.Name == "GetTitle") &&
                instruction.opcode == OpCodes.Ldfld && field != null && field.DeclaringType.FullName == "Terraria.WorldBuilding.GenPass" && field.Name == "Name" && field.FieldType == typeof(string);
        }

        private static bool IsProgressRead(MethodBase method, CodeInstruction instruction)
        {
            var call = instruction.operand as MethodInfo;
            return method.DeclaringType.FullName.StartsWith(UiName + "+", StringComparison.Ordinal) && IsCall(instruction) && call != null &&
                call.DeclaringType.FullName == "Terraria.WorldBuilding.GenerationProgress" && call.Name == "get_Message" && call.ReturnType == typeof(string);
        }

        private static bool IsFeedbackCall(MethodBase method, CodeInstruction instruction)
        {
            var call = instruction.operand as MethodInfo;
            bool source = method.DeclaringType.FullName == ControllerName && method.Name == "ReportException" ||
                method.DeclaringType.FullName == "Terraria.WorldBuilding.WorldGenSnapshot" && method.Name == "Restore";
            return source && IsCall(instruction) && call != null &&
                call.DeclaringType.FullName == "Terraria.Main" && call.Name == "NewText" && call.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(byte), typeof(byte), typeof(byte) });
        }

        private static bool IsCall(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt;
        }

        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> source, MethodBase __originalMethod)
        {
            foreach (CodeInstruction instruction in source)
            {
                string translated = null;
                bool literal = instruction.opcode == OpCodes.Ldstr && WorldGenPreviewText.TryLiteral((string)instruction.operand, out translated);
                if (IsFeedbackCall(__originalMethod, instruction))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = typeof(WorldGenPreviewLocalization).GetMethod(nameof(Feedback), BindingFlags.NonPublic | BindingFlags.Static);
                }
                yield return instruction;
                if (literal)
                {
                    yield return new CodeInstruction(OpCodes.Ldstr, translated);
                    yield return new CodeInstruction(OpCodes.Call, typeof(WorldGenPreviewLocalization).GetMethod(nameof(Choose), BindingFlags.NonPublic | BindingFlags.Static));
                }
                if (IsStepRead(__originalMethod, instruction))
                    yield return new CodeInstruction(OpCodes.Call, typeof(WorldGenPreviewLocalization).GetMethod(nameof(Step), BindingFlags.NonPublic | BindingFlags.Static));
                if (IsProgressRead(__originalMethod, instruction))
                    yield return new CodeInstruction(OpCodes.Call, typeof(WorldGenPreviewLocalization).GetMethod(nameof(Progress), BindingFlags.NonPublic | BindingFlags.Static));
            }
        }

        private static void Feedback(string message, byte red, byte green, byte blue)
        {
            // Trace and exception payloads remain native; only this Controller's
            // actual chat presentation consumes the translated display value.
            Terraria.Main.NewText(OwnsContext ? WorldGenPreviewText.Display(message) : message, red, green, blue);
        }

        private static bool OwnsContext => isPreviewContext != null && isPreviewContext();

        private static string Choose(string original, string translated)
        {
            return OwnsContext ? translated : original;
        }

        private static string Step(string raw)
        {
            return OwnsContext ? WorldGenPreviewText.Step(raw) : raw;
        }

        private static string Progress(string raw)
        {
            // This sole consumer executes on the UI thread. Capacity is one;
            // native progress changes invalidate it, including changes to null.
            if (!OwnsContext) return raw;
            if (string.Equals(progressSource, raw, StringComparison.Ordinal)) return progressDisplay;
            progressSource = raw;
            progressDisplay = WorldGenPreviewText.Display(raw);
            return progressDisplay;
        }

        private static void ClearProgress()
        {
            progressSource = null;
            progressDisplay = null;
        }
    }
}
