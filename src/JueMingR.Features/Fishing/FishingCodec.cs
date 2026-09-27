using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Fishing
{
    public sealed class FishingCodec : IPreferenceCodec<FishingOptions>
    {
        // A byte safety boundary, never a silent list/preset truncation. It is
        // independent of the 96 visible candidates and accommodates the full
        // native catalog in all scopes. Oversize writes fail before storage.
        public const int MaximumBytes=4*1024*1024;
        private static readonly string[] Fields={"format","version","auto","loadout","equipment","cut","storeMode","lastStoreMode","filterMode","lastFilterMode","match","crates","quests","npcs","lists","presets"};
        public FishingOptions Decode(byte[] bytes)
        {
            try
            {
                if(bytes==null || bytes.Length==0 || bytes.Length>MaximumBytes)throw PreferenceJson.Invalid();
                new UTF8Encoding(false,true).GetCharCount(bytes);
                var quotas=new XmlDictionaryReaderQuotas{MaxDepth=12,MaxStringContentLength=MaximumBytes,MaxArrayLength=MaximumBytes,MaxBytesPerRead=MaximumBytes,MaxNameTableCharCount=2048};
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,quotas))
                {
                    var root=XElement.Load(reader);
                    foreach(var node in root.DescendantsAndSelf())foreach(var attr in node.Attributes())
                        if(attr.Name!="type")throw new PreferenceFormatException(PreferenceStatus.UnknownFields,"Unknown fishing fields.");
                    if((string)root.Attribute("type")!="object" || Text(root,"format")!="JueMingR.Fishing")throw PreferenceJson.Invalid();
                    if(Number(root,"version")!=1)throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion,"Unsupported fishing version.");
                    PreferenceJson.ExactFields(root,Fields);
                    int store=Number(root,"storeMode"),lastStore=Number(root,"lastStoreMode"),mode=Number(root,"filterMode"),lastMode=Number(root,"lastFilterMode");
                    if(store!=0 && store!=lastStore || mode!=0 && mode!=lastMode)throw PreferenceJson.Invalid();
                    var lists=Array(root,"lists").Select(ReadList).ToArray();
                    var presets=Array(root,"presets").Select(e=>
                    {
                        Object(e,"name","mode","match","content");
                        return new FishPreset(Number(e,"mode"),Number(e,"match"),ReadList(PreferenceJson.Required(e,"content","object")),Text(e,"name"));
                    }).ToArray();
                    return new FishingOptions(Boolean(root,"auto"),Boolean(root,"loadout"),Boolean(root,"equipment"),Boolean(root,"cut"),store,lastStore,mode,lastMode,
                        Number(root,"match"),Number(root,"crates"),Number(root,"quests"),Number(root,"npcs"),lists,presets);
                }
            }
            catch(PreferenceFormatException){throw;}
            catch(Exception e)when(e is ArgumentException || e is XmlException || e is InvalidOperationException){throw PreferenceJson.Invalid();}
        }
        private static void Object(XElement e,params string[] fields)
        {if((string)e.Attribute("type")!="object")throw PreferenceJson.Invalid();PreferenceJson.ExactFields(e,fields);}
        private static System.Collections.Generic.IEnumerable<XElement> Array(XElement root,string name)
        {
            var value=PreferenceJson.Required(root,name,"array");
            foreach(var e in value.Elements()){if(e.Name!="item")throw PreferenceJson.Invalid();yield return e;}
        }
        private static int Number(XElement e,string name){return PreferenceJson.Integer(PreferenceJson.Required(e,name,"number"));}
        private static string Text(XElement e,string name){var value=PreferenceJson.Required(e,name,"string");if(value.HasElements)throw PreferenceJson.Invalid();return value.Value;}
        private static bool Boolean(XElement e,string name)
        {var value=PreferenceJson.Required(e,name,"boolean");if(value.HasElements || value.Value!="true" && value.Value!="false")throw PreferenceJson.Invalid();return value.Value=="true";}
        private static FishList ReadList(XElement e)
        {
            Object(e,"exact","keywords");
            var exact=Array(e,"exact").Select(x=>{Object(x,"kind","id");return new FishKey((FishKind)Number(x,"kind"),Number(x,"id"));}).ToArray();
            var words=Array(e,"keywords").Select(x=>{if((string)x.Attribute("type")!="string" || x.HasElements)throw PreferenceJson.Invalid();return x.Value;}).ToArray();
            return new FishList(exact,words);
        }
        private static XElement Value(string name,string type,object content){return new XElement(name,new XAttribute("type",type),content);}
        private static XElement N(string name,int value){return Value(name,"number",value.ToString(CultureInfo.InvariantCulture));}
        private static XElement B(string name,bool value){return Value(name,"boolean",value?"true":"false");}
        private static XElement WriteList(string name,FishList list)
        {return Value(name,"object",new object[]{Value("exact","array",list.Exact.Select(k=>Value("item","object",new[]{N("kind",(int)k.Kind),N("id",k.Id)}))),Value("keywords","array",list.Keywords.Select(w=>Value("item","string",w)))});}
        public byte[] Encode(FishingOptions value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            var root=Value("root","object",new object[]{Value("format","string","JueMingR.Fishing"),N("version",1),B("auto",value.Auto),B("loadout",value.Loadout),B("equipment",value.Equipment),B("cut",value.Cut),
                N("storeMode",value.StoreMode),N("lastStoreMode",value.LastStoreMode),N("filterMode",value.FilterMode),N("lastFilterMode",value.LastFilterMode),N("match",value.Match),N("crates",value.Crates),N("quests",value.Quests),N("npcs",value.Npcs),
                Value("lists","array",Enumerable.Range(0,4).Select(i=>WriteList("item",value.List(i/2+1,i%2)))),
                Value("presets","array",value.Presets.Select(p=>Value("item","object",new object[]{Value("name","string",p.Name),N("mode",p.Mode),N("match",p.Match),WriteList("content",p.Content)})))});
            using(var stream=new MemoryStream())
            {
                using(var writer=JsonReaderWriterFactory.CreateJsonWriter(stream,new UTF8Encoding(false,true),false)){root.WriteTo(writer);writer.Flush();}
                if(stream.Length>MaximumBytes)throw PreferenceJson.Invalid();return stream.ToArray();
            }
        }
    }
}
