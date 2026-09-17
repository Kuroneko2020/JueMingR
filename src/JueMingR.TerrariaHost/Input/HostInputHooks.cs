using System;
using System.Reflection;
using HarmonyLib;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Input
{
    internal static class HostInputHooks
    {
        private static HostInputState state;
        internal static MethodInfo[] Resolve(Assembly target)
        {
            return new[] { Exact(target, "Terraria.FocusHelper", "get_AllowInputProcessing", typeof(bool)),
                Exact(target, "Terraria.GameInput.PlayerInput", "UpdateInput", typeof(void)),
                Exact(target, "Terraria.Main", "GetInputText", typeof(string), typeof(string), typeof(bool)),
                Exact(target, "Terraria.GameInput.PlayerInput", "MouseInput", typeof(void)),
                Exact(target, "Terraria.GameInput.KeyConfiguration", "Processkey", typeof(void),
                    target.GetType("Terraria.GameInput.TriggersSet", true), typeof(string), target.GetType("Terraria.GameInput.InputMode", true)) };
        }
        private static MethodInfo Exact(Assembly target, string type, string name, Type result, params Type[] parameters)
        {
            MethodInfo method = target.GetType(type, true).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                null, parameters, null);
            if (method == null || method.ReturnType != result || method.IsGenericMethod || method.GetMethodBody() == null)
                throw new MissingMethodException("host-input-abi:" + type + "." + name);
            return method;
        }
        internal static void Install(Harmony harmony, MethodInfo[] targets, HostInputState input)
        {
            state = input;
            string[] names = { "PermissionPostfix", "MappingPostfix", "TextPrefix", "MousePostfix", "KeyPostfix" };
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
        private static void MousePostfix(System.Collections.Generic.List<string> ___MouseKeys) { if (state != null) state.AfterNativeMouse(___MouseKeys); }
        private static void KeyPostfix(KeyConfiguration __instance, TriggersSet __0, string __1, InputMode __2)
        { if (state != null) state.UseGesture.ObserveMapping(__instance, __0, __1, __2); }
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
