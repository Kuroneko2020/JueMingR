using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;
using Terraria.IO;
using Terraria.Social;
using Terraria.Social.Base;

namespace JueMingR.TerrariaHost.Fishing
{
    internal static class PlayerRenameHooks
    {
        private static readonly Harmony harmony=new Harmony("JueMingR.PlayerRename");
        private static readonly HashSet<MethodInfo> cloudMethods=new HashSet<MethodInfo>();
        [ThreadStatic] private static SaveScope current;
        internal sealed class SaveScope
        {
            internal readonly PlayerFileData File;
            internal readonly Player Player;
            internal readonly string Path;
            internal readonly bool Cloud;
            internal readonly CloudSocialModule Provider;
            internal readonly long Generation;
            internal int Started,Completed,Depth,CloudWrites;
            internal bool Failed,CloudSucceeded;
            internal byte[] Candidate;
            internal SaveScope(PlayerFileData file,Player player,string path,bool cloud,CloudSocialModule provider,long generation)
            {File=file;Player=player;Path=path;Cloud=cloud;Provider=provider;Generation=generation;}
            internal bool Matches(Player player,long generation)
            {return ReferenceEquals(player,Player) && generation==Generation && ReferenceEquals(Main.ActivePlayerFileData,File) && ReferenceEquals(File.Player,Player) && File.Path==Path && File.IsCloudSave==Cloud && (!Cloud || ReferenceEquals(SocialAPI.Cloud,Provider));}
        }
        private sealed class CloudCall
        {internal SaveScope Scope;internal bool Target;}
        internal static void Install()
        {
            var target=typeof(Player).GetMethod("InternalSavePlayerFile",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(PlayerFileData)},null);
            if(target==null || target.ReturnType!=typeof(void) || target.GetMethodBody()==null)throw new MissingMethodException("native player save");
            harmony.Patch(target,prefix:new HarmonyMethod(typeof(PlayerRenameHooks),nameof(SaveBefore)),finalizer:new HarmonyMethod(typeof(PlayerRenameHooks),nameof(SaveFinal)));
        }
        internal static void EnsureCloud(CloudSocialModule provider)
        {
            var method=provider.GetType().GetMethod("Write",BindingFlags.Public|BindingFlags.Instance,null,new[]{typeof(string),typeof(byte[]),typeof(int)},null);
            if(method==null || method.IsAbstract || method.ReturnType!=typeof(bool) || method.GetMethodBody()==null)throw new MissingMethodException("native cloud write");
            if(cloudMethods.Contains(method))return;
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(PlayerRenameHooks),nameof(CloudBefore)),finalizer:new HarmonyMethod(typeof(PlayerRenameHooks),nameof(CloudFinal)));cloudMethods.Add(method);
        }
        internal static void Begin(SaveScope scope){if(current!=null)throw new InvalidOperationException("Nested rename.");current=scope;}
        internal static void End(SaveScope scope){if(ReferenceEquals(current,scope))current=null;}
        private static void SaveBefore(PlayerFileData __0,out SaveScope __state)
        {
            __state=null;var scope=current;if(scope==null || !ReferenceEquals(scope.File,__0))return;
            __state=scope;scope.Started++;scope.Depth++;
        }
        private static Exception SaveFinal(Exception __exception,bool __runOriginal,SaveScope __state)
        {if(__state!=null){__state.Depth--;if(__exception==null && __runOriginal)__state.Completed++;else __state.Failed=true;}return __exception;}
        private static void CloudBefore(CloudSocialModule __instance,string __0,byte[] __1,int __2,out CloudCall __state)
        {
            __state=null;var scope=current;
            if(scope==null || scope.Depth!=1 || !scope.Cloud || !ReferenceEquals(scope.Provider,__instance) || __0!=scope.Path && __0!=scope.Path+".bak")return;
            __state=new CloudCall{Scope=scope,Target=__0==scope.Path};if(!__state.Target)return;
            scope.CloudWrites++;
            try{if(__1==null || __2<=0 || __2>__1.Length || __2>64*1024*1024){scope.Failed=true;return;}scope.Candidate=new byte[__2];Array.Copy(__1,scope.Candidate,__2);}
            catch(Exception){scope.Failed=true;}
        }
        private static Exception CloudFinal(Exception __exception,bool __runOriginal,bool __result,CloudCall __state)
        {
            if(__state!=null){bool ok=__exception==null && __runOriginal && __result;if(!ok)__state.Scope.Failed=true;if(__state.Target)__state.Scope.CloudSucceeded=ok;}
            return __exception;
        }
        internal static void Uninstall(){foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);cloudMethods.Clear();current=null;}
    }
}
