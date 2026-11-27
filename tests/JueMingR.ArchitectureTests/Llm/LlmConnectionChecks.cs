using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Features.Llm;
using JueMingR.Infrastructure.Llm;
using JueMingR.Platform.Llm;

namespace JueMingR.ArchitectureTests
{
    internal static class LlmConnectionChecks
    {
        private const string TestKey = "fixture-key-never-logged";
        private static readonly IReadOnlyList<LlmPreset> Presets = LlmPresetCatalog.Load();

        internal static void Run(List<string> failures)
        {
            Run(failures, "presets, credential scope and discovery", PresetsAndDiscovery);
            Run(failures, "official list paths and credential separation", DiscoveryIsolation);
            Run(failures, "selected credential kind persists independently", CredentialKinds);
            Run(failures, "streaming and non-streaming tool round trips", ProtocolRoundTrips);
            Run(failures, "cross-turn wire fields, tool groups and fragmented UTF-8", AcrossUserTurns);
            Run(failures, "bad completion, cancellation and redaction", FailuresAndRedaction);
            Run(failures, "DPAPI, prepared replacement, references and public history", PersistentConfiguration);
            Run(failures, "save and use commits exactly the requested scope", SaveAndUse);
        }

        private static void Run(List<string> failures, string label, Func<Task> test)
        {
            try { test().GetAwaiter().GetResult(); }
            catch (Exception error) { failures.Add("LlmConnections " + label + ": " + error); }
        }

