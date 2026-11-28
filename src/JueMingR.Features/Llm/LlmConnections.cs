using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Platform.Llm;
using static JueMingR.Features.Llm.ModelConnectionValidation;

namespace JueMingR.Features.Llm
{
    public sealed class LlmConnections
    {
        private const int ConnectionLimit = 48;
        private const int BindingLimit = 512;
        private readonly ILlmStore store;
        private readonly ILlmTransport transport;
        private readonly Func<LlmConnectionSnapshot, CancellationToken, Task<LlmConnectionTest>> testTools;
        private readonly Dictionary<string, LlmPreset> presets;
        private readonly object gate = new object();
        private readonly SemaphoreSlim writes = new SemaphoreSlim(1);
        private readonly ConditionalWeakTable<LlmConnectionDraft, ProbeState> probes =
            new ConditionalWeakTable<LlmConnectionDraft, ProbeState>();
        private LlmStoredConfiguration configuration = new LlmStoredConfiguration();
        private Dictionary<Guid, LlmConnectionSnapshot> runnable = new Dictionary<Guid, LlmConnectionSnapshot>();
        private bool ready;
        private string storageStatus = "正在读取模型连接。";

        public LlmConnections(IReadOnlyList<LlmPreset> presets, ILlmStore store, ILlmTransport transport,
            Func<LlmConnectionSnapshot, CancellationToken, Task<LlmConnectionTest>> testTools = null)
        {
            Presets = presets;
            this.presets = presets.ToDictionary(preset => preset.Id, StringComparer.Ordinal);
            this.store = store;
            this.transport = transport;
            this.testTools = testTools;
        }

        public IReadOnlyList<LlmPreset> Presets { get; }
        public bool Enabled { get { lock (gate) return configuration.Enabled; } }
        public Guid? DefaultId { get { lock (gate) return configuration.DefaultId; } }
        public string StorageStatus { get { lock (gate) return storageStatus; } }

        public async Task InitializeAsync()
        {
            try
            {
                LlmStoredConfiguration loaded = await store.LoadAsync().ConfigureAwait(false);
                Dictionary<Guid, LlmConnectionSnapshot> resolved = BuildSnapshots(loaded);
                lock (gate)
                {
                    configuration = loaded; runnable = resolved; ready = true;
                    storageStatus = loaded.SecretStatus ?? "";
                }
            }
            catch (LlmException exception)
            {
                lock (gate) storageStatus = exception.Message;
            }
        }

