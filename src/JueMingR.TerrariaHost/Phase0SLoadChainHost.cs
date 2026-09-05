using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Biomes;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Terraria.UI;

namespace JueMingR.TerrariaHost
{
    public static class Phase0SLoadChainHost
    {
        private const string HarmonyAssemblyFullName =
            "0Harmony, Version=2.4.2.0, Culture=neutral, PublicKeyToken=null";
        private const string WorkerTypeName = "JueMingR.TerrariaHost.Phase0SHarmonyWorker";
        private const string ReLogicAssemblyFullName =
            "ReLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

        private static int installAttempted;
        private static int primaryErrorRecorded;
        private static int cleanupErrorRecorded;

        public static void Install(Assembly targetAssembly, Assembly reLogicAssembly)
        {
            if (Interlocked.CompareExchange(ref installAttempted, 1, 0) != 0)
            {
                return;
            }

            RuntimeManifest manifest = null;
            string evidencePath = null;
            string stage = "HOST_VALIDATE";
            string code = "IDENTITY_MISMATCH";
            try
            {
                string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                string sidecarDirectory = Path.Combine(baseDirectory, "JueMingR.Validation");
                string manifestPath = Path.Combine(sidecarDirectory, "phase-0-s-runtime.manifest");
                manifest = RuntimeManifest.Read(manifestPath);
                evidencePath = Path.Combine(sidecarDirectory, manifest.EvidenceFileName);

                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                string hostPath = Path.Combine(sidecarDirectory, "JueMingR.TerrariaHost.dll");
                AssemblyIdentity.VerifyUniqueLoaded(
                    executingAssembly,
                    hostPath,
                    manifest.HostAssemblySimpleName,
                    manifest.HostAssemblyVersion,
                    manifest.HostAssemblyMvid,
                    manifest.HostAssemblySha256);

                string targetPath = Path.Combine(baseDirectory, "Terraria.exe");
                AssemblyIdentity.VerifyLoaded(
                    targetAssembly,
                    targetPath,
                    manifest.TargetAssemblySimpleName,
                    manifest.TargetAssemblyVersion,
                    manifest.TargetAssemblyMvid,
                    manifest.TargetAssemblySha256);

                AssemblyIdentity.VerifyReLogicBinding(
                    targetAssembly,
                    reLogicAssembly,
                    manifest);

                stage = "HARMONY_LOAD";
                code = "PRELOADED";
                if (AssemblyIdentity.CountLoaded(manifest.HarmonyAssemblySimpleName) != 0)
                {
                    throw new InvalidOperationException("Harmony was loaded before the Phase 0-S identity gate.");
                }

                string harmonyPath = Path.Combine(sidecarDirectory, "0Harmony.dll");
                code = "IDENTITY_MISMATCH";
                AssemblyIdentity.VerifyFileNameAndVersion(
                    harmonyPath,
                    manifest.HarmonyAssemblySimpleName,
                    manifest.HarmonyAssemblyVersion);
                AssemblyIdentity.VerifyFileHash(harmonyPath, manifest.HarmonyAssemblySha256);

                Assembly harmonyAssembly = Assembly.Load(new AssemblyName(HarmonyAssemblyFullName));
                code = "NOT_UNIQUE";
                AssemblyIdentity.VerifyUniqueLoaded(
                    harmonyAssembly,
                    harmonyPath,
                    manifest.HarmonyAssemblySimpleName,
                    manifest.HarmonyAssemblyVersion,
                    manifest.HarmonyAssemblyMvid,
                    manifest.HarmonyAssemblySha256);

                stage = "HARMONY_LOAD";
                code = "APPEND_FAILED";
                EvidenceWriter.AppendEvent(evidencePath, manifest.PackageId, 2, "HARMONY_READY");

                stage = "PATCH";
                code = "PATCH_FAILED";
                Type workerType = executingAssembly.GetType(WorkerTypeName, true, false);
                MethodInfo workerInstall = workerType.GetMethod(
                    "Install",
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
                    null,
                    new[] { typeof(RuntimeManifest), typeof(Assembly), typeof(Assembly) },
                    null);
                if (workerInstall == null ||
                    workerInstall.ReturnType != typeof(void) ||
                    workerInstall.IsGenericMethod ||
                    workerInstall.ContainsGenericParameters)
                {
                    throw new InvalidOperationException("The fixed Phase 0-S Harmony worker entry is invalid.");
                }

                workerInstall.Invoke(null, new object[] { manifest, targetAssembly, harmonyAssembly });
            }
            catch (Exception exception)
            {
                TryRecordPrimaryError(
                    evidencePath,
                    manifest == null ? null : manifest.PackageId,
                    stage,
                    code,
                    exception);
                throw;
            }
        }

        internal static void TryRecordPatchFailure(
            string evidencePath,
            string packageId,
            Exception primaryException,
            Exception cleanupException)
        {
            TryRecordPrimaryError(
                evidencePath,
                packageId,
                "PATCH",
                "PATCH_FAILED",
                primaryException);
            if (cleanupException != null &&
                Interlocked.CompareExchange(ref cleanupErrorRecorded, 1, 0) == 0)
            {
                Exception effective = EffectiveException(cleanupException);
                EvidenceWriter.TryAppendError(
                    evidencePath,
                    packageId,
                    "PATCH_CLEANUP",
                    "CLEANUP_FAILED",
                    effective.GetType().Name,
                    GetSafeMissingAssemblyIdentity(effective),
                    true);
            }
        }

        internal static void TryRecordPrimaryError(
            string evidencePath,
            string packageId,
            string stage,
            string code,
            Exception exception)
        {
            if (evidencePath == null || packageId == null ||
                Interlocked.CompareExchange(ref primaryErrorRecorded, 1, 0) != 0)
            {
                return;
            }

            Exception effective = EffectiveException(exception);
            EvidenceWriter.TryAppendError(
                evidencePath,
                packageId,
                stage,
                code,
                effective.GetType().Name,
                GetSafeMissingAssemblyIdentity(effective),
                false);
        }

