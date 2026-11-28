using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Web.Script.Serialization;
using JueMingR.Platform.Llm;

namespace JueMingR.Infrastructure.Llm
{
    internal static class LlmJson
    {
        public const int MaximumBytes = 4 * 1024 * 1024;
        private static JavaScriptSerializer NewSerializer() =>
            new JavaScriptSerializer { MaxJsonLength = MaximumBytes, RecursionLimit = 48 };

        public static object Parse(string json)
        {
            if (json == null || json.Length > MaximumBytes) throw Protocol();
            var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 48, MaxStringContentLength = MaximumBytes };
            // JavaScriptSerializer itself accepts duplicate object members. A second structural pass prevents
            // ambiguous tool fields and preserves unknown-field/write-protection semantics in local documents.
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), quotas))
            {
                reader.MoveToContent();
                ReadUnique(reader);
                if (reader.MoveToContent() != XmlNodeType.None) throw Protocol();
            }
            return NewSerializer().DeserializeObject(json);
        }

        private static void ReadUnique(XmlReader reader)
        {
            string type = reader.GetAttribute("type");
            if (type != "object" && type != "array")
            {
                reader.Skip();
                return;
            }
            var names = type == "object" ? new HashSet<string>(StringComparer.Ordinal) : null;
            bool empty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (empty) return;
            while (reader.MoveToContent() == XmlNodeType.Element)
            {
                if (names != null && !names.Add(reader.GetAttribute("item") ?? reader.LocalName))
                    throw Protocol();
                ReadUnique(reader);
            }
            reader.ReadEndElement();
        }
        public static string Write(object value) => NewSerializer().Serialize(value);
        public static T Convert<T>(object value) => NewSerializer().ConvertToType<T>(value);

        public static Dictionary<string, object> Map(object value) => value as Dictionary<string, object>;
        public static Dictionary<string, object> RequireMap(object value) => Map(value) ?? throw Protocol();
        public static object Get(object container, string key) =>
            Map(container) is Dictionary<string, object> map && map.TryGetValue(key, out object value) ? value : null;
        public static string Text(object container, string key) => Get(container, key) as string;
        public static bool Flag(object container, string key) => Get(container, key) is bool value && value;
        public static int Number(object container, string key, int fallback = -1) =>
            Get(container, key) is int value ? value : fallback;
        public static bool Has(object container, string key) => Map(container)?.ContainsKey(key) == true;

        public static object[] Array(object value)
        {
            if (value is object[] array) return array;
            return value is IList list ? list.Cast<object>().ToArray() : System.Array.Empty<object>();
        }

        public static string[] Strings(object value) => Array(value).OfType<string>().ToArray();
        public static LlmException Protocol() => new LlmException(LlmFailure.Protocol, "模型返回的数据与所选协议不符。");
        public static LlmException Incomplete() => new LlmException(LlmFailure.Incomplete, "模型未完整结束本次回复，未提交新的工具动作。");
    }
}
