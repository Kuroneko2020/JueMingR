using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace JueMingR.Platform.Llm
{
    public sealed class LlmPreset
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Provider { get; set; }
        public string Region { get; set; }
        public string RegionLabel { get; set; }
        public string PlanId { get; set; }
        public string PlanLabel { get; set; }
        public string Policy { get; set; }
        public string Note { get; set; }
        public string VerifiedAt { get; set; }
        public string Protocol { get; set; }
        public bool DefaultForProvider { get; set; }
        public bool Advanced { get; set; }
        public string BaseUrl { get; set; }
        public string InferencePath { get; set; }
        public string DiscoveryStatus { get; set; }
        public string DiscoveryPath { get; set; }
        public string DiscoveryUrl { get; set; }
        public string DiscoveryFormat { get; set; }
        public string DiscoveryAuthMode { get; set; }
        public string AuthMode { get; set; }
        public bool AuthRequired { get; set; }
        public string AnthropicVersion { get; set; }
        public IReadOnlyList<string> Aliases { get; set; }
        public IReadOnlyList<string> Sources { get; set; }
        public IReadOnlyList<string> Candidates { get; set; }
        public IReadOnlyList<string> CredentialKinds { get; set; }
        public IReadOnlyList<string> FundingSources { get; set; }
        public IReadOnlyList<string> AcceptedAccountPlans { get; set; }
        public string AuthHeader { get; set; }
        public string DiscoveryAuthHeader { get; set; }
    }

    // The settings type is the explicit allowlist for the public configuration file.
    // Secret input lives on the draft only and is never copied through these properties.
    public class LlmConnectionSettings
    {
        public string DisplayName { get; set; } = "";
        public string PresetId { get; set; } = "";
        public string Protocol { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string InferencePath { get; set; } = "";
        public string DiscoveryPath { get; set; } = "";
        public string AuthMode { get; set; } = "bearer";
        public string CredentialKind { get; set; } = "";
        public string AnthropicVersion { get; set; } = "";
        public string Workspace { get; set; } = "";
        public string ProxyMode { get; set; } = "system";
        public string ProxyUrl { get; set; } = "";
        public string Model { get; set; } = "";
        public string Reasoning { get; set; } = "";
        public string StopSequences { get; set; } = "";
        public string ExtraParameters { get; set; } = "";
        public string ExtraHeaders { get; set; } = "";
        public bool Stream { get; set; } = true;
        public bool GameActionsEnabled { get; set; } = true;
        public bool LocalNoticesEnabled { get; set; } = true;
        public int? MaxOutput { get; set; }
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public int InputBudget { get; set; } = 12000;
        public int HistoryLimit { get; set; } = 100;
        public int MaxModelTurns { get; set; } = 4;
        public int MaxToolCalls { get; set; } = 8;
        public int RunTimeoutSeconds { get; set; } = 90;
        public int FirstByteSeconds { get; set; } = 25;
        public int IdleSeconds { get; set; } = 20;
        public int RetryCount { get; set; } = 1;

        private static readonly PropertyInfo[] PublicOptions = typeof(LlmConnectionSettings).GetProperties();
        public void CopyTo(LlmConnectionSettings target)
        {
            foreach (PropertyInfo option in PublicOptions)
                option.SetValue(target, option.GetValue(this));
        }
    }

    public sealed class LlmConnectionDraft : LlmConnectionSettings
    {
        public LlmConnectionDraft(Guid id) { Id = id; }
        public Guid Id { get; }
        public bool HasStoredKey { get; set; }
        public LlmPreset RoutingPreset { get; set; }
        private string apiKey;
        private string secretHeaders;
        private string proxyCredentials;
        public string ApiKey { get => apiKey; set { apiKey = value; ApiKeyScope = CredentialScope(); } }
        public string SecretHeaders { get => secretHeaders; set { secretHeaders = value; HeaderScope = CredentialScope(); } }
        public string ProxyCredentials { get => proxyCredentials; set { proxyCredentials = value; ProxyScope = CredentialScope(); } }
        public string ApiKeyScope { get; private set; }
        public string HeaderScope { get; private set; }
        public string ProxyScope { get; private set; }

        public string CredentialScope()
        {
            string expanded = (BaseUrl ?? "").Replace("{workspaceId}", Workspace ?? "");
            string endpoint = expanded.Trim().TrimEnd('/') + InferencePath;
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri url))
                endpoint = url.GetLeftPart(UriPartial.Authority).ToLowerInvariant() + url.AbsolutePath;
            // Plans can share a host but use different paths, or share a complete endpoint with independent credentials.
            var normalized = new System.Text.StringBuilder();
            string discovery = DiscoveryEndpoint()?.AbsoluteUri;
            foreach (string part in new[] { PresetId, Protocol, endpoint, discovery, Workspace, AuthMode, CredentialKind, ProxyMode, ProxyUrl })
            {
                string value = part ?? "";
                normalized.Append(value.Length).Append(':').Append(value);
            }
            return normalized.ToString();
        }
        // One route calculation serves both request capture and the credential binding.
        public Uri DiscoveryEndpoint()
        {
            LlmPreset preset = RoutingPreset;
            if (preset == null || preset.Policy == "reference_only") return null;
            Uri configured = ParseEndpoint(Expand(BaseUrl));
            if (configured == null) return null;
            if (preset.Provider == "custom")
                return ParseEndpoint(Join(configured, DiscoveryPath));

            Uri documented = ParseEndpoint(Expand(preset.BaseUrl));
            if (documented == null) return null;
            Uri configuredInference = ParseEndpoint(Join(configured, InferencePath));
            Uri documentedInference = ParseEndpoint(Join(documented, preset.InferencePath));
            if (!Uri.Equals(configured, documented) && (documentedInference == null || !Uri.Equals(configuredInference, documentedInference)))
                return null;
            if (preset.DiscoveryStatus == "provider_specific")
                return ParseEndpoint(Expand(preset.DiscoveryUrl));
            return preset.DiscoveryStatus == "documented" ? ParseEndpoint(Join(documented, preset.DiscoveryPath)) : null;
        }

        private string Expand(string path) => (path ?? "").Replace("{workspaceId}", Workspace ?? "");
        private static Uri ParseEndpoint(string path) => Uri.TryCreate(path.Trim(), UriKind.Absolute, out Uri uri) ? uri : null;
        private static string Join(Uri location, string path) =>
            string.IsNullOrWhiteSpace(path) ? "" : location.AbsoluteUri.TrimEnd('/') + path;

        public string SecretScope { get; set; }
        public Guid SecretVersion { get; set; }
    }

    public sealed class LlmConnectionInfo
    {
        public LlmConnectionInfo(Guid id, LlmConnectionSettings settings)
        {
            Id = id; DisplayName = settings.DisplayName; PresetId = settings.PresetId; Model = settings.Model;
        }
        public Guid Id { get; }
        public string DisplayName { get; }
        public string PresetId { get; }
        public string Model { get; }
    }

    public sealed class LlmConnectionSecrets
    {
        public LlmConnectionSecrets(string apiKey, string headers, string proxyCredentials)
        {
            ApiKey = apiKey ?? ""; Headers = headers ?? ""; ProxyCredentials = proxyCredentials ?? "";
        }

        public string ApiKey { get; }
        public string Headers { get; }
        public string ProxyCredentials { get; }
        public bool IsEmpty => ApiKey.Length == 0 && Headers.Length == 0 && ProxyCredentials.Length == 0;
    }

    public sealed class LlmConnectionSnapshot
    {
        private readonly LlmConnectionSettings settings;

        public LlmConnectionSnapshot(Guid id, string wireIdentity, LlmConnectionSettings options, LlmPreset preset,
            LlmConnectionSecrets secrets, Uri endpoint, Uri discoveryEndpoint)
        {
            Id = id; WireIdentity = wireIdentity; Preset = preset; Secrets = secrets;
            Endpoint = endpoint; DiscoveryEndpoint = discoveryEndpoint;
            settings = new LlmConnectionSettings();
            options.CopyTo(settings);
        }

        public Guid Id { get; }
        public string WireIdentity { get; }
        public LlmPreset Preset { get; }
        public LlmConnectionSecrets Secrets { get; }
        public Uri Endpoint { get; }
        public Uri DiscoveryEndpoint { get; }
        public string DisplayName => settings.DisplayName;
        public string Model => settings.Model;
        public bool GameActionsEnabled => settings.GameActionsEnabled;
        public bool LocalNoticesEnabled => settings.LocalNoticesEnabled;
        public int MaxModelTurns => settings.MaxModelTurns;
        public int MaxToolCalls => settings.MaxToolCalls;
        public int InputBudget => settings.InputBudget;
        public int HistoryLimit => settings.HistoryLimit;
        public int RunTimeoutSeconds => settings.RunTimeoutSeconds;

        public LlmConnectionSettings CopySettings()
        {
            var copy = new LlmConnectionSettings();
            settings.CopyTo(copy);
            return copy;
        }
    }

    public sealed class LlmProbeRecord
    {
        public LlmProbeRecord(bool success, DateTimeOffset time)
        {
            Success = success; Time = time;
        }
        public bool Success { get; }
        public DateTimeOffset Time { get; }
    }

    public sealed class LlmStoredConnection
    {
        public LlmStoredConnection(Guid id, LlmConnectionSettings options, Guid secretVersion,
            IReadOnlyDictionary<string, LlmProbeRecord> probes = null)
        {
            Id = id; Settings = options; SecretVersion = secretVersion;
            Probes = probes ?? new Dictionary<string, LlmProbeRecord>();
        }

        public Guid Id { get; }
        public LlmConnectionSettings Settings { get; }
        public Guid SecretVersion { get; }
        public IReadOnlyDictionary<string, LlmProbeRecord> Probes { get; }
    }

    public sealed class LlmStoredConfiguration
    {
        public bool Enabled { get; set; }
        public Guid? DefaultId { get; set; }
        public Dictionary<Guid, LlmStoredConnection> Connections { get; } = new Dictionary<Guid, LlmStoredConnection>();
        public Dictionary<Guid, LlmConnectionSecrets> Secrets { get; } = new Dictionary<Guid, LlmConnectionSecrets>();
        public Dictionary<Tuple<Guid, Guid>, Guid> Bindings { get; } = new Dictionary<Tuple<Guid, Guid>, Guid>();
        public string SecretStatus { get; set; }
    }

    public interface ILlmConnectionValidator
    {
        void Validate(LlmConnectionSnapshot connection);
    }

    public interface ILlmStore
    {
        Task<LlmStoredConfiguration> LoadAsync();
        Task SaveAsync(LlmStoredConfiguration configuration);
        Task<IReadOnlyList<LlmHistoryEntry>> LoadHistoryAsync(Guid owner, Guid profile);
        Task SaveHistoryAsync(Guid owner, Guid profile, IReadOnlyList<LlmHistoryEntry> entries);
        Task ClearHistoryAsync(Guid owner, Guid profile);
    }
}
