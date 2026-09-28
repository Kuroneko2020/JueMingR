using System;
using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JueMingR.Platform.Information;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Information
{
    public sealed class InformationPreferenceCodec : IPreferenceCodec<InformationPreferences>
    {
        public InformationPreferences Decode(byte[] contents){int ignored;return Decode(contents,out ignored);}
        public InformationPreferences Decode(byte[] contents,out int sourceVersion)
        {
            sourceVersion=0;
            try
            {
                if(contents==null || contents.Length==0 || contents.Length>PreferenceJson.MaximumBytes)throw PreferenceJson.Invalid();
                new UTF8Encoding(false,true).GetCharCount(contents);
                var quotas=new XmlDictionaryReaderQuotas{MaxDepth=4,MaxArrayLength=32,MaxStringContentLength=PreferenceJson.MaximumBytes,MaxBytesPerRead=4096,MaxNameTableCharCount=1024};
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(contents,quotas))
                {
                    var root=XElement.Load(reader);
                    foreach(var node in root.DescendantsAndSelf())foreach(var attribute in node.Attributes())if(attribute.Name!="type")throw new PreferenceFormatException(PreferenceStatus.UnknownFields,"Unknown information fields are protected.");
                    var format=PreferenceJson.Required(root,"format","string");
                    if((string)root.Attribute("type")!="object" || format.HasElements || format.Value!="JueMingR.InformationDisplay")throw PreferenceJson.Invalid();
                    int version=Number(root,"version");
                    if(version!=1 && version!=2)throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion,"Unsupported information version.");
                    int enabled=Number(root,"enabled");InformationPreferences value;
                    if(version==1)
                    {
                        PreferenceJson.ExactFields(root,"format","version","enabled","biome","infection","luck","angler");
                        if((enabled&~14)!=0)throw new PreferenceFormatException(PreferenceStatus.UnknownFields,"Unknown legacy enabled bits are protected.");
                        value=new InformationPreferences(enabled,Style(root,"biome"),Style(root,"infection"),Style(root,"luck"),Style(root,"angler"));
                    }
                    else
                    {
                        PreferenceJson.ExactFields(root,"format","version","enabled","biome","infection","luck","angler","fullFish","filteredFish");
                        value=new InformationPreferences(enabled,Style(root,"biome"),Style(root,"infection"),Style(root,"luck"),Style(root,"angler"),Style(root,"fullFish"),Style(root,"filteredFish"));
                    }
                    sourceVersion=version;return value;
                }
            }
            catch(PreferenceFormatException){throw;}
            catch(Exception e) when(e is XmlException || e is ArgumentException || e is InvalidOperationException){throw PreferenceJson.Invalid();}
        }
        public byte[] Encode(InformationPreferences value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            string json = "{\"format\":\"JueMingR.InformationDisplay\",\"version\":2,\"enabled\":" + Integer(value.EnabledMask);
            string[] names = { "biome", "infection", "luck", "angler", "fullFish", "filteredFish" };
            for (int i = 0; i < names.Length; i++)
            {
                var style = value.Style((InformationKind)i);
                json += ",\"" + names[i] + "\":{\"rgb\":" + Integer(style.Rgb) + ",\"size\":" + Integer(style.Size) + "}";
            }
            return new UTF8Encoding(false, true).GetBytes(json + "}\n");
        }
        private static InformationStyle Style(XElement root, string name)
        {
            var item = PreferenceJson.Required(root, name, "object"); PreferenceJson.ExactFields(item, "rgb", "size");
            return new InformationStyle(Number(item, "rgb"), Number(item, "size"));
        }
        private static int Number(XElement root, string name) { return PreferenceJson.Integer(PreferenceJson.Required(root, name, "number")); }
        private static string Integer(int value) { return value.ToString(CultureInfo.InvariantCulture); }
    }
}
