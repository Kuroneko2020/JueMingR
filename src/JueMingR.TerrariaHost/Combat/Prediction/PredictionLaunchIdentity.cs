using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Build-time component hashes are part of the Host already authenticated
    // by the existing loader. No untrusted adjacent manifest can replace them.
    internal sealed class PredictionLaunchIdentity
    {
        internal readonly string Directory,GamePath,CacheDirectory;
        internal readonly string[] Hashes;
        internal PredictionLaunchIdentity(string authenticatedHostHash,string gameDirectory)
        {
            if(authenticatedHostHash==null || authenticatedHostHash.Length!=64)throw new ArgumentException("Authenticated Host identity required.");
            var assembly=typeof(PredictionLaunchIdentity).Assembly;Directory=Path.GetDirectoryName(assembly.Location);
            CacheDirectory=Path.Combine(gameDirectory,"JueMingRData","cache","npc-prediction");
            GamePath=typeof(Terraria.Main).Assembly.Location;Hashes=new string[PredictionWorkerClient.PayloadNames.Length];
            var metadata=assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Where(a=>a.Key.StartsWith("Prediction.File.",StringComparison.Ordinal)).ToDictionary(a=>a.Key,a=>a.Value,StringComparer.Ordinal);
            for(int i=0;i<Hashes.Length;i++)
            {
                if(i==2){Hashes[i]=authenticatedHostHash;continue;}
                if(i==6){Hashes[i]="7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C";continue;}
                if(!metadata.TryGetValue("Prediction.File."+PredictionWorkerClient.PayloadNames[i],out Hashes[i]))throw new InvalidDataException("Prediction build identity absent.");
            }
        }
        internal PredictionWorkerClient Start(){return new PredictionWorkerClient(Directory,GamePath,Hashes,60000,5000,CacheDirectory);}
    }
}
