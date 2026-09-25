using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Recovery
{
    public sealed class RecoveryCodec : IPreferenceCodec<RecoveryOptions>
    {
        private readonly int domain;
        private static readonly string[][] fields={new[]{"lifeMode","mana","noLife","noMana"},new[]{"buffs","followAdd","followRemove","allowedBuffs"},new[]{"nurse","furniture","tax"}};
        public RecoveryCodec(int domain){if(domain<0 || domain>2)throw new ArgumentOutOfRangeException(nameof(domain));this.domain=domain;}
        public RecoveryOptions Decode(byte[] bytes)
        {
            var root=PreferenceJson.Read(bytes,"JueMingR."+Name(domain),new[]{"format","version"}.Concat(fields[domain]).ToArray());
            if(domain==0){int mode=PreferenceJson.Integer(PreferenceJson.Required(root,"lifeMode","number"));if(mode<0 || mode>2)throw PreferenceJson.Invalid();return new RecoveryOptions(mode,Boolean(root,"mana"),noLife:Types(root,"noLife"),noMana:Types(root,"noMana"));}
            if(domain==1)return new RecoveryOptions(buffs:Boolean(root,"buffs"),followAdd:Boolean(root,"followAdd"),followRemove:Boolean(root,"followRemove"),allowedBuffs:Types(root,"allowedBuffs"));
            return new RecoveryOptions(nurse:Boolean(root,"nurse"),furniture:Boolean(root,"furniture"),tax:Boolean(root,"tax"));
        }
        private static bool Boolean(XElement root,string name){var e=PreferenceJson.Required(root,name,"boolean");if(e.HasElements || e.Value!="true" && e.Value!="false")throw PreferenceJson.Invalid();return e.Value=="true";}
        private static int[] Types(XElement root,string name)
        {
            var result=new HashSet<int>();foreach(var e in PreferenceJson.Required(root,name,"array").Elements())
            {if(e.Name!="item" || (string)e.Attribute("type")!="number")throw PreferenceJson.Invalid();int type=PreferenceJson.Integer(e);if(type<=0 || type>100000 || !result.Add(type) || result.Count>8192)throw PreferenceJson.Invalid();}return result.ToArray();
        }
        private static string B(bool v){return v?"true":"false";}
        private static string A(IEnumerable<int> v){return "["+string.Join(",",v.Select(x=>x.ToString(CultureInfo.InvariantCulture)))+"]";}
        public byte[] Encode(RecoveryOptions v)
        {
            string body=domain==0?"\"lifeMode\":"+v.LifeMode+",\"mana\":"+B(v.Mana)+",\"noLife\":"+A(v.NoLife)+",\"noMana\":"+A(v.NoMana):
                domain==1?"\"buffs\":"+B(v.Buffs)+",\"followAdd\":"+B(v.FollowAdd)+",\"followRemove\":"+B(v.FollowRemove)+",\"allowedBuffs\":"+A(v.AllowedBuffs):
                "\"nurse\":"+B(v.Nurse)+",\"furniture\":"+B(v.Furniture)+",\"tax\":"+B(v.Tax);
            byte[] bytes=new UTF8Encoding(false,true).GetBytes("{\"format\":\"JueMingR."+Name(domain)+"\",\"version\":1,"+body+"}\n");
            if(bytes.Length>PreferenceJson.MaximumBytes)throw PreferenceJson.Invalid();return bytes;
        }
        private static string Name(int d){return d==0?"RecoveryPotions":d==1?"RecoveryBuffs":"NearbyServices";}
    }
}