        public IReadOnlyList<LlmConnectionInfo> List()
        {
            lock (gate)
                return configuration.Connections.Values.Select(row => new LlmConnectionInfo(row.Id, row.Settings))
                    .OrderBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        public Guid? GetBinding(Guid owner, Guid profile)
        {
            lock (gate)
                return configuration.Bindings.TryGetValue(Tuple.Create(owner, profile), out Guid id) ? id : (Guid?)null;
        }

        public int GetAffectedBindingCount(Guid id)
        {
            lock (gate) return configuration.Bindings.Values.Count(bound => bound == id);
        }

        public bool TryResolve(Guid owner, Guid profile, out LlmConnectionSnapshot snapshot, out string reason)
        {
            lock (gate)
            {
                snapshot = null;
                if (!ready) { reason = storageStatus; return false; }
                if (!configuration.Enabled) { reason = "请先在模型页启用直连。"; return false; }
                Guid id = configuration.Bindings.TryGetValue(Tuple.Create(owner, profile), out Guid bound)
                    ? bound : configuration.DefaultId.GetValueOrDefault();
                if (!configuration.Connections.ContainsKey(id))
                {
                    reason = "当前队友尚未配置模型连接。";
                    return false;
                }
                if (!runnable.TryGetValue(id, out snapshot))
                {
                    reason = "当前连接还不能使用。请在模型页检查配置和 API Key。";
                    return false;
                }
                reason = "";
                return true;
            }
        }

        public LlmConnectionDraft Edit(Guid? id = null)
        {
            lock (gate)
            {
                if (id.HasValue)
                {
                    if (!configuration.Connections.TryGetValue(id.Value, out LlmStoredConnection existing))
                        throw Error("这条连接已删除。");
                    LlmConnectionDraft draft = Draft(existing, configuration);
                    configuration.Secrets.TryGetValue(existing.SecretVersion, out LlmConnectionSecrets secrets);
                    if (existing.SecretVersion == Guid.Empty || secrets != null)
                    {
                        string fingerprint = Fingerprint(draft, secrets ?? new LlmConnectionSecrets(null, null, null));
                        if (existing.Probes.Count != 0)
                        {
                            ProbeState state = probes.GetOrCreateValue(draft);
                            foreach (var item in existing.Probes) state.Results[item.Key] = Tuple.Create(fingerprint, item.Value);
                        }
                    }
                    return draft;
                }
            }
            var created = new LlmConnectionDraft(Guid.NewGuid()) { DisplayName = "新连接" };
            ApplyPreset(created, "deepseek_official_chat");
            return created;
        }

        public LlmConnectionDraft Copy(Guid id)
        {
            LlmConnectionDraft source = Edit(id);
            var copy = new LlmConnectionDraft(Guid.NewGuid()) { RoutingPreset = source.RoutingPreset };
            source.CopyTo(copy);
            copy.DisplayName = source.DisplayName.Substring(0, Math.Min(75, source.DisplayName.Length)) + " 副本";
            return copy;
        }

        public void ApplyPreset(LlmConnectionDraft draft, string presetId)
        {
            if (!presets.TryGetValue(presetId, out LlmPreset preset)) throw Error("未找到厂商预设。");
            if (draft.PresetId == presetId) return;
            draft.PresetId = preset.Id;
            draft.RoutingPreset = preset;
            draft.Protocol = preset.Protocol;
            draft.BaseUrl = preset.BaseUrl ?? "";
            draft.InferencePath = preset.InferencePath ?? "";
            draft.DiscoveryPath = preset.DiscoveryPath ?? "";
            draft.AuthMode = preset.AuthMode == "user_configured" ? "optional_bearer" : preset.AuthMode;
            draft.CredentialKind = preset.CredentialKinds.Count == 1 ? preset.CredentialKinds[0] : "";
            draft.AnthropicVersion = preset.AnthropicVersion ?? "";
            draft.Workspace = "";
            draft.Model = "";
            draft.Reasoning = "";
            draft.StopSequences = "";
            // A preset selection is an explicit domain/account transition. Even same-host plans need fresh credentials.
            draft.HasStoredKey = false;
            draft.SecretVersion = Guid.Empty;
            draft.SecretScope = null;
            draft.ApiKey = null;
            draft.SecretHeaders = null;
            draft.ProxyCredentials = null;
            draft.ExtraHeaders = "";
        }

        public string RevealApiKey(LlmConnectionDraft draft) => ResolveSecrets(draft).ApiKey;
        public string RevealSecretHeaders(LlmConnectionDraft draft) => ResolveSecrets(draft).Headers;
        public string RevealProxyCredentials(LlmConnectionDraft draft) => ResolveSecrets(draft).ProxyCredentials;

        public Task SetBindingAsync(Guid owner, Guid profile, Guid? connection) =>
            ChangeAsync(candidate => ApplyBinding(candidate, owner, profile, connection));

        private static void ApplyBinding(LlmStoredConfiguration candidate, Guid owner, Guid profile, Guid? connection)
        {
            if (owner == Guid.Empty || profile == Guid.Empty)
                throw Error("请先选中一个队友档案。");
            var key = Tuple.Create(owner, profile);
            if (!connection.HasValue) { candidate.Bindings.Remove(key); return; }
            EnsureExists(candidate, connection.Value);
            if (candidate.Bindings.Count >= BindingLimit && !candidate.Bindings.ContainsKey(key))
                throw Error("队友连接绑定已达上限。");
            candidate.Bindings[key] = connection.Value;
        }
        public Task ForgetBindingAsync(Guid owner, Guid profile) =>
            ChangeAsync(candidate => candidate.Bindings.Remove(Tuple.Create(owner, profile)), true);

        public Task SetDefaultAsync(Guid? id) => ChangeAsync(candidate =>
        {
            if (id.HasValue) EnsureExists(candidate, id.Value);
            candidate.DefaultId = id ?? Guid.Empty;
        });

        public Task SetEnabledAsync(bool enabled) => ChangeAsync(candidate => candidate.Enabled = enabled);

        public Task DeleteAsync(Guid id) => ChangeAsync(candidate =>
        {
            if (!candidate.Connections.Remove(id)) return;
            if (candidate.DefaultId == id) candidate.DefaultId = Guid.Empty;
            // Keep explicit overrides so a deleted connection cannot silently move a teammate to a paid default.
        });

        public Task SaveAsync(LlmConnectionDraft draft) => SaveDraftAsync(draft, false, null, null);

        public Task SaveAndUseAsync(LlmConnectionDraft draft, Guid? owner = null, Guid? profile = null) =>
            SaveDraftAsync(draft, true, owner, profile);

        private Task SaveDraftAsync(LlmConnectionDraft draft, bool use, Guid? owner, Guid? profile)
        {
            LlmConnectionDraft captured = CopyDraft(draft);
            LlmConnectionSecrets secrets = ResolveSecrets(draft);
            var testResults = MatchingProbes(draft, Fingerprint(captured, secrets));
            return ChangeAsync(candidate =>
            {
                if (candidate.Connections.Count >= ConnectionLimit && !candidate.Connections.ContainsKey(captured.Id))
                    throw Error("连接档案已达上限，请先整理已有连接。");
                if (owner.HasValue != profile.HasValue)
                    throw Error("请先选中一个完整的队友档案。");
                CaptureValidated(captured, secrets, true, use);
                var settings = new LlmConnectionSettings();
                captured.CopyTo(settings);
                settings.DisplayName = settings.DisplayName.Trim();
                settings.Model = (settings.Model ?? "").Trim();

                // An immutable secret reference is published only after its encrypted payload is durable.
                Guid version = Guid.Empty;
                if (!secrets.IsEmpty)
                {
                    bool unchanged = candidate.Secrets.TryGetValue(captured.SecretVersion, out LlmConnectionSecrets original) &&
                        SameSecrets(original, secrets);
                    version = unchanged ? captured.SecretVersion : Guid.NewGuid();
                    candidate.Secrets[version] = secrets;
                }
                candidate.Connections[captured.Id] = new LlmStoredConnection(captured.Id, settings, version, testResults);
                if (!use) return;

                // The one candidate publishes the new connection, enable flag and exactly one scope.
                // A failed validation or protected document never switches active accounts.
                if (owner.HasValue) ApplyBinding(candidate, owner.Value, profile.Value, captured.Id);
                else candidate.DefaultId = captured.Id;
                candidate.Enabled = true;
            });
        }

        public async Task<LlmConnectionTest> TestChatAsync(LlmConnectionDraft draft, CancellationToken cancellation)
        {
            LlmConnectionSnapshot connection = CaptureProbe(draft, true);
            var request = new LlmRequest("这是连接测试。请回复一句简短的中文问候。",
                new[] { LlmMessage.User("你好。") }, Array.Empty<LlmToolDefinition>());
            LlmTurn reply;
            try { reply = await transport.CompleteAsync(connection, request, null, cancellation).ConfigureAwait(false); }
            catch (LlmException) { SetProbe(draft, connection, "chat", false); throw; }
            bool passed = !string.IsNullOrWhiteSpace(reply.Text) && reply.ToolCalls.Count == 0;
            var result = new LlmConnectionTest(passed
                ? (connection.CopySettings().Stream ? "普通对话和流式回复完整结束。" : "普通对话完整结束。")
                : "服务未返回完整的公开文本。", passed);
            SetProbe(draft, connection, "chat", passed);
            return result;
        }

        public async Task<LlmConnectionTest> TestToolsAsync(LlmConnectionDraft draft, CancellationToken cancellation)
        {
            LlmConnectionSnapshot connection = CaptureProbe(draft, true);
            if (testTools == null) return new LlmConnectionTest("工具回填入口暂不可用。", false);
            LlmConnectionTest result;
            try { result = await testTools(connection, cancellation).ConfigureAwait(false); }
            catch (LlmException) { SetProbe(draft, connection, "tools", false); throw; }
            SetProbe(draft, connection, "tools", result.Success);
            return result;
        }

        public async Task<IReadOnlyList<LlmModelInfo>> DiscoverAsync(LlmConnectionDraft draft, CancellationToken cancellation)
        {
            LlmConnectionSnapshot connection = CaptureProbe(draft, false);
            EnsureDiscoveryPlan(connection);
            IReadOnlyList<LlmModelInfo> models;
            try { models = await transport.DiscoverAsync(connection, cancellation).ConfigureAwait(false); }
            catch (LlmException) { SetProbe(draft, connection, "models", false); throw; }
            SetProbe(draft, connection, "models", true);
            return models;
        }

        public bool HasValidDiscovery(LlmConnectionDraft draft) => HasProbe(draft, "models");
        public bool HasValidChatTest(LlmConnectionDraft draft) => HasProbe(draft, "chat");
        public bool HasValidToolTest(LlmConnectionDraft draft) => HasProbe(draft, "tools");

        public string GetProbeSummary(LlmConnectionDraft draft)
        {
            IReadOnlyDictionary<string, LlmProbeRecord> results = MatchingProbes(draft, Fingerprint(draft, ResolveSecrets(draft)));
            if (results.Count == 0) return "";
            var names = new Dictionary<string, string> { ["chat"] = "对话", ["tools"] = "工具回填", ["models"] = "模型列表" };
            return string.Join(" · ", results.Where(row => names.ContainsKey(row.Key)).Select(row =>
                names[row.Key] + (row.Value.Success ? "成功 " : "未通过 ") + row.Value.Time.ToLocalTime().ToString("MM-dd HH:mm")));
        }

        private bool HasProbe(LlmConnectionDraft draft, string kind)
        {
            if (!probes.TryGetValue(draft, out ProbeState state)) return false;
            lock (state)
                return state.Results.TryGetValue(kind, out var record) && record.Item2.Success &&
                    record.Item1 == Fingerprint(draft, ResolveSecrets(draft));
        }

        private IReadOnlyDictionary<string, LlmProbeRecord> MatchingProbes(LlmConnectionDraft draft, string fingerprint)
        {
            if (!probes.TryGetValue(draft, out ProbeState state)) return new Dictionary<string, LlmProbeRecord>();
            lock (state) return state.Results.Where(row => row.Value.Item1 == fingerprint)
                .ToDictionary(row => row.Key, row => row.Value.Item2);
        }

        private void SetProbe(LlmConnectionDraft draft, LlmConnectionSnapshot connection, string kind, bool success)
        {
            ProbeState state = probes.GetOrCreateValue(draft);
            lock (state)
                state.Results[kind] = Tuple.Create(connection.WireIdentity, new LlmProbeRecord(success, DateTimeOffset.UtcNow));
        }
        private LlmConnectionSnapshot CaptureProbe(LlmConnectionDraft draft, bool inference)
        {
            // A saved disabled connection never sends automatically. An explicit discovery/test click may test a draft.
            LlmConnectionSecrets secrets = ResolveSecrets(draft);
            LlmConnectionDraft detached = CopyDraft(draft);
            return CaptureValidated(detached, secrets, inference, true);
        }

        private LlmConnectionSnapshot CaptureValidated(LlmConnectionDraft draft, LlmConnectionSecrets secrets, bool model, bool request)
        {
            presets.TryGetValue(draft.PresetId ?? "", out LlmPreset preset);
            draft.RoutingPreset = preset;
            LlmConnectionSnapshot connection = Capture(draft, preset, secrets, Fingerprint(draft, secrets), model, request);
            EnsureSelectedPlan(connection);
            (transport as ILlmConnectionValidator)?.Validate(connection);
            return connection;
        }

        private void EnsureSelectedPlan(LlmConnectionSnapshot connection)
        {
            LlmPreset current = connection.Preset;
            if (MatchesPresetEndpoint(current, connection.Endpoint)) return;
            LlmPreset[] matches = Presets.Where(preset => MatchesPresetEndpoint(preset, connection.Endpoint)).ToArray();
            if (matches.Length == 0) return;
            if (matches.All(match => match.Policy == "reference_only"))
                throw Error("此地址属于仅供参考的方案，游戏内直连不可用。");
            if (current.Provider != "custom" && !matches.Any(match => match.Provider == current.Provider &&
                match.PlanId == current.PlanId && match.Region == current.Region))
                throw Error("该地址属于另一个厂商、地区或计费方案，请选择对应预设并填写独立凭据。");
        }

        private void EnsureDiscoveryPlan(LlmConnectionSnapshot connection)
        {
            Uri discovery = connection.DiscoveryEndpoint;
            if (discovery == null)
                throw new LlmException(LlmFailure.DiscoveryUnavailable, "此连接没有可用的官方模型列表，请手工填写模型 ID。");

            LlmPreset current = connection.Preset;
            LlmConnectionSettings settings = connection.CopySettings();
            if (current.Provider != "custom")
            {
                if (current.DiscoveryStatus == "documented" &&
                    !string.Equals(settings.DiscoveryPath, current.DiscoveryPath, StringComparison.Ordinal))
                    throw new LlmException(LlmFailure.DiscoveryUnavailable, "官方连接使用固定的模型列表路径，请恢复预设或手工填写模型 ID。");
                return;
            }
            LlmPreset[] discoveryScopes = KnownRouteScopes(discovery);
            if (discoveryScopes.Length == 0) return;
            LlmPreset[] inferenceScopes = KnownRouteScopes(connection.Endpoint);
            if (!discoveryScopes.Any(discovered => discovered.Policy != "reference_only" &&
                inferenceScopes.Any(inference => inference.Policy != "reference_only" &&
                    discovered.Provider == inference.Provider && discovered.Region == inference.Region && discovered.PlanId == inference.PlanId)))
                throw new LlmException(LlmFailure.DiscoveryUnavailable, "模型列表路径与当前推理的已知地区或计费方案不一致，请手工填写模型 ID。");
        }

        private LlmPreset[] KnownRouteScopes(Uri endpoint)
        {
            // A longer verified Base path takes precedence on hosts that also have an ordinary API.
            // A path prefix must end at a segment boundary to identify the same service.
            var matches = new List<Tuple<LlmPreset, int>>();
            foreach (LlmPreset preset in Presets)
            {
                if (preset.BaseUrl == null || preset.BaseUrl.Contains("{") ||
                    !Uri.TryCreate(preset.BaseUrl, UriKind.Absolute, out Uri root) || root.Scheme != endpoint.Scheme ||
                    root.IdnHost != endpoint.IdnHost || root.Port != endpoint.Port) continue;
                string prefix = root.AbsolutePath.TrimEnd('/');
                if (string.Equals(endpoint.AbsolutePath, prefix, StringComparison.OrdinalIgnoreCase) ||
                    endpoint.AbsolutePath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                    matches.Add(Tuple.Create(preset, prefix.Length));
            }
            int longest = matches.Select(match => match.Item2).DefaultIfEmpty(-1).Max();
            return matches.Where(match => match.Item2 == longest).Select(match => match.Item1).ToArray();
        }

        private static bool MatchesPresetEndpoint(LlmPreset preset, Uri endpoint)
        {
            if (preset.BaseUrl == null || preset.InferencePath == null ||
                !Uri.TryCreate(preset.BaseUrl.TrimEnd('/') + preset.InferencePath, UriKind.Absolute, out Uri configured))
                return false;
            return Uri.Compare(configured, endpoint, UriComponents.HttpRequestUrl, UriFormat.UriEscaped, StringComparison.Ordinal) == 0;
        }

        private Dictionary<Guid, LlmConnectionSnapshot> BuildSnapshots(LlmStoredConfiguration candidate)
        {
            var result = new Dictionary<Guid, LlmConnectionSnapshot>();
            foreach (LlmStoredConnection row in candidate.Connections.Values)
            {
                LlmConnectionDraft draft = Draft(row, candidate);
                candidate.Secrets.TryGetValue(row.SecretVersion, out LlmConnectionSecrets secrets);
                try
                {
                    result[row.Id] = CaptureValidated(draft, secrets ?? new LlmConnectionSecrets(null, null, null), true, true);
                }
                catch (LlmException) { }
            }
            return result;
        }

        private LlmConnectionDraft Draft(LlmStoredConnection row, LlmStoredConfiguration configuration)
        {
            var draft = new LlmConnectionDraft(row.Id) { SecretVersion = row.SecretVersion };
            row.Settings.CopyTo(draft);
            presets.TryGetValue(draft.PresetId ?? "", out LlmPreset preset);
            draft.RoutingPreset = preset;
            draft.SecretScope = draft.CredentialScope();
            draft.HasStoredKey = configuration.Secrets.TryGetValue(row.SecretVersion, out LlmConnectionSecrets secrets) &&
                secrets.ApiKey.Length != 0;
            return draft;
        }

        private static LlmConnectionDraft CopyDraft(LlmConnectionDraft draft)
        {
            var copy = new LlmConnectionDraft(draft.Id) { SecretVersion = draft.SecretVersion, RoutingPreset = draft.RoutingPreset };
            draft.CopyTo(copy);
            return copy;
        }

        private LlmConnectionSecrets ResolveSecrets(LlmConnectionDraft draft)
        {
            string current = draft.CredentialScope();
            LlmConnectionSecrets saved = null;
            lock (gate)
            {
                if (current == draft.SecretScope && draft.SecretVersion != Guid.Empty &&
                    configuration.Connections.TryGetValue(draft.Id, out LlmStoredConnection row) && row.SecretVersion == draft.SecretVersion)
                    configuration.Secrets.TryGetValue(draft.SecretVersion, out saved);
            }
            return new LlmConnectionSecrets(
                draft.ApiKey != null && current == draft.ApiKeyScope ? draft.ApiKey : saved?.ApiKey,
                draft.SecretHeaders != null && current == draft.HeaderScope ? draft.SecretHeaders : saved?.Headers,
                draft.ProxyCredentials != null && current == draft.ProxyScope ? draft.ProxyCredentials : saved?.ProxyCredentials);
        }

        private async Task ChangeAsync(Action<LlmStoredConfiguration> apply, bool wait = false)
        {
            if (wait) await writes.WaitAsync().ConfigureAwait(false);
            else if (!await writes.WaitAsync(0).ConfigureAwait(false))
                throw Error("另一个模型配置操作正在保存。");
            try
            {
                await Task.Run(async () =>
                {
                    LlmStoredConfiguration candidate;
                    lock (gate)
                    {
                        if (!ready) throw new LlmException(LlmFailure.Storage, storageStatus);
                        candidate = Copy(configuration);
                    }
                    apply(candidate);
                    var usedSecrets = new HashSet<Guid>(candidate.Connections.Values.Select(row => row.SecretVersion));
                    foreach (Guid secret in candidate.Secrets.Keys.ToArray())
                        if (!usedSecrets.Contains(secret)) candidate.Secrets.Remove(secret);
                    Dictionary<Guid, LlmConnectionSnapshot> snapshots = BuildSnapshots(candidate);
                    await store.SaveAsync(candidate).ConfigureAwait(false);
                    lock (gate)
                    {
                        configuration = candidate; runnable = snapshots;
                        storageStatus = candidate.SecretStatus ?? "";
                    }
                }).ConfigureAwait(false);
            }
            catch (LlmException exception)
            {
                if (exception.Failure == LlmFailure.Storage)
                    lock (gate) storageStatus = exception.Message;
                throw;
            }
            finally { writes.Release(); }
        }

        private static LlmStoredConfiguration Copy(LlmStoredConfiguration current)
        {
            var copy = new LlmStoredConfiguration { Enabled = current.Enabled, DefaultId = current.DefaultId, SecretStatus = current.SecretStatus };
            foreach (var pair in current.Connections) copy.Connections.Add(pair.Key, pair.Value);
            foreach (var pair in current.Secrets) copy.Secrets.Add(pair.Key, pair.Value);
            foreach (var pair in current.Bindings) copy.Bindings.Add(pair.Key, pair.Value);
            return copy;
        }

        private static void EnsureExists(LlmStoredConfiguration candidate, Guid id)
        {
            if (id != Guid.Empty && !candidate.Connections.ContainsKey(id)) throw Error("这条连接已删除。");
        }

        private static bool SameSecrets(LlmConnectionSecrets a, LlmConnectionSecrets b) =>
            a.ApiKey == b.ApiKey && a.Headers == b.Headers && a.ProxyCredentials == b.ProxyCredentials;

        private static readonly PropertyInfo[] RequestOptions = typeof(LlmConnectionSettings).GetProperties()
            .Where(property => property.Name != nameof(LlmConnectionSettings.DisplayName)).ToArray();

        // Used in memory only, with length prefixes so option values cannot alias each other.
        private static string Fingerprint(LlmConnectionDraft settings, LlmConnectionSecrets secrets)
        {
            var value = new StringBuilder();
            AppendFingerprint(value, settings.Id.ToString("D"));
            AppendFingerprint(value, settings.SecretVersion.ToString("D"));
            AppendFingerprint(value, settings.CredentialScope());
            foreach (PropertyInfo property in RequestOptions)
                AppendFingerprint(value, Convert.ToString(property.GetValue(settings), System.Globalization.CultureInfo.InvariantCulture));
            AppendFingerprint(value, secrets.ApiKey);
            AppendFingerprint(value, secrets.Headers);
            AppendFingerprint(value, secrets.ProxyCredentials);
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value.ToString())));
        }

        private static void AppendFingerprint(StringBuilder buffer, string value)
        {
            value = value ?? "";
            buffer.Append(value.Length).Append(':').Append(value);
        }

        private sealed class ProbeState
        {
            public Dictionary<string, Tuple<string, LlmProbeRecord>> Results { get; } =
                new Dictionary<string, Tuple<string, LlmProbeRecord>>();
        }
    }
}
