using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JueMingR.Setup;

// Isolated file/service admission contracts. The synthetic version exercises
// metadata policy only; no claim of real cross-version gameplay compatibility.
internal static class CompatibilityAdmissionChecks
{
    internal static void Run(string root,string terraria,string bootstrapPath,string hostPath,SetupPayload payload)
    {
        string area=Path.Combine(root,"compatibility-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(area);
        string altered=Path.Combine(area,"different-hash");Directory.CreateDirectory(altered);
        string alteredExe=Path.Combine(altered,"Terraria.exe");File.Copy(terraria,alteredExe);
        using(var bytes=new FileStream(alteredExe,FileMode.Append,FileAccess.Write,FileShare.None))bytes.WriteByte(0x52);
        Require(SetupPayload.FileHash(alteredExe)!=TargetSafety.TerrariaHash,"changed real-client file differs from reference hash");
        InstallAndRemove(altered,payload);

        string future=Path.Combine(area,"unverified-version");Directory.CreateDirectory(future);
        var name=new AssemblyName("Terraria"){Version=new Version(9,8,7,6)};
        var assembly=AppDomain.CurrentDomain.DefineDynamicAssembly(name,AssemblyBuilderAccess.Save,future);
        var module=assembly.DefineDynamicModule("Terraria.exe","Terraria.exe");var main=module.DefineType("Terraria.Main",TypeAttributes.Public);
        var entry=main.DefineMethod("Main",MethodAttributes.Static|MethodAttributes.Public,typeof(void),Type.EmptyTypes);entry.GetILGenerator().Emit(OpCodes.Ret);
        main.CreateType();assembly.SetEntryPoint(entry,PEFileKinds.WindowApplication);assembly.Save("Terraria.exe",PortableExecutableKinds.ILOnly|PortableExecutableKinds.Required32Bit,ImageFileMachine.I386);
        Require(AssemblyName.GetAssemblyName(Path.Combine(future,"Terraria.exe")).Version==name.Version,"unverified version fixture is on disk");
        InstallAndRemove(future,payload);

        // A separate declaration supplies the actual supported update ABI with
        // a different token, version and MVID. It is not a substitute game.
        string abi=Path.Combine(area,"unverified-abi");Directory.CreateDirectory(abi);
        var abiAssembly=AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("Terraria"){Version=new Version(9,8,7,5)},AssemblyBuilderAccess.Save,abi);
        var abiModule=abiAssembly.DefineDynamicModule("Terraria.exe","Terraria.exe");var abiMain=abiModule.DefineType("Terraria.Main",TypeAttributes.Public);
        var abiEntry=abiMain.DefineMethod("Main",MethodAttributes.Public|MethodAttributes.Static,typeof(void),Type.EmptyTypes);abiEntry.GetILGenerator().Emit(OpCodes.Ret);
        var gameTime=Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(terraria),"Microsoft.Xna.Framework.Game.dll")).GetType("Microsoft.Xna.Framework.GameTime",true);
        abiMain.DefineMethod("Update",MethodAttributes.Family,typeof(void),new[]{gameTime}).GetILGenerator().Emit(OpCodes.Ret);
        abiMain.CreateType();abiAssembly.SetEntryPoint(abiEntry,PEFileKinds.WindowApplication);abiAssembly.Save("Terraria.exe",PortableExecutableKinds.ILOnly|PortableExecutableKinds.Required32Bit,ImageFileMachine.I386);
        foreach(string sample in new[]{altered,future,abi})
        {
            var domain=AppDomain.CreateDomain("compatibility-"+Guid.NewGuid().ToString("N"),null,new AppDomainSetup{ApplicationBase=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)});
            try
            {
                var checks=(CompatibilityDomainChecks)domain.CreateInstanceFromAndUnwrap(Assembly.GetExecutingAssembly().Location,typeof(CompatibilityDomainChecks).FullName);
                checks.Run(Path.Combine(sample,"Terraria.exe"),Path.GetDirectoryName(terraria),bootstrapPath,hostPath,sample!=future);
            }
            finally{AppDomain.Unload(domain);}
        }

        string foreign=Path.Combine(area,"foreign");Directory.CreateDirectory(foreign);File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(foreign,"Terraria.exe"));
        Require(new InstallService(()=>payload,null).Execute("install",foreign).exitCode==2 && !File.Exists(Path.Combine(foreign,"JueMingR.dll")),"renaming a different managed EXE does not make it Terraria");
        string broken=Path.Combine(area,"broken");Directory.CreateDirectory(broken);File.WriteAllText(Path.Combine(broken,"Terraria.exe"),"not PE");
        Require(new InstallService(()=>payload,null).Execute("install",broken).exitCode==2 && !File.Exists(Path.Combine(broken,"JueMingR.dll")),"invalid PE rejected before installing payload");
        string architecture=Path.Combine(area,"wrong-architecture");Directory.CreateDirectory(architecture);
        var wide=AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("Terraria"){Version=new Version(9,8,7,4)},AssemblyBuilderAccess.Save,architecture);
        var wideModule=wide.DefineDynamicModule("Terraria.exe","Terraria.exe");var wideMain=wideModule.DefineType("Terraria.Main",TypeAttributes.Public);
        var wideEntry=wideMain.DefineMethod("Main",MethodAttributes.Public|MethodAttributes.Static,typeof(void),Type.EmptyTypes);wideEntry.GetILGenerator().Emit(OpCodes.Ret);
        wideMain.CreateType();wide.SetEntryPoint(wideEntry,PEFileKinds.WindowApplication);wide.Save("Terraria.exe",PortableExecutableKinds.ILOnly|PortableExecutableKinds.PE32Plus,ImageFileMachine.AMD64);
        Require(new InstallService(()=>payload,null).Execute("install",architecture).exitCode==2 && !File.Exists(Path.Combine(architecture,"JueMingR.dll")),"incompatible architecture rejected before installing payload");
        Console.WriteLine("PASS compatibility admission: changed real-client hash and unverified version install/remove in isolated directories; foreign/invalid EXE refused; no game execution.");
    }
    private static void InstallAndRemove(string directory,SetupPayload payload)
    {
        string game=Path.Combine(directory,"Terraria.exe"),before=SetupPayload.FileHash(game);var service=new InstallService(()=>payload,null);
        Require(service.Execute("install",directory).exitCode==0,"unverified client admitted by real install service");
        Require(SetupPayload.FileHash(Path.Combine(directory,"JueMingR.dll"))==payload.DllHash && SetupPayload.FileHash(Path.Combine(directory,"Terraria.exe.config"))==payload.ConfigHash,"installed exact owned payload pair");
        Require(service.Execute("uninstall",directory).exitCode==0 && !File.Exists(Path.Combine(directory,"JueMingR.dll")) && !File.Exists(Path.Combine(directory,"Terraria.exe.config")),"same owned payload removable on unverified client");
        Require(SetupPayload.FileHash(game)==before,"game file preserved by install/removal");
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

public sealed class CompatibilityDomainChecks : MarshalByRefObject
{
    private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    public void Run(string gamePath,string references,string bootstrapPath,string hostPath,bool hasUpdate)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>
        {
            string simple=new AssemblyName(args.Name).Name;
            if(simple=="Terraria")return AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Terraria");
            // Native reference resolution is confined to the explicitly
            // supplied reference directory. Do not guess product build trees.
            string candidate=Path.Combine(references,simple+".dll");
            if(File.Exists(candidate))return Assembly.LoadFrom(candidate);
            return null;
        };
        var game=Assembly.LoadFrom(gamePath);var bootstrap=Assembly.LoadFrom(bootstrapPath);var host=Assembly.LoadFrom(hostPath);
        foreach(var product in new[]{bootstrap,host})
        {
            string prefix=product==bootstrap?"JueMingR.Bootstrap.":"JueMingR.TerrariaHost.";
            var identity=product.GetType(prefix+"AssemblyIdentity",true);var admission=identity.GetMethod("VerifyGameTarget",Flags);
            admission.Invoke(null,new object[]{game,gamePath});
            ExpectFailure(()=>admission.Invoke(null,new object[]{game,Path.Combine(Path.GetDirectoryName(gamePath),"other.exe")}),"wrong live file path");
            ExpectFailure(()=>identity.GetMethod("VerifyFileHash",Flags).Invoke(null,new object[]{product.Location,new string('0',64)}),"own payload hash mismatch remains rejected");
            if(game.GetManifestResourceNames().Any(n=>n.EndsWith(".ReLogic.dll",StringComparison.Ordinal)))
            {
                string resource=game.GetManifestResourceNames().Single(n=>n.EndsWith(".ReLogic.dll",StringComparison.Ordinal));Assembly dependency;
                using(var stream=game.GetManifestResourceStream(resource))using(var bytes=new MemoryStream()){stream.CopyTo(bytes);dependency=Assembly.Load(bytes.ToArray());}
                identity.GetMethod("VerifyGameReLogicSource",Flags).Invoke(null,new object[]{game,dependency});
                ExpectFailure(()=>identity.GetMethod("VerifyGameReLogicSource",Flags).Invoke(null,new object[]{game,bootstrap}),"foreign game dependency object");
            }
        }
        var manifest=host.GetType("JueMingR.TerrariaHost.RuntimeManifest",true);
        var value=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(manifest);
        foreach(var pair in new[]{new object[]{"TargetTypeName","Terraria.Main"},new object[]{"TargetMethodName","Update"},new object[]{"TargetMethodMetadataToken",0x06000001},
            new object[]{"TargetMethodIsStatic",false},new object[]{"TargetMethodReturnType","System.Void"},new object[]{"TargetMethodParameterCount",1},new object[]{"TargetMethodParameterType","Microsoft.Xna.Framework.GameTime"}})
            manifest.GetField("<"+pair[0]+">k__BackingField",Flags).SetValue(value,pair[1]);
        var resolve=host.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker",true).GetMethod("ResolveTargetMethod",Flags);
        if(hasUpdate)
        {
            var update=(MethodInfo)resolve.Invoke(null,new[]{value,(object)game});
            if(update.DeclaringType.Assembly!=game || update.MetadataToken==0x06000001)throw new InvalidOperationException("Actual update declaration/token not selected.");
        }
        else ExpectFailure(()=>resolve.Invoke(null,new[]{value,(object)game}),"missing actual update ABI");
        Console.WriteLine("PASS compatibility live-object/ABI contracts version="+game.GetName().Version+" actualUpdate="+hasUpdate+"; no game methods executed.");
    }
    private static void ExpectFailure(Action action,string reason)
    {
        try{action();}catch(TargetInvocationException error)when(error.InnerException is InvalidDataException || error.InnerException is InvalidOperationException){return;}
        throw new InvalidOperationException("Expected compatibility rejection: "+reason);
    }
}
