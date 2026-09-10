using System;
using System.Reflection;
using HarmonyLib;

namespace JueMingR.TerrariaHost.Input
{
    internal static class HostInputHooks
    {
        private static HostInputState state;
        internal static MethodInfo[] Resolve(Assembly target)
        {
            return new[] { Exact(target, "Terraria.FocusHelper", "get_AllowInputProcessing", typeof(bool)),
                Exact(target, "Terraria.GameInput.PlayerInput", "UpdateInput", typeof(void)),
                Exact(target, "Terraria.Main", "GetInputText", typeof(string), typeof(string), typeof(bool)) };
        }
        private static MethodInfo Exact(Assembly target, string type, string name, Type result, params Type[] parameters)
        {
            MethodInfo method = target.GetType(type, true).GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                null, parameters, null);
            if (method == null || method.ReturnType != result || method.IsGenericMethod || method.GetMethodBody() == null)
                throw new MissingMethodException("host-input-abi:" + type + "." + name);
            return method;
        }
        internal static void Install(Harmony harmony, MethodInfo[] targets, HostInputState input)
        {
            state = input;
            string[] names = { "PermissionPostfix", "MappingPostfix", "TextPrefix" };
            for (int i = 0; i < targets.Length; i++)
            {
                MethodInfo patch = typeof(HostInputHooks).GetMethod(names[i], BindingFlags.NonPublic | BindingFlags.Static);
                bool prefix = i == 2;
                if (prefix) harmony.Patch(targets[i], prefix: new HarmonyMethod(patch));
                else harmony.Patch(targets[i], postfix: new HarmonyMethod(patch));
                Patches info = Harmony.GetPatchInfo(targets[i]);
                if (info == null || info.Owners.Count != 1 || info.Owners[0] != harmony.Id || info.Postfixes.Count != (prefix ? 0 : 1) ||
                    info.Prefixes.Count != (prefix ? 1 : 0) || (prefix ? info.Prefixes[0] : info.Postfixes[0]).PatchMethod != patch || info.Transpilers.Count != 0 ||
                    info.Finalizers.Count != 0 || info.InnerPrefixes.Count != 0 || info.InnerPostfixes.Count != 0)
                    throw new InvalidOperationException("host-input-patch-ownership");
            }
        }
        private static void PermissionPostfix(ref bool __result) { if (state != null) __result = state.RestrictNativePermission(__result); }
        private static void MappingPostfix() { if (state != null) state.AfterMapping(); }
        private static bool TextPrefix(string oldString, ref string __result)
        {
            // .8 chat/menu/sign consumers call after DoUpdate_HandleInput's
            // final sample (or in Draw). The original reads Keyboard.GetState
            // directly, so an inlined focus getter must not bypass this gate.
            if (state == null || state.CanUseInput) return true;
            HostInputState.ClearTextActions();
            __result = oldString;
            return false;
        }
    }
}