        private static async Task PresetsAndDiscovery()
        {
            Require(Presets.Count == 53 && new[] { "openai", "anthropic", "deepseek", "glm", "mimo", "kimi", "minimax" }
                .All(provider => Presets.Any(preset => preset.Provider == provider && preset.Aliases.Count != 0)), "required catalog coverage");
            Require(Presets.Where(preset => preset.Provider == "alibaba_bailian" && preset.PlanId == "coding_plan")
                .All(preset => preset.Protocol != "openai_responses"), "Aliyun Coding Plan rejects Responses");
            Require(Presets.Any(preset => preset.Provider == "mimo" && preset.PlanId == "token_plan" &&
                preset.Region == "ams" && preset.Policy == "reference_only"), "MiMo plan regions");
            int calls = 0;
            using (var transport = new LlmHttpTransport(_ => new Fixture(request =>
            {
                calls++;
                string url = request.RequestUri.AbsoluteUri;
                if (url.Contains("minimax.cn"))
                {
                    Require(url == "https://api.minimax.cn/anthropic/v1/models", "MiniMax base path");
                    Require(Header(request, "X-Api-Key") == TestKey && Header(request, "Authorization") == null, "list-specific MiniMax auth");
                    return Json("{\"data\":[{\"id\":\"text-model\"}],\"has_more\":false}");
                }
                Require(url.StartsWith("https://demo.cn-beijing.maas.aliyuncs.com/api/v1/models?page_no=", StringComparison.Ordinal), "Bailian native list");
                Require(Header(request, "Authorization") == "Bearer " + TestKey, "Bailian bearer");
                return url.Contains("page_no=1&")
                    ? Json("{\"output\":{\"models\":[{\"model_id\":\"first\"}],\"total\":101}}")
                    : Json("{\"output\":{\"models\":[{\"model_id\":\"second\"}],\"total\":101}}");
            })))
            {
                var owner = new LlmConnections(Presets, null, transport);
                var draft = NewDraft(owner, "minimax_china_anthropic", false);
                IReadOnlyList<LlmModelInfo> models = await owner.DiscoverAsync(draft, CancellationToken.None).ConfigureAwait(false);
                Require(models.Single().Id == "text-model" && owner.HasValidDiscovery(draft), "test fingerprint at discovery");
                draft.DisplayName = "same request";
                Require(owner.HasValidDiscovery(draft), "renaming does not invalidate discovery");
                draft.Model = "different-model";
                Require(!owner.HasValidDiscovery(draft), "selection change invalidates discovery");
                owner.ApplyPreset(draft, "minimax_china_subscription_anthropic");
                Require(owner.RevealApiKey(draft) == "", "same-host subscription must clear API credential");
                await Failure(() => owner.DiscoverAsync(draft, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
                owner.ApplyPreset(draft, "mimo_token_sgp_chat");
                draft.Model = "mimo";
                draft.ApiKey = TestKey;
                await Failure(() => owner.TestChatAsync(draft, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
                owner.ApplyPreset(draft, "ark_beijing_agent_plan_chat");
                draft.Model = "ark-code-latest";
                draft.ApiKey = TestKey;
                draft.BaseUrl = "https://ark.cn-beijing.volces.com/api/v3";
                Require(owner.RevealApiKey(draft) == "", "same-origin path move detaches plan key");
                draft.ApiKey = TestKey;
                await Failure(() => owner.TestChatAsync(draft, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
                owner.ApplyPreset(draft, "bailian_workspace_beijing_chat");
                await Failure(() => owner.DiscoverAsync(draft, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
                draft.Workspace = "demo";
                draft.ApiKey = TestKey;
                var found = await owner.DiscoverAsync(draft, CancellationToken.None).ConfigureAwait(false);
                Require(found.Count == 2 && calls == 3, "provider-specific list paging and request policy");
                draft.BaseUrl = "https://changed.example/v1";
                await Failure(() => owner.DiscoverAsync(draft, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
                draft.ApiKey = TestKey;
                await Failure(() => owner.DiscoverAsync(draft, CancellationToken.None), LlmFailure.DiscoveryUnavailable).ConfigureAwait(false);
                draft.BaseUrl = "https://changed.example/v1/chat/completions";
                draft.ApiKey = TestKey;
                await Failure(() => owner.DiscoverAsync(draft, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
            }
        }

        private static async Task DiscoveryIsolation()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-Llm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            int handlerCalls = 0;
            try
            {
                using (var transport = new LlmHttpTransport(_ => new Fixture(request =>
                {
                    handlerCalls++;
                    // No attempted GET may arrive at a different plan's path or origin.
                    Require(request.RequestUri.AbsoluteUri == "https://ark.cn-beijing.volces.com/api/v3/models", "verified Ark models URI");
                    Require(Header(request, "Authorization") == "Bearer " + TestKey, "unchanged credential in ordinary request");
                    return Json("{\"data\":[{\"id\":\"fixture-model\"}]}");
                })))
                using (var store = new LlmFileStore(root))
                {
                    var owner = new LlmConnections(Presets, store, transport);
                    await owner.InitializeAsync().ConfigureAwait(false);
                    LlmConnectionDraft saved = NewDraft(owner, "ark_beijing_chat", false);
                    await owner.SaveAsync(saved).ConfigureAwait(false);
                    LlmConnectionDraft edit = owner.Edit(saved.Id);
                    await owner.DiscoverAsync(edit, CancellationToken.None).ConfigureAwait(false);
                    Require(owner.HasValidDiscovery(edit), "official discovery proof before route edit");
                    edit.BaseUrl = "https://ark.cn-beijing.volces.com";
                    edit.InferencePath = "/api/v3/chat/completions";
                    foreach (string path in new[] { "/api/plan/v3/models", "/api/coding/v3/models", "https://other.example/models" })
                    {
                        edit.DiscoveryPath = path;
                        Require(!owner.HasValidDiscovery(edit), "edited official list cannot reuse previous successful test");
                        await Failure(() => owner.DiscoverAsync(edit, CancellationToken.None), LlmFailure.DiscoveryUnavailable).ConfigureAwait(false);
                    }
                    Require(handlerCalls == 1 && owner.RevealApiKey(edit) == TestKey, "invalid discovery is blocked before HTTP and saved inference key stays usable");
                    edit.DiscoveryPath = "/models";
                    await owner.DiscoverAsync(edit, CancellationToken.None).ConfigureAwait(false);
                    Require(handlerCalls == 2, "verified list survives equivalent inference URI split");

                    LlmConnectionDraft custom = NewDraft(owner, "custom_openai_chat", false);
                    custom.BaseUrl = "https://ark.cn-beijing.volces.com";
                    custom.InferencePath = "/api/v3/chat/completions";
                    custom.DiscoveryPath = "/api/v3/models";
                    custom.ApiKey = TestKey;
                    await owner.DiscoverAsync(custom, CancellationToken.None).ConfigureAwait(false);
                    Require(handlerCalls == 3 && owner.HasValidDiscovery(custom), "custom same-plan, same-origin discovery");
                    custom.DiscoveryPath = "/api/plan/v3/models";
                    Require(owner.RevealApiKey(custom) == "" && !owner.HasValidDiscovery(custom), "custom path move detaches credential and test");
                    custom.ApiKey = TestKey;
                    await Failure(() => owner.DiscoverAsync(custom, CancellationToken.None), LlmFailure.DiscoveryUnavailable).ConfigureAwait(false);
                    custom.DiscoveryPath = "https://other.example/models";
                    custom.ApiKey = TestKey;
                    await Failure(() => owner.DiscoverAsync(custom, CancellationToken.None), LlmFailure.DiscoveryUnavailable).ConfigureAwait(false);
                    Require(handlerCalls == 3, "custom cross-region/plan and cross-origin paths are never sent");
                }
            }
            finally { Directory.Delete(root, true); }
        }
        private static async Task CredentialKinds()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-Llm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            int handlerCalls = 0;
            Guid id;
            try
            {
                using (var transport = new LlmHttpTransport(_ => new Fixture(request =>
                {
                    handlerCalls++;
                    Require(Header(request, "Authorization") == "Bearer " + TestKey, "GLM uses explicit selected credential");
                    return Sample("openai_chat", false, false);
                })))
                {
                    using (var store = new LlmFileStore(root))
                    {
                        var owner = new LlmConnections(Presets, store, transport);
                        await owner.InitializeAsync().ConfigureAwait(false);
                        var draft = NewDraft(owner, "glm_china_standard_chat", false);
                        id = draft.Id;
                        Require(draft.CredentialKind == "", "multiple kinds require explicit choice");
                        await Failure(() => owner.SaveAsync(draft), LlmFailure.Configuration).ConfigureAwait(false);
                        draft.CredentialKind = "legacy_compatible_key";
                        draft.ApiKey = TestKey;
                        await owner.TestChatAsync(draft, CancellationToken.None).ConfigureAwait(false);
                        await owner.SaveAsync(draft).ConfigureAwait(false);
                    }

                    using (var store = new LlmFileStore(root))
                    {
                        var owner = new LlmConnections(Presets, store, transport);
                        await owner.InitializeAsync().ConfigureAwait(false);
                        var draft = owner.Edit(id);
                        Require(draft.CredentialKind == "legacy_compatible_key" && owner.HasValidChatTest(draft) &&
                            owner.RevealApiKey(draft) == TestKey, "stored kind preserves verified credential identity");
                        draft.CredentialKind = "standard_api_key";
                        Require(!owner.HasValidChatTest(draft) && owner.RevealApiKey(draft) == "", "switching kind detaches previous credential and proof");
                        await Failure(() => owner.TestChatAsync(draft, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
                        draft.CredentialKind = "team_plan_key";
                        draft.ApiKey = TestKey;
                        await Failure(() => owner.TestChatAsync(draft, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
                        Require(handlerCalls == 1, "missing or inconsistent credential kinds never start HTTP");
                    }
                }
            }
            finally { Directory.Delete(root, true); }
        }
        private static async Task ProtocolRoundTrips()
        {
            foreach (string presetId in new[] { "deepseek_official_chat", "openai_official_responses", "claude_official_messages" })
            {
                foreach (bool stream in new[] { false, true })
                {
                    string protocol = Presets.Single(p => p.Id == presetId).Protocol;
                    int count = 0;
                    using (var transport = new LlmHttpTransport(_ => new Fixture(request =>
                    {
                        count++;
                        Require(Header(request, "Authorization") == "Bearer " + TestKey, "provider bearer");
                        string path = request.RequestUri.AbsolutePath;
                        string body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        string toolPair = protocol == "openai_chat" ? "\"tool_call_id\":\"call-1\"" :
                            protocol == "openai_responses" ? "\"call_id\":\"call-1\"" : "\"tool_use_id\":\"call-1\"";
                        string type = protocol == "openai_chat" ? "\"role\":\"tool\"" :
                            protocol == "openai_responses" ? "\"type\":\"function_call_output\"" : "\"type\":\"tool_result\"";
                        Require(path.EndsWith(protocol == "openai_chat" ? "/chat/completions" :
                            protocol == "openai_responses" ? "/responses" : "/v1/messages", StringComparison.Ordinal), "protocol own endpoint");
                        if (protocol == "anthropic_messages")
                            Require(Header(request, "anthropic-version") == "2026-10-21", "Claude API version");
                        if (count == 1)
                            Require(body.Contains("check_connection") && !body.Contains("JMR_LOCAL_TOOL_OK"), "test tool schema only");
                        else
                        {
                            string privateMarker = protocol == "openai_chat" ? "deep-wire" :
                                protocol == "openai_responses" ? "encrypted-wire" : "sig-wire";
                            Require(body.Contains(toolPair) && body.Contains(type) && body.Contains("JMR_LOCAL_TOOL_OK") &&
                                body.Contains(privateMarker), "original wire and paired local marker");
                        }
                        return Sample(protocol, stream, count == 1);
                    })))
                    {
                        var feature = new LlmConnections(Presets, null, transport,
                            (connection, token) => CompanionChat.TestToolsAsync(transport, connection, token));
                        var draft = NewDraft(feature, presetId, stream);
                        LlmConnectionTest result = await feature.TestToolsAsync(draft, CancellationToken.None).ConfigureAwait(false);
                        Require(result.Success && feature.HasValidToolTest(draft) && count == 2, "real reducer round trip");
                    }
                }
            }
        }

        private static HttpResponseMessage Sample(string protocol, bool stream, bool tool)
        {
            if (protocol == "openai_chat")
            {
                string call = "{\"id\":\"call-1\",\"type\":\"function\",\"function\":{\"name\":\"check_connection\",\"arguments\":\"{}\"}}";
                if (!stream) return Json(tool
                    ? "{\"choices\":[{\"finish_reason\":\"tool_calls\",\"message\":{\"role\":\"assistant\",\"reasoning_content\":\"deep-wire\",\"tool_calls\":[" + call + "]}}]}"
                    : "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"已完成\"}}]}");
                return Events(tool
                    ? E("{\"choices\":[{\"delta\":{\"role\":\"assistant\",\"reasoning_content\":\"deep-wire\",\"tool_calls\":[{"
                        + "\"index\":0," + call.Substring(1) + "]},\"finish_reason\":\"tool_calls\"}]}") + E("[DONE]")
                    : E("{\"choices\":[{\"delta\":{\"content\":\"已完成\"},\"finish_reason\":\"stop\"}]}") + E("[DONE]"));
            }
            if (protocol == "openai_responses")
            {
                string reason = "{\"type\":\"reasoning\",\"id\":\"r1\",\"encrypted_content\":\"encrypted-wire\"}";
                string call = "{\"type\":\"function_call\",\"id\":\"item-1\",\"call_id\":\"call-1\",\"name\":\"check_connection\",\"arguments\":\"{}\",\"status\":\"completed\"}";
                string message = "{\"type\":\"message\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":\"已完成\"}]}";
                string output = tool ? "[" + reason + "," + call + "]" : "[" + message + "]";
                return stream
                    ? Events((tool ? E("{\"type\":\"response.output_item.done\",\"output_index\":0,\"item\":" + reason + "}") : "") +
                        E("{\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":" +
                        output.Replace(",\"encrypted_content\":\"encrypted-wire\"", "") + "}}"))
                    : Json("{\"status\":\"completed\",\"output\":" + output + "}");
            }
            string thinking = "{\"type\":\"thinking\",\"thinking\":\"internal\",\"signature\":\"sig-wire\"}";
            string toolUse = "{\"type\":\"tool_use\",\"id\":\"call-1\",\"name\":\"check_connection\",\"input\":{}}";
            string text = "{\"type\":\"text\",\"text\":\"已完成\"}";
            if (!stream) return Json("{\"stop_reason\":\"" + (tool ? "tool_use" : "end_turn") + "\",\"content\":[" +
                (tool ? thinking + "," + toolUse : text) + "]}");
            return Events(E("{\"type\":\"message_start\",\"message\":{\"content\":[]}}") + E("{\"type\":\"ping\"}") +
                Block(0, tool ? thinking : text) + (tool ? Block(1, toolUse) : "") +
                E("{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"" + (tool ? "tool_use" : "end_turn") + "\"}}") +
                E("{\"type\":\"message_stop\"}"));
        }

        private static string Block(int index, string json) =>
            E("{\"type\":\"content_block_start\",\"index\":" + index + ",\"content_block\":" + json + "}") +
            E("{\"type\":\"content_block_stop\",\"index\":" + index + "}");

        private static async Task AcrossUserTurns()
        {
            int exchanges = 0;
            using (var transport = new LlmHttpTransport(_ => new Fixture(request =>
            {
                int nth = ++exchanges;
                string body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (nth == 1)
                    return Events(E("{\"choices\":[{\"delta\":{\"reasoning_content\":\"secret-reason-1\",\"content\":\"你好\"},\"finish_reason\":\"stop\"}]}") + E("[DONE]"), true);
                Require(body.Contains("secret-reason-1") && body.Contains("second user"), "new user preserves previous non-tool reasoning");
                if (nth == 2)
                    return Events(E("{\"choices\":[{\"delta\":{\"encrypted_content\":\"cipher-1\",\"tool_calls\":["
                        + "{\"index\":0,\"id\":\"first\",\"type\":\"function\",\"function\":{\"name\":\"check\",\"arguments\":\"{\\\"words\\\":\\\"\"},\"extra_content\":{\"google\":{\"thought_signature\":\"sig-first\"}}},"
                        + "{\"index\":1,\"id\":\"second\",\"type\":\"function\",\"function\":{\"name\":\"check\",\"arguments\":\"{}\"}}]}}]}") +
                        E("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"鱼饵\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}]}") + E("[DONE]"), true);
                Require(body.Contains("sig-first") && body.Contains("cipher-1") &&
                    body.Contains("\"tool_call_id\":\"first\",\"content\":\"first-result\"") &&
                    body.Contains("\"tool_call_id\":\"second\",\"content\":\"second-result\""), "tool results preserve position, private fields and ids");
                return Sample("openai_chat", true, false);
            })))
            {
                LlmConnectionSnapshot connection = Connection("deepseek_official_chat", true);
                var messages = new List<LlmMessage> { LlmMessage.User("first user") };
                var tools = new[] { new LlmToolDefinition("check", "check", "{\"type\":\"object\"}") };
                var visible = new StringBuilder();
                int dispatched = 0;
                Task<LlmToolResult> Execute(LlmToolCall call, CancellationToken token)
                {
                    dispatched++;
                    if (call.Id == "first") Require((string)call.Arguments["words"] == "鱼饵", "UTF-8 and argument fragments");
                    return Task.FromResult(new LlmToolResult(call.Id, call.Id + "-result"));
                }
                await CompanionChat.RunAsync(transport, connection, "", messages, tools, Execute, text => visible.Append(text), CancellationToken.None).ConfigureAwait(false);
                messages.Add(LlmMessage.User("second user"));
                await CompanionChat.RunAsync(transport, connection, "", messages, tools, Execute, text => visible.Append(text), CancellationToken.None).ConfigureAwait(false);
                Require(dispatched == 2 && exchanges == 3 && visible.ToString() == "你好已完成", "exact dispatch and public-only streaming");
            }
        }

        private static async Task FailuresAndRedaction()
        {
            LlmConnectionSnapshot connection = Connection("deepseek_official_chat", true);
            var request = new LlmRequest("", new[] { LlmMessage.User("hi") }, System.Array.Empty<LlmToolDefinition>());
            using (var transport = new LlmHttpTransport(_ => new Fixture(req =>
                Events(E("{\"choices\":[{\"delta\":{\"content\":\"fixture-key-\"}}]}") +
                    E("{\"choices\":[{\"delta\":{\"content\":\"never-logged\"},\"finish_reason\":\"stop\"}]}") + E("[DONE]"), true))))
            {
                var text = new StringBuilder();
                var turn = await transport.CompleteAsync(connection, request, value => text.Append(value), CancellationToken.None).ConfigureAwait(false);
                Require(turn.Text == "[已隐藏]" && text.ToString() == "[已隐藏]", "split secrets escaped from neither final output nor chunks");
            }
            using (var transport = new LlmHttpTransport(_ => new Fixture(req =>
                Events(E("{\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}")))))
                await Failure(() => transport.CompleteAsync(connection, request, null, CancellationToken.None), LlmFailure.Incomplete).ConfigureAwait(false);
            using (var transport = new LlmHttpTransport(_ => new Fixture(req =>
                Events(E("{\"error\":{\"code\":\"insufficient_quota\",\"message\":\"" + TestKey + "\"}}")))))
                await Failure(() => transport.CompleteAsync(connection, request, null, CancellationToken.None), LlmFailure.Quota).ConfigureAwait(false);
            using (var transport = new LlmHttpTransport(_ => new Fixture(req =>
                Events(E("{\"choices\":[],\"choices\":[]}") + E("[DONE]")))))
                await Failure(() => transport.CompleteAsync(connection, request, null, CancellationToken.None), LlmFailure.Protocol).ConfigureAwait(false);
            int posts = 0;
            using (var transport = new LlmHttpTransport(_ => new Fixture(req =>
            {
                posts++;
                return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent(
                    "{\"code\":\"AllocationQuota.FreeTierOnly\",\"message\":\"" + TestKey + "\"}") };
            })))
                await Failure(() => transport.CompleteAsync(connection, request, null, CancellationToken.None), LlmFailure.Quota).ConfigureAwait(false);
            Require(posts == 1, "a rejected plan never replays the POST");
            using (var transport = new LlmHttpTransport(_ => new Fixture(req =>
            {
                var redirected = new HttpResponseMessage(HttpStatusCode.Redirect);
                redirected.Headers.Location = new Uri("https://unrelated.example/");
                return redirected;
            })))
                await Failure(() => transport.CompleteAsync(connection, request, null, CancellationToken.None), LlmFailure.Configuration).ConfigureAwait(false);
            using (var transport = new LlmHttpTransport(_ => new Fixture(req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StalledBody())
            })))
                await Failure(() => transport.CompleteAsync(Connection("deepseek_official_chat", true, 1), request, null,
                    CancellationToken.None), LlmFailure.Timeout).ConfigureAwait(false);
            using (var cancellation = new CancellationTokenSource())
            using (var transport = new LlmHttpTransport(_ => new Fixture(req =>
                Events(E("{\"choices\":[{\"delta\":{\"content\":\"chunk\"}}]}") + E("[DONE]"), true))))
            {
                try
                {
                    await transport.CompleteAsync(connection, request, _ => cancellation.Cancel(), cancellation.Token).ConfigureAwait(false);
                    throw new InvalidOperationException("Canceled request completed.");
                }
                catch (OperationCanceledException) { }
            }
        }

        private static async Task SaveAndUse()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-Llm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var ownerId = Guid.NewGuid();
            var currentProfile = Guid.NewGuid();
            var otherProfile = Guid.NewGuid();
            var inheritedProfile = Guid.NewGuid();
            Guid defaultId, selectedId;
            try
            {
                using (var transport = new LlmHttpTransport())
                using (var store = new LlmFileStore(root))
                {
                    var connections = new LlmConnections(Presets, store, transport);
                    await connections.InitializeAsync().ConfigureAwait(false);
                    var first = NewDraft(connections, "deepseek_official_chat", false);
                    var blockedPlan = NewDraft(connections, "mimo_token_sgp_chat", false);
                    first.ApiKey = "";
                    await Failure(() => connections.SaveAndUseAsync(first), LlmFailure.Configuration).ConfigureAwait(false);
                    await Failure(() => connections.SaveAndUseAsync(blockedPlan), LlmFailure.Configuration).ConfigureAwait(false);
                    Require(!connections.Enabled && !connections.DefaultId.HasValue && connections.List().Count == 0,
                        "invalid key or restricted plan does not partially save or enable");

                    first.ApiKey = TestKey;
                    defaultId = first.Id;
                    await connections.SaveAndUseAsync(first).ConfigureAwait(false);
                    Require(connections.Enabled && connections.DefaultId == defaultId &&
                        connections.TryResolve(ownerId, inheritedProfile, out var active, out _) && active.Id == defaultId,
                        "initial save works without a selected teammate");
                    await connections.SetBindingAsync(ownerId, currentProfile, defaultId).ConfigureAwait(false);
                    await connections.SetBindingAsync(ownerId, otherProfile, defaultId).ConfigureAwait(false);

                    var second = NewDraft(connections, "openai_official_chat", false);
                    selectedId = second.Id;
                    await connections.SaveAndUseAsync(second, ownerId, currentProfile).ConfigureAwait(false);
                    Require(connections.DefaultId == defaultId && connections.GetBinding(ownerId, currentProfile) == selectedId &&
                        connections.GetBinding(ownerId, otherProfile) == defaultId && !connections.GetBinding(ownerId, inheritedProfile).HasValue,
                        "targeted save preserves the default and other bindings");
                }

                using (var transport = new LlmHttpTransport())
                using (var store = new LlmFileStore(root))
                {
                    var connections = new LlmConnections(Presets, store, transport);
                    await connections.InitializeAsync().ConfigureAwait(false);
                    Require(connections.Enabled && connections.DefaultId == defaultId &&
                        connections.TryResolve(ownerId, currentProfile, out var selected, out _) && selected.Id == selectedId,
                        "enable, default and teammate selections survive reload");
                    await connections.SetEnabledAsync(false).ConfigureAwait(false);
                    string path = Path.Combine(root, "JueMingRData", "Llm", "connections.json");
                    File.AppendAllText(path, Environment.NewLine);
                    string incoming = File.ReadAllText(path);
                    await Failure(() => connections.SaveAndUseAsync(connections.Edit(selectedId)), LlmFailure.Storage).ConfigureAwait(false);
                    Require(!connections.Enabled && connections.DefaultId == defaultId && File.ReadAllText(path) == incoming,
                        "actual atomic conflict cannot enable or publish a new default");
                }
            }
            finally { Directory.Delete(root, true); }
        }
        private static async Task PersistentConfiguration()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-Llm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Guid ownerId = Guid.NewGuid(), profileId = Guid.NewGuid(), connectionId;
            string data = Path.Combine(root, "JueMingRData", "Llm");
            try
            {
                using (var transport = new LlmHttpTransport(_ => new Fixture(request => Sample("openai_responses", true, false))))
                using (var store = new LlmFileStore(root))
                {
                    var owner = new LlmConnections(Presets, store, transport);
                    await owner.InitializeAsync().ConfigureAwait(false);
                    var draft = NewDraft(owner, "openai_official_responses", true);
                    connectionId = draft.Id;
                    draft.SecretHeaders = "{\"X-Custom-Secret\":\"secret-header-fixture\"}";
                    await owner.TestChatAsync(draft, CancellationToken.None).ConfigureAwait(false);
                    await owner.SaveAsync(draft).ConfigureAwait(false);
                    await owner.SetEnabledAsync(true).ConfigureAwait(false);
                    await owner.SetDefaultAsync(connectionId).ConfigureAwait(false);
                    await owner.SetBindingAsync(ownerId, profileId, connectionId).ConfigureAwait(false);
                    Require(owner.TryResolve(ownerId, profileId, out var actual, out _) &&
                        actual.Endpoint.AbsoluteUri == "https://api.openai.com/v1/responses", "resolved committed connection");
                    await store.LoadHistoryAsync(ownerId, profileId).ConfigureAwait(false);
                    await store.SaveHistoryAsync(ownerId, profileId,
                        new[] { new LlmHistoryEntry(Guid.NewGuid(), "user", "visible chat", DateTimeOffset.UtcNow) }).ConfigureAwait(false);
                    Require(Directory.GetFiles(data, "*.json", SearchOption.AllDirectories)
                        .All(path => !File.ReadAllText(path).Contains(TestKey) && !File.ReadAllText(path).Contains("secret-header-fixture")), "no plaintext credentials");
                }

                using (var transport = new LlmHttpTransport())
                using (var store = new LlmFileStore(root))
                {
                    var owner = new LlmConnections(Presets, store, transport);
                    await owner.InitializeAsync().ConfigureAwait(false);
                    Require(owner.TryResolve(ownerId, profileId, out var actual, out _) && actual.Id == connectionId &&
                        owner.RevealApiKey(owner.Edit(connectionId)) == TestKey, "DPAPI recovery and persisted binding");
                    Require(owner.HasValidChatTest(owner.Edit(connectionId)), "test record persisted with its credential version");
                    Require((await store.LoadHistoryAsync(ownerId, profileId).ConfigureAwait(false)).Single().Text == "visible chat", "reviewable public history");
                    // A different writer that read the old history must not copy it back after an explicit clear.
                    using (var stale = new LlmFileStore(root))
                    {
                        var previous = await stale.LoadHistoryAsync(ownerId, profileId).ConfigureAwait(false);
                        await store.ClearHistoryAsync(ownerId, profileId).ConfigureAwait(false);
                        await Failure(() => stale.SaveHistoryAsync(ownerId, profileId, previous), LlmFailure.Storage).ConfigureAwait(false);
                    }
                    await store.ClearHistoryAsync(ownerId, profileId).ConfigureAwait(false);
                    Require((await store.LoadHistoryAsync(ownerId, profileId).ConfigureAwait(false)).Count == 0 &&
                        Directory.GetFiles(Path.Combine(data, "history"), "*", SearchOption.AllDirectories)
                            .All(path => !File.ReadAllText(path).Contains("visible chat")), "explicit clear empties current and previous history");
                    await owner.DeleteAsync(connectionId).ConfigureAwait(false);
                    Require(!owner.TryResolve(ownerId, profileId, out _, out _) && owner.GetBinding(ownerId, profileId) == connectionId,
                        "deletion preserves explicit unconfigured binding");
                }
                string file = Path.Combine(data, "connections.json");
                string future = File.ReadAllText(file).Replace("\"version\":1", "\"version\":3");
                File.WriteAllText(file, future);
                using (var store = new LlmFileStore(root))
                    await Failure(() => store.LoadAsync(), LlmFailure.Storage).ConfigureAwait(false);
                Require(File.ReadAllText(file) == future, "future config document stays intact");
            }
            finally { Directory.Delete(root, true); }
        }

        private static LlmConnectionDraft NewDraft(LlmConnections owner, string preset, bool stream)
        {
            var draft = owner.Edit();
            owner.ApplyPreset(draft, preset);
            draft.Model = "fixture-model";
            draft.ApiKey = TestKey;
            draft.Stream = stream;
            return draft;
        }

        private static LlmConnectionSnapshot Connection(string presetId, bool stream, int firstByteSeconds = 25)
        {
            LlmPreset preset = Presets.Single(p => p.Id == presetId);
            var options = new LlmConnectionSettings
            {
                PresetId = preset.Id, DisplayName = "fixture", Protocol = preset.Protocol, AuthMode = preset.AuthMode,
                Stream = stream, Model = "fixture-model", FirstByteSeconds = firstByteSeconds
            };
            return new LlmConnectionSnapshot(Guid.NewGuid(), "fixture", options, preset, new LlmConnectionSecrets(TestKey, null, null),
                new Uri(preset.BaseUrl + preset.InferencePath), null);
        }

        private static async Task Failure(Func<Task> test, LlmFailure expected)
        {
            try { await test().ConfigureAwait(false); }
            catch (LlmException error) when (error.Failure == expected)
            {
                Require(!error.Message.Contains(TestKey), "errors suppress remote data");
                return;
            }
            throw new InvalidOperationException("Expected failure " + expected);
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }

        private static string Header(HttpRequestMessage request, string name) =>
            request.Headers.TryGetValues(name, out IEnumerable<string> results) ? results.Single() : null;

        private static string E(string data) => "data: " + data + "\n\n";

        private static HttpResponseMessage Json(string body) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static HttpResponseMessage Events(string body, bool fragmented = false)
        {
            Stream source = fragmented ? new SmallReads(Encoding.UTF8.GetBytes(body)) : new MemoryStream(Encoding.UTF8.GetBytes(body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(source) };
        }

        private sealed class Fixture : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> reply;
            public Fixture(Func<HttpRequestMessage, HttpResponseMessage> reply) { this.reply = reply; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation) =>
                Task.FromResult(reply(request));
        }

        private sealed class StalledBody : MemoryStream
        {
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                return 0;
            }
        }
        private sealed class SmallReads : MemoryStream
        {
            public SmallReads(byte[] bytes) : base(bytes) { }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
                base.ReadAsync(buffer, offset, Math.Min(count, 3), token);
        }
    }
}
