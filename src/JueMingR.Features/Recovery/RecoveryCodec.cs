using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Recovery
{
    public sealed class RecoveryCodec : IPreferenceCodec<RecoveryOptions>
    {
        private readonly int domain;
        private static readonly string[][] fields={new[]{"lifeMode","mana","noLife","noMana"},new[]{"buffs","followAdd","followRemove","allowedBuffs"},new[]{"nurse","furniture","tax"}};
        public RecoveryCodec(int domain){if(domain<0 || domain>2)throw new ArgumentOutOfRangeException(nameof(domain));this.domain=domain;}
        public RecoveryOptions Decode(byte[] bytes){int ignored;return Decode(bytes,out ignored);}
        public RecoveryOptions Decode(byte[] bytes,out int sourceVersion)
        {
            sourceVersion=0;
            if(domain==0)return DecodePotions(bytes,out sourceVersion);
            var root=PreferenceJson.Read(bytes,"JueMingR."+Name(domain),new[]{"format","version"}.Concat(fields[domain]).ToArray());
            sourceVersion=1;
            if(domain==1)return new RecoveryOptions(buffs:Boolean(root,"buffs"),followAdd:Boolean(root,"followAdd"),followRemove:Boolean(root,"followRemove"),allowedBuffs:Types(root,"allowedBuffs"));
            return new RecoveryOptions(nurse:Boolean(root,"nurse"),furniture:Boolean(root,"furniture"),tax:Boolean(root,"tax"));
        }
        private static RecoveryOptions DecodePotions(byte[] bytes,out int sourceVersion)
        {
            sourceVersion=0;
            try
            {
                if(bytes==null || bytes.Length==0 || bytes.Length>PreferenceJson.MaximumBytes)throw PreferenceJson.Invalid();
                new UTF8Encoding(false,true).GetCharCount(bytes);
                var quotas=new XmlDictionaryReaderQuotas{MaxDepth=8,MaxStringContentLength=PreferenceJson.MaximumBytes,MaxArrayLength=32,MaxBytesPerRead=PreferenceJson.MaximumBytes,MaxNameTableCharCount=1024};
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,quotas))
                {
                    var root=XElement.Load(reader);
                    foreach(var node in root.DescendantsAndSelf())foreach(var attribute in node.Attributes())
                        if(attribute.Name!="type")throw new PreferenceFormatException(PreferenceStatus.UnknownFields,"Unknown potion fields are protected.");
                    var format=PreferenceJson.Required(root,"format","string");
                    if((string)root.Attribute("type")!="object" || format.HasElements || format.Value!="JueMingR.RecoveryPotions")throw PreferenceJson.Invalid();
                    int version=PreferenceJson.Integer(PreferenceJson.Required(root,"version","number"));
                    if(version!=1 && version!=2)throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion,"Unsupported potion version.");
                    PreferenceJson.ExactFields(root,version==1?new[]{"format","version","lifeMode","mana","noLife","noMana"}:new[]{"format","version","lifeMode","lastLifeMode","mana","noLife","noMana"});
                    int mode=PreferenceJson.Integer(PreferenceJson.Required(root,"lifeMode","number"));
                    // v1 has no history while OFF. Only that known format may
                    // default to fast; an active selection survives migration.
                    int last=version==1?(mode==0?1:mode):PreferenceJson.Integer(PreferenceJson.Required(root,"lastLifeMode","number"));
                    if(mode<0 || mode>2 || last<1 || last>2 || mode!=0 && mode!=last)throw PreferenceJson.Invalid();
                    var value=new RecoveryOptions(mode,Boolean(root,"mana"),noLife:Types(root,"noLife"),noMana:Types(root,"noMana"),lastLifeMode:last);
                    sourceVersion=version;return value;
                }
            }
            catch(PreferenceFormatException){throw;}
            catch(Exception e)when(e is XmlException || e is ArgumentException || e is InvalidOperationException){throw PreferenceJson.Invalid();}
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
            string body=domain==0?"\"lifeMode\":"+v.LifeMode+",\"lastLifeMode\":"+v.LastLifeMode+",\"mana\":"+B(v.Mana)+",\"noLife\":"+A(v.NoLife)+",\"noMana\":"+A(v.NoMana):
                domain==1?"\"buffs\":"+B(v.Buffs)+",\"followAdd\":"+B(v.FollowAdd)+",\"followRemove\":"+B(v.FollowRemove)+",\"allowedBuffs\":"+A(v.AllowedBuffs):
                "\"nurse\":"+B(v.Nurse)+",\"furniture\":"+B(v.Furniture)+",\"tax\":"+B(v.Tax);
            byte[] bytes=new UTF8Encoding(false,true).GetBytes("{\"format\":\"JueMingR."+Name(domain)+"\",\"version\":"+(domain==0?2:1)+","+body+"}\n");
            if(bytes.Length>PreferenceJson.MaximumBytes)throw PreferenceJson.Invalid();return bytes;
        }
        private static string Name(int d){return d==0?"RecoveryPotions":d==1?"RecoveryBuffs":"NearbyServices";}
    }
}