        private static Exception EffectiveException(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception;
        }

        private static string GetSafeMissingAssemblyIdentity(Exception exception)
        {
            FileNotFoundException missing = exception as FileNotFoundException;
            return missing != null &&
                String.Equals(missing.FileName, ReLogicAssemblyFullName, StringComparison.Ordinal)
                ? missing.FileName
                : null;
        }
    }

    internal static class Phase0SHarmonyWorker
    {
        private static int hookCommitted;
        private static int postfixGate;
        private static int handoffGate;
        private static int biomeFeatureFailed;
        private static bool f5LayersReady;
        private static PostfixContext postfixContext;

        private const string BiomeLayerName = "JueMingR: Biome Display";
        private const string BiomeLayerAnchorName = "Vanilla: Map / Minimap";

        internal static void Install(
            RuntimeManifest manifest,
            Assembly targetAssembly,
            Assembly harmonyAssembly)
        {
            if (!ReferenceEquals(typeof(Harmony).Assembly, harmonyAssembly) ||
                AssemblyIdentity.CountLoaded(manifest.HarmonyAssemblySimpleName) != 1)
            {
                throw new InvalidOperationException("The Harmony worker is not bound to the verified assembly.");
            }

            MethodInfo targetMethod = ResolveTargetMethod(manifest, targetAssembly);
            MethodInfo drawSetupMethod = ResolveDrawSetupMethod(targetAssembly);
            MethodInfo inputMethod = ResolveF5Target(targetAssembly, "DoUpdate_HandleInput", Type.EmptyTypes);
            MethodInfo npcHoverMethod = ResolveF5Target(targetAssembly, "HoverOverNPCs", new[] { typeof(Rectangle) });
            MethodInfo inputPostfixMethod = typeof(Phase0SHarmonyWorker).GetMethod("InputPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo npcHoverPrefixMethod = typeof(Phase0SHarmonyWorker).GetMethod("NpcHoverPrefix", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo postfixMethod = typeof(Phase0SHarmonyWorker).GetMethod(
                "Postfix",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
                null,
                new[] { typeof(List<GameInterfaceLayer>) },
                null);
            if (postfixMethod == null || postfixMethod.ReturnType != typeof(void))
            {
                throw new InvalidOperationException("The fixed Phase 0-S postfix is invalid.");
            }
            MethodInfo drawSetupPostfixMethod = typeof(Phase0SHarmonyWorker).GetMethod(
                "DrawSetupPostfix",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
                null,
                new[] { typeof(List<GameInterfaceLayer>) },
                null);
            if (drawSetupPostfixMethod == null || drawSetupPostfixMethod.ReturnType != typeof(void))
            {
                throw new InvalidOperationException("The fixed Phase 0-T draw setup postfix is invalid.");
            }

            string evidencePath = Path.Combine(
                Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), "JueMingR.Validation"),
                manifest.EvidenceFileName);
            postfixContext = new PostfixContext(manifest.PackageId, evidencePath);

