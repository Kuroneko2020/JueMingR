using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.Recovery;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class RecoveryModeChecks
    {
        internal static void Check(List<string> failures)
        {
            var codec=new RecoveryCodec(0);
            foreach(int mode in new[]{1,2})
            {
                var off=new RecoveryOptions().Change(0,mode).Change(0,0);
                foreach(var value in new[]{off,off.Change(1,1),off.ChangeType(0,28,true),off.ChangeType(1,110,true),off.ClearBuffs()})
                {
                    int version;var loaded=codec.Decode(codec.Encode(value),out version);
                    if(version!=2 || loaded.LifeMode!=0 || loaded.LastLifeMode!=mode)failures.Add("Potion OFF/copy/reload lost selected mode "+mode);
                }
            }
            foreach(int mode in new[]{0,1,2})
            {
                int version;var value=codec.Decode(Encoding.UTF8.GetBytes(Legacy(mode)),out version);
                if(version!=1 || value.LifeMode!=mode || value.LastLifeMode!=(mode==0?1:mode) || !value.Mana || value.NoLife.Count!=1)
                    failures.Add("Known potion v1 migration lost mode or adjacent fields.");
            }
            string valid=Encoding.UTF8.GetString(codec.Encode(new RecoveryOptions(2).Change(0,0)));
            foreach(string invalid in new[]{valid.Replace("\"version\":2","\"version\":3"),valid.Replace("\"lastLifeMode\":2","\"lastLifeMode\":0"),
                valid.Replace("\"lastLifeMode\":2","\"lastLifeMode\":3"),valid.Replace("\"lastLifeMode\":2,",""),
                valid.Replace("\"lastLifeMode\":2","\"lastLifeMode\":2,\"lastLifeMode\":2"),valid.Replace("\"lifeMode\":0","\"lifeMode\":1"),
                valid.Replace("\"mana\":false","\"mana\":false,\"future\":true"),valid.Replace("{","{\"__type\":\"future\","),
                Legacy(0).Replace("\"mana\":true","\"mana\":true,\"lastLifeMode\":2")})
            {try{codec.Decode(Encoding.UTF8.GetBytes(invalid));failures.Add("Malformed/future potion memory accepted.");}catch(PreferenceFormatException){}}
            foreach(int domain in new[]{1,2})
            {int version;new RecoveryCodec(domain).Decode(new RecoveryCodec(domain).Encode(new RecoveryOptions()),out version);if(version!=1)failures.Add("Unrelated recovery domain schema changed.");}
        }
        private static string Legacy(int mode)
        {return "{\"format\":\"JueMingR.RecoveryPotions\",\"version\":1,\"lifeMode\":"+mode+",\"mana\":true,\"noLife\":[28],\"noMana\":[]}";}
    }
}
