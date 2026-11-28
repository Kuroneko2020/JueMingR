using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JueMingR.Platform.Llm;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    public static class LlmPresetCatalog
    {
        public static IReadOnlyList<LlmPreset> Load()
        {
            using (var stream = typeof(LlmPresetCatalog).Assembly.GetManifestResourceStream("JueMingR.Llm.provider_presets.json"))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false, true)))
            {
                object document = Parse(reader.ReadToEnd());
                if (Number(document, "version") != 1)
                    throw new InvalidOperationException("Unknown LLM preset format.");
                var presets = Array(Get(document, "providers")).Select(row => Decode(row, Text(document, "verifiedAt"))).ToArray();
                if (presets.Any(p => string.IsNullOrEmpty(p.Id)) || presets.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != presets.Length)
                    throw new InvalidOperationException("Non-unique LLM preset identifiers.");
                return System.Array.AsReadOnly(presets);
            }
        }

        private static LlmPreset Decode(object row, string verifiedAt)
        {
            object auth = Get(row, "auth");
            object discovery = Get(row, "modelsDiscovery");
            object plan = Get(row, "billingPlan");
            object policy = Get(row, "usagePolicy");
            object suggestions = Get(row, "modelSuggestions");
            string region = Text(row, "region");
            string[] aliases = Strings(Get(row, "aliases"));
            return new LlmPreset
            {
                Id = Text(row, "id"), Label = Text(row, "label"), Provider = Text(row, "provider"),
                Region = region, RegionLabel = Region(region), PlanId = Text(plan, "id"), PlanLabel = Text(plan, "label"),
                Policy = Text(policy, "status"), VerifiedAt = Text(suggestions, "verifiedAt") ?? verifiedAt,
                Note = string.Join(" ", new[] { Text(policy, "note") }
                    .Concat(Strings(Get(plan, "notes"))).Concat(new[] { Text(auth, "note"), Text(discovery, "reason"), Text(discovery, "note"), Text(suggestions, "note") })
                    .Where(note => !string.IsNullOrEmpty(note))),
                Protocol = Text(row, "protocol"), DefaultForProvider = Flag(row, "defaultForProvider"),
                Advanced = Flag(row, "advanced"), BaseUrl = Text(row, "baseUrl"), InferencePath = Text(row, "inferencePath"),
                DiscoveryStatus = Text(discovery, "status"), DiscoveryPath = Text(discovery, "path"),
                DiscoveryUrl = Text(discovery, "url"), DiscoveryFormat = Text(discovery, "responseFormat"),
                DiscoveryAuthMode = Text(Get(discovery, "auth"), "mode"),
                DiscoveryAuthHeader = Text(Get(discovery, "auth"), "header"), AuthHeader = Text(auth, "header"),
                AuthMode = Text(auth, "mode"), AuthRequired = Flag(auth, "required"),
                AnthropicVersion = Text(Get(auth, "extraHeaders"), "anthropic-version"),
                Aliases = System.Array.AsReadOnly(aliases), Sources = System.Array.AsReadOnly(Strings(Get(row, "sources"))),
                Candidates = System.Array.AsReadOnly(Strings(Get(suggestions, "models"))),
                CredentialKinds = System.Array.AsReadOnly(Strings(Get(plan, "credentialKinds"))),
                FundingSources = System.Array.AsReadOnly(Strings(Get(plan, "fundingSources"))),
                AcceptedAccountPlans = System.Array.AsReadOnly(Strings(Get(plan, "acceptedAccountPlans")))
            };
        }

        private static string Region(string region)
        {
            switch (region)
            {
                case "china": case "cn": return "中国站";
                case "international": return "国际站";
                case "cn-beijing": return "北京";
                case "cn-hongkong": return "中国香港";
                case "ap-southeast-1": case "sgp": return "新加坡";
                case "us-east-1": return "美国弗吉尼亚";
                case "ams": return "欧洲阿姆斯特丹";
                case "local": return "本机";
                case "official_endpoint": case "global": return "官方入口";
                case "account_region": case "account_region_requires_console": return "按账号地区";
                case "user_defined": return "自定义";
                default: return region;
            }
        }
    }
}