            Harmony harmony = new Harmony(manifest.PatchOwner);
            bool patchAttempted = false;
            try
            {
                patchAttempted = true;
                harmony.Patch(
                    targetMethod,
                    null,
                    new HarmonyMethod(postfixMethod),
                    null,
                    null);
                VerifyExactPatchInfo(targetMethod, postfixMethod, manifest.PatchOwner);
                harmony.Patch(
                    drawSetupMethod,
                    null,
                    new HarmonyMethod(drawSetupPostfixMethod),
                    null,
                    null);
                VerifyExactPatchInfo(drawSetupMethod, drawSetupPostfixMethod, manifest.PatchOwner);
                harmony.Patch(inputMethod, null, new HarmonyMethod(inputPostfixMethod), null, null);
                VerifyExactPatchInfo(inputMethod, inputPostfixMethod, manifest.PatchOwner);
                harmony.Patch(npcHoverMethod, new HarmonyMethod(npcHoverPrefixMethod), null, null, null);
                VerifyExactPatchInfo(npcHoverMethod, npcHoverPrefixMethod, manifest.PatchOwner, true);
                EvidenceWriter.AppendEvent(evidencePath, manifest.PackageId, 3, "HOOK_INSTALLED");
                Volatile.Write(ref hookCommitted, 1);
            }
            catch (Exception primaryException)
            {
                Exception cleanupException = null;
                if (patchAttempted)
                {
                    cleanupException = UnpatchTargets(
                        harmony,
                        manifest.PatchOwner,
                        targetMethod,
                        drawSetupMethod, inputMethod, npcHoverMethod);
                }

                Phase0SLoadChainHost.TryRecordPatchFailure(
                    evidencePath,
                    manifest.PackageId,
                    primaryException,
                    cleanupException);
                throw;
            }
        }

        private static Exception UnpatchTargets(
            Harmony harmony,
            string owner,
            MethodInfo updateMethod,
            MethodInfo drawSetupMethod, MethodInfo inputMethod, MethodInfo npcHoverMethod)
        {
            Exception firstFailure = null;
            foreach (MethodInfo method in new[] { updateMethod, drawSetupMethod, inputMethod, npcHoverMethod })
            {
                try
                {
                    harmony.Unpatch(method, HarmonyPatchType.All, owner);
                }
                catch (Exception exception)
                {
                    if (firstFailure == null)
                    {
                        firstFailure = exception;
                    }
                }
            }

            return firstFailure;
        }

        private static MethodInfo ResolveTargetMethod(RuntimeManifest manifest, Assembly targetAssembly)
        {
            Type targetType = targetAssembly.GetType(manifest.TargetTypeName, true, false);
            MethodInfo targetMethod = targetAssembly.ManifestModule.ResolveMethod(
                manifest.TargetMethodMetadataToken) as MethodInfo;
            ParameterInfo[] parameters = targetMethod == null
                ? null
                : targetMethod.GetParameters();
            if (targetMethod == null ||
                !ReferenceEquals(targetMethod.DeclaringType, targetType) ||
                !ReferenceEquals(targetMethod.DeclaringType.Assembly, targetAssembly) ||
                !String.Equals(targetMethod.Name, manifest.TargetMethodName, StringComparison.Ordinal) ||
                targetMethod.MetadataToken != manifest.TargetMethodMetadataToken ||
                targetMethod.IsStatic != manifest.TargetMethodIsStatic ||
                targetMethod.ReturnType != typeof(void) ||
                !String.Equals(targetMethod.ReturnType.FullName, manifest.TargetMethodReturnType, StringComparison.Ordinal) ||
                parameters.Length != manifest.TargetMethodParameterCount ||
                parameters.Length != 1 ||
                !String.Equals(
                    parameters[0].ParameterType.FullName,
                    manifest.TargetMethodParameterType,
                    StringComparison.Ordinal) ||
                targetMethod.IsGenericMethod ||
                targetMethod.ContainsGenericParameters ||
                targetMethod.IsAbstract ||
                targetMethod.GetMethodBody() == null)
            {
                throw new InvalidOperationException("The Phase 0-S target method does not match the manifest.");
            }

            byte[] il = targetMethod.GetMethodBody().GetILAsByteArray();
            if (il == null || il.Length == 0)
            {
                throw new InvalidOperationException("The Phase 0-S target method has no managed IL.");
            }

            return targetMethod;
        }

        private static MethodInfo ResolveDrawSetupMethod(Assembly targetAssembly)
        {
            Type mainType = targetAssembly.GetType("Terraria.Main", true, false);
            MethodInfo method = mainType.GetMethod(
                "SetupDrawInterfaceLayers",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                null,
                Type.EmptyTypes,
                null);
            if (method == null ||
                method.IsStatic ||
                method.ReturnType != typeof(void) ||
                method.IsGenericMethod ||
                method.ContainsGenericParameters ||
                method.IsAbstract ||
                method.GetMethodBody() == null)
            {
                throw new InvalidOperationException(
                    "Terraria 1.4.5.8 SetupDrawInterfaceLayers does not match the fixed Phase 0-T shape.");
            }

            byte[] il = method.GetMethodBody().GetILAsByteArray();
            if (il == null || il.Length == 0)
            {
                throw new InvalidOperationException("The Phase 0-T draw setup target has no managed IL.");
            }

            return method;
        }

        private static void VerifyExactPatchInfo(
            MethodInfo targetMethod,
            MethodInfo postfixMethod,
            string expectedOwner, bool prefix = false)
        {
            Patches patches = Harmony.GetPatchInfo(targetMethod);
            if (patches == null ||
                patches.Owners.Count != 1 ||
                !String.Equals(patches.Owners[0], expectedOwner, StringComparison.Ordinal) ||
                patches.Prefixes.Count != (prefix ? 1 : 0) ||
                patches.Postfixes.Count != (prefix ? 0 : 1) ||
                patches.Transpilers.Count != 0 ||
                patches.Finalizers.Count != 0 ||
                patches.InnerPrefixes.Count != 0 ||
                patches.InnerPostfixes.Count != 0)
            {
                throw new InvalidOperationException("The Phase 0-S patch set is not exact.");
            }

            Patch postfix = prefix ? patches.Prefixes[0] : patches.Postfixes[0];
            if (!String.Equals(postfix.owner, expectedOwner, StringComparison.Ordinal) ||
                !SameMethod(postfix.PatchMethod, postfixMethod))
            {
                throw new InvalidOperationException("The Phase 0-S postfix owner or method does not match.");
            }
        }

        private static bool SameMethod(MethodBase left, MethodBase right)
        {
            return left != null &&
                right != null &&
                ReferenceEquals(left.Module, right.Module) &&
                left.MetadataToken == right.MetadataToken;
        }

        private static MethodInfo ResolveF5Target(Assembly targetAssembly, string name, Type[] parameters)
        {
            Type main = targetAssembly.GetType("Terraria.Main", true, false);
            MethodInfo method = main.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                null, parameters, null);
            if (method == null || method.DeclaringType != main || !method.IsPrivate || method.IsStatic ||
                method.ReturnType != typeof(void) || method.IsGenericMethod || method.ContainsGenericParameters ||
                method.IsAbstract || method.GetMethodBody() == null || method.GetMethodBody().GetILAsByteArray().Length == 0)
                throw new InvalidOperationException("The exact Phase 0-U target shape is unavailable: " + name);
            ParameterInfo[] actual = method.GetParameters();
            if (actual.Length != parameters.Length) throw new InvalidOperationException("F5 target parameter count differs.");
            for (int i = 0; i < actual.Length; i++)
                if (actual[i].ParameterType != parameters[i]) throw new InvalidOperationException("F5 target parameter type differs.");
            return method;
        }

        private static void InputPostfix()
        {
            PostfixContext context = postfixContext;
            if (Volatile.Read(ref hookCommitted) == 1 && context != null && context.Shell != null)
                context.Shell.ProcessInput();
        }

        private static bool NpcHoverPrefix()
        {
            PostfixContext context = postfixContext;
            F5Shell shell = context == null ? null : context.Shell;
            try { return Volatile.Read(ref hookCommitted) != 1 || shell == null || shell.AllowNpcHover(); }
            catch { if (shell != null) shell.FailClosed(); return true; }
        }

        private static void Postfix(List<GameInterfaceLayer> ____gameInterfaceLayers)
        {
            PostfixContext context = postfixContext;
            string stage = "POSTFIX";
            try
            {
                if (Volatile.Read(ref hookCommitted) != 1 || context == null)
                {
                    throw new InvalidOperationException("The Phase 0-S postfix context is unavailable.");
                }

                if (Interlocked.CompareExchange(ref postfixGate, 1, 0) == 0)
                {
                    EvidenceWriter.AppendEvent(
                        context.EvidencePath,
                        context.PackageId,
                        4,
                        "MAIN_UPDATE_POSTFIX_FIRED");

                    stage = "HANDOFF";
                    CompleteEmptyHandoffOnce();
                    EnsureBiomeLayerForHandoff(____gameInterfaceLayers);
                    EnsureF5Layers(____gameInterfaceLayers);
                    context.InitializeRuntime(Volatile.Read(ref biomeFeatureFailed) == 0);
                    EvidenceWriter.AppendEvent(
                        context.EvidencePath,
                        context.PackageId,
                        5,
                        "RUNTIME_HANDOFF_COMPLETE");
                }

                context.UpdateRuntime();
                context.UpdateShell();
            }
            catch (Exception exception)
            {
                HandlePostfixFailure(context, stage, exception);
            }
        }

        private static void HandlePostfixFailure(
            PostfixContext context,
            string stage,
            Exception exception)
        {
            DisableBiomeFeature();
            if (context != null)
            {
                Phase0SLoadChainHost.TryRecordPrimaryError(
                    context.EvidencePath,
                    context.PackageId,
                    stage,
                    "APPEND_FAILED",
                    exception);
            }
        }

        private static void DrawSetupPostfix(List<GameInterfaceLayer> ____gameInterfaceLayers)
        {
            try
            {
                InsertBiomeLayer(____gameInterfaceLayers);
            }
            catch
            {
                DisableBiomeFeature();
            }
            EnsureF5Layers(____gameInterfaceLayers);
        }

        private static void EnsureF5Layers(List<GameInterfaceLayer> layers)
        {
            if (layers == null) return;
            try
            {
                InsertF5Layer(layers, "JueMingR: F5 Pointer Begin", "Vanilla: Achievement Complete Popups", F5PointerBegin);
                InsertF5Layer(layers, "JueMingR: F5 Window", "Vanilla: Cursor", F5Window);
                InsertF5Layer(layers, "JueMingR: F5 Hover Gate", "Vanilla: Mouse Over", F5HoverGate);
                InsertF5Layer(layers, "JueMingR: F5 Pointer End", "Vanilla: Interface Logic 4", F5PointerEnd);
                f5LayersReady = true;
                if (postfixContext != null && postfixContext.Shell != null) postfixContext.Shell.LayersReady = true;
            }
            catch
            {
                f5LayersReady = false;
                if (postfixContext != null && postfixContext.Shell != null) postfixContext.Shell.FailClosed();
            }
        }

        private static void InsertF5Layer(List<GameInterfaceLayer> layers, string name, string anchor, GameInterfaceDrawMethod draw)
        {
            int anchorIndex = -1, ownIndex = -1, anchors = 0, own = 0;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] == null) continue;
                if (layers[i].Name == anchor) { anchorIndex = i; anchors++; }
                if (layers[i].Name == name) { ownIndex = i; own++; }
            }
            if (anchors != 1 || own > 1 || (own == 1 && ownIndex + 1 != anchorIndex))
                throw new InvalidOperationException("F5 interface layer anchor is unavailable or ambiguous.");
            if (own == 0) layers.Insert(anchorIndex, new LegacyGameInterfaceLayer(name, draw, InterfaceScaleType.UI));
        }

        private static bool F5PointerBegin() { return postfixContext == null || postfixContext.Shell == null || postfixContext.Shell.BeginPointerLayer(); }
        private static bool F5Window() { return postfixContext == null || postfixContext.Shell == null || postfixContext.Shell.DrawLayer(); }
        private static bool F5HoverGate() { return postfixContext == null || postfixContext.Shell == null || postfixContext.Shell.HoverGateLayer(); }
        private static bool F5PointerEnd() { return postfixContext == null || postfixContext.Shell == null || postfixContext.Shell.EndPointerLayer(); }

        private static void InsertBiomeLayer(List<GameInterfaceLayer> layers)
        {
            if (layers == null)
            {
                throw new InvalidOperationException("The Terraria interface layer list is unavailable.");
            }

            int anchorIndex = -1;
            int anchorCount = 0;
            int ownLayerCount = 0;
            for (int index = 0; index < layers.Count; index++)
            {
                GameInterfaceLayer layer = layers[index];
                string name = layer == null ? string.Empty : layer.Name;
                if (String.Equals(name, BiomeLayerAnchorName, StringComparison.Ordinal))
                {
                    anchorIndex = index;
                    anchorCount++;
                }
                if (String.Equals(name, BiomeLayerName, StringComparison.Ordinal))
                {
                    ownLayerCount++;
                }
            }

            if (anchorCount != 1 || ownLayerCount != 0)
            {
                throw new InvalidOperationException("The fixed Phase 0-T interface layer anchor is missing or ambiguous.");
            }

            layers.Insert(
                anchorIndex,
                new LegacyGameInterfaceLayer(
                    BiomeLayerName,
                    DrawBiomeDisplayLayer,
                    InterfaceScaleType.UI));
        }

        private static void EnsureBiomeLayerForHandoff(List<GameInterfaceLayer> layers)
        {
            if (layers == null)
            {
                // SetupDrawInterfaceLayers has not run yet; its exact postfix
                // remains the sole insertion path when setup happens later.
                return;
            }

            int anchorIndex = -1;
            int anchorCount = 0;
            int ownLayerIndex = -1;
            int ownLayerCount = 0;
            for (int index = 0; index < layers.Count; index++)
            {
                GameInterfaceLayer layer = layers[index];
                string name = layer == null ? string.Empty : layer.Name;
                if (String.Equals(name, BiomeLayerAnchorName, StringComparison.Ordinal))
                {
                    anchorIndex = index;
                    anchorCount++;
                }
                if (String.Equals(name, BiomeLayerName, StringComparison.Ordinal))
                {
                    ownLayerIndex = index;
                    ownLayerCount++;
                }
            }

            if (ownLayerCount == 1 &&
                anchorCount == 1 &&
                ownLayerIndex + 1 == anchorIndex)
            {
                return;
            }
            if (ownLayerCount != 0)
            {
                throw new InvalidOperationException(
                    "The Phase 0-T interface layer is duplicated or misplaced.");
            }

            InsertBiomeLayer(layers);
        }

        private static bool DrawBiomeDisplayLayer()
        {
            try
            {
                PostfixContext context = postfixContext;
                Phase0TBiomeRuntime runtime = context == null ? null : context.Runtime;
                BiomeDisplayViewModel viewModel = runtime == null ? null : runtime.CurrentViewModel;
                if (runtime == null ||
                    !runtime.FeatureEnabled ||
                    viewModel == null ||
                    !viewModel.Visible ||
                    String.IsNullOrEmpty(viewModel.Text))
                {
                    return true;
                }

                const float scale = 0.72f;
                const float lineBoxHeight = 40f * scale;
                int clientHeight = Terraria.Main.screenHeight;
                if (clientHeight <= 0)
                {
                    throw new InvalidOperationException("The Terraria client height is unavailable.");
                }

                float y = clientHeight * 0.45f;
                if (y + lineBoxHeight > clientHeight)
                {
                    y = Math.Max(0f, clientHeight - lineBoxHeight);
                }

                Terraria.Utils.DrawBorderString(
                    Terraria.Main.spriteBatch,
                    viewModel.Text,
                    new Vector2(20f, y),
                    new Color(144, 238, 144, 255),
                    scale);
            }
            catch
            {
                DisableBiomeFeature();
            }

            return true;
        }

        private static void DisableBiomeFeature()
        {
            Interlocked.Exchange(ref biomeFeatureFailed, 1);
            PostfixContext context = postfixContext;
            if (context != null)
            {
                context.FailRuntimeClosed();
            }
        }

        private static void CompleteEmptyHandoffOnce()
        {
            if (Interlocked.CompareExchange(ref handoffGate, 1, 0) != 0)
            {
                throw new InvalidOperationException("The Phase 0-S empty handoff was already consumed.");
            }
        }

        private sealed class PostfixContext
        {
            private Phase0TBiomeRuntime runtime;
            private ulong updateTick;

            internal PostfixContext(string packageId, string evidencePath)
            {
                PackageId = packageId;
                EvidencePath = evidencePath;
            }

            internal string PackageId { get; private set; }

            internal string EvidencePath { get; private set; }

            internal Phase0TBiomeRuntime Runtime
            {
                get { return runtime; }
            }

            internal F5Shell Shell { get; private set; }

            internal void InitializeRuntime(bool enabled)
            {
                if (runtime != null)
                {
                    throw new InvalidOperationException("The Phase 0-T runtime was already initialized.");
                }

                runtime = Phase0TBiomeRuntime.Create(enabled);
                Shell = new F5Shell(runtime) { LayersReady = f5LayersReady };
            }

            internal void UpdateRuntime()
            {
                Phase0TBiomeRuntime current = runtime;
                if (current == null)
                {
                    return;
                }

                current.Update(updateTick);
                updateTick = unchecked(updateTick + 1);
            }

            internal void FailRuntimeClosed()
            {
                Phase0TBiomeRuntime current = runtime;
                if (current != null)
                {
                    current.FailClosed();
                }
            }

            internal void UpdateShell() { if (Shell != null) Shell.AfterUpdate(); }
        }
    }

    internal sealed class RuntimeManifest
    {
        private static readonly string[] Keys =
        {
            "schemaVersion",
            "packageId",
            "sourceCommit",
            "targetAssemblySimpleName",
            "targetAssemblyVersion",
            "targetAssemblyMvid",
            "targetAssemblySha256",
            "reLogicAssemblySimpleName",
            "reLogicAssemblyVersion",
            "reLogicAssemblyPublicKeyToken",
            "reLogicAssemblyMvid",
            "reLogicResourceName",
            "reLogicResourceSha256",
            "targetTypeName",
            "targetMethodName",
            "targetMethodMetadataToken",
            "targetMethodIsStatic",
            "targetMethodReturnType",
            "targetMethodParameterCount",
            "targetMethodParameterType",
            "hostAssemblySimpleName",
            "hostAssemblyVersion",
            "hostAssemblyMvid",
            "hostAssemblySha256",
            "harmonyAssemblySimpleName",
            "harmonyAssemblyVersion",
            "harmonyAssemblyMvid",
            "harmonyAssemblySha256",
            "patchOwner",
            "evidenceFileName"
        };

        private RuntimeManifest(
            string packageId,
            string targetAssemblySimpleName,
            Version targetAssemblyVersion,
            Guid targetAssemblyMvid,
            string targetAssemblySha256,
            string reLogicAssemblySimpleName,
            Version reLogicAssemblyVersion,
            string reLogicAssemblyPublicKeyToken,
            Guid reLogicAssemblyMvid,
            string reLogicResourceName,
            string reLogicResourceSha256,
            string targetTypeName,
            string targetMethodName,
            int targetMethodMetadataToken,
            bool targetMethodIsStatic,
            string targetMethodReturnType,
            int targetMethodParameterCount,
            string targetMethodParameterType,
            string hostAssemblySimpleName,
            Version hostAssemblyVersion,
            Guid hostAssemblyMvid,
            string hostAssemblySha256,
            string harmonyAssemblySimpleName,
            Version harmonyAssemblyVersion,
            Guid harmonyAssemblyMvid,
            string harmonyAssemblySha256,
            string patchOwner,
            string evidenceFileName)
        {
            PackageId = packageId;
            TargetAssemblySimpleName = targetAssemblySimpleName;
            TargetAssemblyVersion = targetAssemblyVersion;
            TargetAssemblyMvid = targetAssemblyMvid;
            TargetAssemblySha256 = targetAssemblySha256;
            ReLogicAssemblySimpleName = reLogicAssemblySimpleName;
            ReLogicAssemblyVersion = reLogicAssemblyVersion;
            ReLogicAssemblyPublicKeyToken = reLogicAssemblyPublicKeyToken;
            ReLogicAssemblyMvid = reLogicAssemblyMvid;
            ReLogicResourceName = reLogicResourceName;
            ReLogicResourceSha256 = reLogicResourceSha256;
            TargetTypeName = targetTypeName;
            TargetMethodName = targetMethodName;
            TargetMethodMetadataToken = targetMethodMetadataToken;
            TargetMethodIsStatic = targetMethodIsStatic;
            TargetMethodReturnType = targetMethodReturnType;
            TargetMethodParameterCount = targetMethodParameterCount;
            TargetMethodParameterType = targetMethodParameterType;
            HostAssemblySimpleName = hostAssemblySimpleName;
            HostAssemblyVersion = hostAssemblyVersion;
            HostAssemblyMvid = hostAssemblyMvid;
            HostAssemblySha256 = hostAssemblySha256;
            HarmonyAssemblySimpleName = harmonyAssemblySimpleName;
            HarmonyAssemblyVersion = harmonyAssemblyVersion;
            HarmonyAssemblyMvid = harmonyAssemblyMvid;
            HarmonyAssemblySha256 = harmonyAssemblySha256;
            PatchOwner = patchOwner;
            EvidenceFileName = evidenceFileName;
        }

        internal string PackageId { get; private set; }

        internal string TargetAssemblySimpleName { get; private set; }

        internal Version TargetAssemblyVersion { get; private set; }

        internal Guid TargetAssemblyMvid { get; private set; }

        internal string TargetAssemblySha256 { get; private set; }

        internal string ReLogicAssemblySimpleName { get; private set; }

        internal Version ReLogicAssemblyVersion { get; private set; }

        internal string ReLogicAssemblyPublicKeyToken { get; private set; }

        internal Guid ReLogicAssemblyMvid { get; private set; }

        internal string ReLogicResourceName { get; private set; }

        internal string ReLogicResourceSha256 { get; private set; }

        internal string TargetTypeName { get; private set; }

        internal string TargetMethodName { get; private set; }

        internal int TargetMethodMetadataToken { get; private set; }

        internal bool TargetMethodIsStatic { get; private set; }

        internal string TargetMethodReturnType { get; private set; }

        internal int TargetMethodParameterCount { get; private set; }

        internal string TargetMethodParameterType { get; private set; }

        internal string HostAssemblySimpleName { get; private set; }

        internal Version HostAssemblyVersion { get; private set; }

        internal Guid HostAssemblyMvid { get; private set; }

        internal string HostAssemblySha256 { get; private set; }

        internal string HarmonyAssemblySimpleName { get; private set; }

        internal Version HarmonyAssemblyVersion { get; private set; }

        internal Guid HarmonyAssemblyMvid { get; private set; }

        internal string HarmonyAssemblySha256 { get; private set; }

        internal string PatchOwner { get; private set; }

        internal string EvidenceFileName { get; private set; }

        internal static RuntimeManifest Read(string path)
        {
            string[] values = StrictManifestReader.ReadValues(path, Keys);

            RequireExact(values[0], "2");
            RequirePackageId(values[1]);
            RequireCharacters(values[2], 40, IsLowerHex);
            RequireExact(values[3], "Terraria");
            RequireExact(values[4], "1.4.5.8");
            Guid targetMvid = ParseGuid(values[5]);
            RequireCharacters(values[6], 64, IsUpperHex);
            RequireExact(values[7], "ReLogic");
            RequireExact(values[8], "1.0.0.0");
            RequireExact(values[9], "null");
            Guid reLogicMvid = ParseGuid(values[10]);
            RequireExact(values[10], "ee258be9-88a4-423d-b3ce-84b6c35b141a");
            RequireExact(values[11], "Terraria.Libraries.ReLogic.ReLogic.dll");
            RequireExact(values[12], "E1C5DCCEFFF5FD1C789FF712BABFA1A305FCED0D03C96EF30F2C14D99AA0AF29");
            RequireExact(values[13], "Terraria.Main");
            RequireExact(values[14], "Update");
            int targetToken = ParseToken(values[15]);
            RequireExact(values[16], "false");
            RequireExact(values[17], "System.Void");
            RequireExact(values[18], "1");
            RequireExact(values[19], "Microsoft.Xna.Framework.GameTime");
            RequireExact(values[20], "JueMingR.TerrariaHost");
            RequireExact(values[21], "0.0.0.0");
            Guid hostMvid = ParseGuid(values[22]);
            RequireCharacters(values[23], 64, IsUpperHex);
            RequireExact(values[24], "0Harmony");
            RequireExact(values[25], "2.4.2.0");
            Guid harmonyMvid = ParseGuid(values[26]);
            RequireExact(values[26], "024a0e6e-c8c2-437e-ad04-7b6279389c23");
            RequireExact(
                values[27],
                "7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C");
            RequireExact(values[28], "JueMingR.Phase0S.MainUpdate");
            RequireExact(values[29], "phase-0-s-evidence.log");

            return new RuntimeManifest(
                values[1],
                values[3],
                new Version(values[4]),
                targetMvid,
                values[6],
                values[7],
                new Version(values[8]),
                values[9],
                reLogicMvid,
                values[11],
                values[12],
                values[13],
                values[14],
                targetToken,
                false,
                values[17],
                1,
                values[19],
                values[20],
                new Version(values[21]),
                hostMvid,
                values[23],
                values[24],
                new Version(values[25]),
                harmonyMvid,
                values[27],
                values[28],
                values[29]);
        }

        private static void RequireExact(string actual, string expected)
        {
            if (!String.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Phase 0-S runtime manifest has an invalid fixed value.");
            }
        }

        private static Guid ParseGuid(string value)
        {
            Guid result;
            if (!Guid.TryParseExact(value, "D", out result) ||
                !String.Equals(result.ToString("D"), value, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Phase 0-S runtime manifest has an invalid GUID.");
            }

            return result;
        }

        private static int ParseToken(string value)
        {
            uint result;
            if (value.Length != 10 || value[0] != '0' || value[1] != 'x')
            {
                throw new InvalidDataException("The Phase 0-S method token has an invalid format.");
            }

            for (int index = 2; index < value.Length; index++)
            {
                if (!IsUpperHex(value[index]))
                {
                    throw new InvalidDataException("The Phase 0-S method token has an invalid format.");
                }
            }

            if (!UInt32.TryParse(
                    value.Substring(2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out result) ||
                result > Int32.MaxValue)
            {
                throw new InvalidDataException("The Phase 0-S method token is invalid.");
            }

            return (int)result;
        }

        private static void RequirePackageId(string value)
        {
            if (value.Length < 1 || value.Length > 64)
            {
                throw new InvalidDataException("The Phase 0-S package id has an invalid length.");
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!IsAsciiLetterOrDigit(character) && character != '.' && character != '-')
                {
                    throw new InvalidDataException("The Phase 0-S package id has an invalid character.");
                }
            }
        }

        private static void RequireCharacters(string value, int length, Func<char, bool> predicate)
        {
            if (value.Length != length)
            {
                throw new InvalidDataException("The Phase 0-S runtime manifest field has an invalid length.");
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (!predicate(value[index]))
                {
                    throw new InvalidDataException("The Phase 0-S runtime manifest field has an invalid character.");
                }
            }
        }

        private static bool IsAsciiLetterOrDigit(char character)
        {
            return (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9');
        }

        private static bool IsLowerHex(char character)
        {
            return (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f');
        }

        private static bool IsUpperHex(char character)
        {
            return (character >= '0' && character <= '9') ||
                (character >= 'A' && character <= 'F');
        }
    }

    internal static class StrictManifestReader
    {
        internal static string[] ReadValues(string path, string[] expectedKeys)
        {
            RejectUtf8Bom(path);
            string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
            if (lines.Length != expectedKeys.Length)
            {
                throw new InvalidDataException("The Phase 0-S runtime manifest has an invalid line count.");
            }

            string[] values = new string[lines.Length];
            for (int index = 0; index < lines.Length; index++)
            {
                int separator = lines[index].IndexOf('=');
                if (separator <= 0 || separator != lines[index].LastIndexOf('=') ||
                    !String.Equals(
                        lines[index].Substring(0, separator),
                        expectedKeys[index],
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Phase 0-S runtime manifest has an invalid field.");
                }

                values[index] = lines[index].Substring(separator + 1);
            }

            return values;
        }

        private static void RejectUtf8Bom(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length >= 3 &&
                    stream.ReadByte() == 0xEF &&
                    stream.ReadByte() == 0xBB &&
                    stream.ReadByte() == 0xBF)
                {
                    throw new InvalidDataException("The Phase 0-S runtime manifest must not have a BOM.");
                }
            }
        }
    }

    internal static class AssemblyIdentity
    {
        private const int ReLogicResourceLength = 176128;

        internal static int CountLoaded(string simpleName)
        {
            int count = 0;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (String.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        internal static void VerifyFileNameAndVersion(
            string path,
            string expectedSimpleName,
            Version expectedVersion)
        {
            AssemblyName name = AssemblyName.GetAssemblyName(path);
            if (!String.Equals(name.Name, expectedSimpleName, StringComparison.Ordinal) ||
                !Equals(name.Version, expectedVersion))
            {
                throw new InvalidDataException("A Phase 0-S assembly file identity does not match.");
            }
        }

        internal static void VerifyFileHash(string path, string expectedSha256)
        {
            if (!String.Equals(ComputeSha256(path), expectedSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("A Phase 0-S assembly hash does not match.");
            }
        }

        internal static void VerifyLoaded(
            Assembly assembly,
            string expectedPath,
            string expectedSimpleName,
            Version expectedVersion,
            Guid expectedMvid,
            string expectedSha256)
        {
            string fullExpectedPath = Path.GetFullPath(expectedPath);
            string fullActualPath = Path.GetFullPath(assembly.Location);
            AssemblyName name = assembly.GetName();
            if (!String.Equals(fullActualPath, fullExpectedPath, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(name.Name, expectedSimpleName, StringComparison.Ordinal) ||
                !Equals(name.Version, expectedVersion) ||
                assembly.ManifestModule.ModuleVersionId != expectedMvid)
            {
                throw new InvalidDataException("A loaded Phase 0-S assembly identity does not match.");
            }

            VerifyFileHash(fullExpectedPath, expectedSha256);
        }

        internal static void VerifyUniqueLoaded(
            Assembly expectedAssembly,
            string expectedPath,
            string expectedSimpleName,
            Version expectedVersion,
            Guid expectedMvid,
            string expectedSha256)
        {
            int count = 0;
            Assembly matchedAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (String.Equals(assembly.GetName().Name, expectedSimpleName, StringComparison.Ordinal))
                {
                    count++;
                    matchedAssembly = assembly;
                }
            }

            if (count != 1 || !ReferenceEquals(matchedAssembly, expectedAssembly))
            {
                throw new InvalidDataException("A loaded Phase 0-S assembly is not unique.");
            }

            VerifyLoaded(
                expectedAssembly,
                expectedPath,
                expectedSimpleName,
                expectedVersion,
                expectedMvid,
                expectedSha256);
        }

        internal static void VerifyReLogicBinding(
            Assembly targetAssembly,
            Assembly reLogicAssembly,
            RuntimeManifest manifest)
        {
            AssemblyName name = reLogicAssembly.GetName();
            byte[] token = name.GetPublicKeyToken();
            string expectedFullName = manifest.ReLogicAssemblySimpleName + ", Version=" +
                manifest.ReLogicAssemblyVersion + ", Culture=neutral, PublicKeyToken=" +
                manifest.ReLogicAssemblyPublicKeyToken;
            if (!String.Equals(name.FullName, expectedFullName, StringComparison.Ordinal) ||
                !String.Equals(name.Name, manifest.ReLogicAssemblySimpleName, StringComparison.Ordinal) ||
                !Equals(name.Version, manifest.ReLogicAssemblyVersion) ||
                !String.Equals(manifest.ReLogicAssemblyPublicKeyToken, "null", StringComparison.Ordinal) ||
                (token != null && token.Length != 0) ||
                reLogicAssembly.ManifestModule.ModuleVersionId != manifest.ReLogicAssemblyMvid)
            {
                throw new InvalidDataException("The loaded ReLogic identity does not match.");
            }

            int count = 0;
            Assembly match = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (String.Equals(
                        assembly.GetName().Name,
                        manifest.ReLogicAssemblySimpleName,
                        StringComparison.Ordinal))
                {
                    count++;
                    match = assembly;
                }
            }

            if (count != 1 || !ReferenceEquals(match, reLogicAssembly))
            {
                throw new InvalidDataException("The loaded ReLogic assembly is not unique.");
            }

            using (Stream stream = targetAssembly.GetManifestResourceStream(manifest.ReLogicResourceName))
            {
                if (stream == null || stream.Length != ReLogicResourceLength ||
                    !String.Equals(ComputeSha256(stream), manifest.ReLogicResourceSha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Terraria ReLogic resource does not match.");
                }
            }
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("X2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static string ComputeSha256(Stream stream)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("X2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }

    internal static class EvidenceWriter
    {
        private static readonly string[] EventNames =
        {
            "TERRARIA_ASSEMBLY_READY",
            "HARMONY_READY",
            "HOOK_INSTALLED",
            "MAIN_UPDATE_POSTFIX_FIRED",
            "RUNTIME_HANDOFF_COMPLETE"
        };

        internal static void AppendEvent(string path, string packageId, int sequence, string eventName)
        {
            if (sequence < 2 || sequence > EventNames.Length ||
                !String.Equals(eventName, EventNames[sequence - 1], StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Phase 0-S evidence event is invalid.");
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                string[] lines = ReadAllLines(stream);
                if (lines.Length != sequence - 1)
                {
                    throw new InvalidDataException("The Phase 0-S evidence prefix has an invalid length.");
                }

                VerifySuccessPrefix(lines, packageId);
                stream.Position = stream.Length;
                WriteAndFlush(stream, FormatEvent(packageId, sequence, eventName));
            }
        }

        internal static void TryAppendError(
            string path,
            string packageId,
            string stage,
            string code,
            string exceptionType,
            string missingAssemblyIdentity,
            bool cleanupSecondary)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    string[] lines = ReadAllLines(stream);
                    int successCount = cleanupSecondary ? lines.Length - 1 : lines.Length;
                    if (successCount < 1 || successCount > 4)
                    {
                        return;
                    }

                    string[] successLines = new string[successCount];
                    Array.Copy(lines, successLines, successCount);
                    VerifySuccessPrefix(successLines, packageId);
                    if (cleanupSecondary &&
                        !IsPrimaryPatchError(lines[lines.Length - 1], packageId))
                    {
                        return;
                    }

                    stream.Position = stream.Length;
                    string line = String.Join(
                        "|",
                        "PHASE0S",
                        "1",
                        packageId,
                        "ERROR",
                        stage,
                        code,
                        exceptionType);
                    if (missingAssemblyIdentity != null)
                    {
                        line += "|" + missingAssemblyIdentity;
                    }

                    WriteAndFlush(
                        stream,
                        line);
                }
            }
            catch (Exception)
            {
                // Error evidence is best-effort and must never escape into the game.
            }
        }

        private static bool IsPrimaryPatchError(string line, string packageId)
        {
            string[] fields = line.Split('|');
            return (fields.Length == 7 || fields.Length == 8) &&
                String.Equals(fields[0], "PHASE0S", StringComparison.Ordinal) &&
                String.Equals(fields[1], "1", StringComparison.Ordinal) &&
                String.Equals(fields[2], packageId, StringComparison.Ordinal) &&
                String.Equals(fields[3], "ERROR", StringComparison.Ordinal) &&
                String.Equals(fields[4], "PATCH", StringComparison.Ordinal) &&
                String.Equals(fields[5], "PATCH_FAILED", StringComparison.Ordinal);
        }

        private static string FormatEvent(string packageId, int sequence, string eventName)
        {
            return String.Join(
                "|",
                "PHASE0S",
                "1",
                packageId,
                sequence.ToString("D2", CultureInfo.InvariantCulture),
                eventName,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteAndFlush(FileStream stream, string line)
        {
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false, true), 1024, true))
            {
                writer.WriteLine(line);
                writer.Flush();
                stream.Flush(true);
            }
        }

        private static string[] ReadAllLines(FileStream stream)
        {
            if (stream.Length >= 3)
            {
                int first = stream.ReadByte();
                int second = stream.ReadByte();
                int third = stream.ReadByte();
                if (first == 0xEF && second == 0xBB && third == 0xBF)
                {
                    throw new InvalidDataException("The Phase 0-S evidence must not have a BOM.");
                }
            }

            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 1024, true))
            {
                string contents = reader.ReadToEnd();
                if (contents.Length == 0)
                {
                    return new string[0];
                }

                string normalized = contents.Replace("\r\n", "\n");
                if (!normalized.EndsWith("\n", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Phase 0-S evidence line is incomplete.");
                }

                normalized = normalized.Substring(0, normalized.Length - 1);
                if (normalized.IndexOf('\r') >= 0)
                {
                    throw new InvalidDataException("The Phase 0-S evidence has invalid line endings.");
                }

                return normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            }
        }

        private static void VerifySuccessPrefix(string[] lines, string packageId)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                string[] fields = lines[index].Split('|');
                int threadId;
                DateTimeOffset timestamp;
                if (fields.Length != 7 ||
                    !String.Equals(fields[0], "PHASE0S", StringComparison.Ordinal) ||
                    !String.Equals(fields[1], "1", StringComparison.Ordinal) ||
                    !String.Equals(fields[2], packageId, StringComparison.Ordinal) ||
                    !String.Equals(fields[3], (index + 1).ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
                    !String.Equals(fields[4], EventNames[index], StringComparison.Ordinal) ||
                    !fields[5].EndsWith("Z", StringComparison.Ordinal) ||
                    !DateTimeOffset.TryParseExact(
                        fields[5],
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out timestamp) ||
                    timestamp.Offset != TimeSpan.Zero ||
                    !Int32.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out threadId) ||
                    threadId <= 0 ||
                    !String.Equals(fields[6], threadId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Phase 0-S evidence prefix is invalid.");
                }
            }
        }
    }
}
