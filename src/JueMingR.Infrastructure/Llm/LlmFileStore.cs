using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Llm;
using JueMingR.Platform.Settings;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    public sealed class LlmFileStore : ILlmStore, ILlmChatArchiveStore, IDisposable
    {
        private const int SettingsBytes = 2 * 1024 * 1024;
        private const int SecretsBytes = 2 * 1024 * 1024;
        private const int HistoryBytes = 2 * 1024 * 1024;
        private const string SettingsFormat = "JueMingR.Llm.Connections";
        private const string SecretsFormat = "JueMingR.Llm.Credentials";
        private const string HistoryFormat = "JueMingR.Llm.PublicHistory";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly PropertyInfo[] PersistedOptions = typeof(LlmConnectionSettings).GetProperties();
        private static readonly Lazy<Dictionary<string, LlmPreset>> RoutingPresets = new Lazy<Dictionary<string, LlmPreset>>(
            () => LlmPresetCatalog.Load().ToDictionary(preset => preset.Id, StringComparer.Ordinal));
        private readonly string directory;
        private readonly AtomicFileDocument settingsFile;
        private readonly AtomicFileDocument secretsFile;
        private readonly SemaphoreSlim connectionWork = new SemaphoreSlim(1);
        private readonly SemaphoreSlim historyWork = new SemaphoreSlim(1);
        private readonly HashSet<string> protectedHistories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> historyIdentities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string settingsIdentity;
        private string secretsIdentity;
        private bool settingsProtected;
        private bool secretsProtected;
        private Dictionary<Guid, string> encrypted = new Dictionary<Guid, string>();
        private HashSet<Guid> liveReferences = new HashSet<Guid>();

        public LlmFileStore(string fullGameDirectory)
        {
            if (string.IsNullOrWhiteSpace(fullGameDirectory) || !Path.IsPathRooted(fullGameDirectory) ||
                !string.Equals(Path.GetPathRoot(fullGameDirectory), Path.GetPathRoot(Path.GetFullPath(fullGameDirectory)),
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An absolute, host-resolved game directory is required.", nameof(fullGameDirectory));
            directory = Path.Combine(Path.GetFullPath(fullGameDirectory), "JueMingRData", "Llm");
            settingsFile = new AtomicFileDocument(Path.Combine(directory, "connections.json"), SettingsBytes, true);
            secretsFile = new AtomicFileDocument(Path.Combine(directory, "credentials.json"), SecretsBytes, true);
        }

        public Task<LlmStoredConfiguration> LoadAsync() => RunAsync(connectionWork, Load);

        private LlmStoredConfiguration Load()
        {
            var result = new LlmStoredConfiguration();
            try
            {
                object document = Read(settingsFile, SettingsFormat, "模型连接配置", out settingsIdentity);
                if (document != null) DecodeSettings(document, result);
            }
            catch (Exception exception) when (IsDocumentError(exception))
            {
                settingsProtected = true;
                throw ForDocument(exception, "模型连接配置");
            }

            liveReferences = References(result);
            var scopes = result.Connections.Values.Where(row => row.SecretVersion != Guid.Empty)
                .ToDictionary(row => row.SecretVersion);
            try
            {
                object document = Read(secretsFile, SecretsFormat, "受保护凭据", out secretsIdentity);
                if (document == null)
                {
                    if (liveReferences.Count != 0) result.SecretStatus = "受保护凭据文件缺失，请重新填写 API Key。";
                    return result;
                }
                Shape(document, "format", "version", "entries");
                var rows = RequireMap(Get(document, "entries"));
                if (rows.Count > 144) throw Protocol();
                foreach (var row in rows)
                {
                    Guid id = Id(row.Key);
                    if (!(row.Value is string ciphertext) || ciphertext.Length > 32768) throw Protocol();
                    encrypted.Add(id, ciphertext);
                    if (!liveReferences.Contains(id)) continue;
                    try { result.Secrets.Add(id, Unprotect(scopes[id], ciphertext)); }
                    catch (Exception exception) when (IsDocumentError(exception))
                    {
                        result.SecretStatus = "有些连接的受保护凭据无法解密，请重新填写。";
                    }
                }
            }
            catch (Exception exception) when (IsDocumentError(exception))
            {
                secretsProtected = true;
                result.Secrets.Clear();
                result.SecretStatus = ForDocument(exception, "受保护凭据").Message;
            }
            if (liveReferences.Any(id => !result.Secrets.ContainsKey(id)) && result.SecretStatus == null)
                result.SecretStatus = "有些连接缺少受保护凭据，请重新填写。";
            return result;
        }

        public Task SaveAsync(LlmStoredConfiguration configuration) => RunAsync(connectionWork, () =>
        {
            if (settingsProtected || settingsIdentity == null)
                throw Storage("模型连接配置受保护，未覆盖原文件。");
            // These immutable reference IDs allow revising the credentials file first without breaking
            // either the currently published settings or the preceding settings backup.
            HashSet<Guid> next = References(configuration);
            bool hasNew = configuration.Secrets.Keys.Any(id => !encrypted.ContainsKey(id));
            if (hasNew && (secretsProtected || secretsIdentity == null))
                throw Storage("受保护凭据无法保存，未更改连接配置。");
            byte[] publicBytes = EncodeSettings(configuration);
            if (!secretsProtected && secretsIdentity != null && (hasNew || encrypted.Keys.Any(id => !liveReferences.Contains(id) && !next.Contains(id))))
            {
                var retained = encrypted.Where(row => liveReferences.Contains(row.Key) || next.Contains(row.Key))
                    .ToDictionary(row => row.Key, row => row.Value);
                foreach (LlmStoredConnection row in configuration.Connections.Values)
                    if (configuration.Secrets.TryGetValue(row.SecretVersion, out LlmConnectionSecrets secret) &&
                        !retained.ContainsKey(row.SecretVersion))
                        retained.Add(row.SecretVersion, Protect(row, secret));
                byte[] privateBytes = Bytes(Document(SecretsFormat, "entries", retained.ToDictionary(row => row.Key.ToString("D"), row => row.Value)), SecretsBytes);
                try { secretsIdentity = Commit(secretsFile, secretsIdentity, privateBytes, "受保护凭据"); }
                catch (LlmException) { secretsProtected = true; throw; }
                encrypted = retained;
            }
            try { settingsIdentity = Commit(settingsFile, settingsIdentity, publicBytes, "模型连接配置"); }
            catch (LlmException) { settingsProtected = true; throw; }
            liveReferences = next;
            if (!secretsProtected)
                configuration.SecretStatus = next.Any(id => !configuration.Secrets.ContainsKey(id))
                    ? "有些连接缺少可用的受保护凭据，请重新填写。" : null;
            return true;
        });

        public async Task<IReadOnlyList<LlmHistoryEntry>> LoadHistoryAsync(Guid owner, Guid profile) =>
            (await LoadChatArchiveAsync(owner, profile).ConfigureAwait(false)).Entries;

        public Task<LlmChatArchive> LoadChatArchiveAsync(Guid owner, Guid profile) =>
            RunAsync(historyWork, () =>
            {
                string path = HistoryPath(owner, profile);
                using (var file = new AtomicFileDocument(path, HistoryBytes, true))
                {
                    var archive = ReadHistory(file, path, out string identity);
                    AcceptHistoryIdentity(path, identity);
                    return archive;
                }
            });

        public Task SaveHistoryAsync(Guid owner, Guid profile, IReadOnlyList<LlmHistoryEntry> entries)
        {
            LlmHistoryEntry[] accepted = entries.ToArray();
            return RunAsync(historyWork, () => WriteHistory(owner, profile, accepted, null, false));
        }

        public Task SaveChatArchiveAsync(Guid owner, Guid profile, LlmChatArchive archive) =>
            RunAsync(historyWork, () => WriteHistory(owner, profile, archive.Entries, archive.Usage, false));

        public Task ClearHistoryAsync(Guid owner, Guid profile) =>
            RunAsync(historyWork, () => WriteHistory(owner, profile, System.Array.Empty<LlmHistoryEntry>(), LlmChatUsage.Empty, true));

        private bool WriteHistory(Guid owner, Guid profile, IReadOnlyList<LlmHistoryEntry> entries, LlmChatUsage usage, bool clear)
        {
            string path = HistoryPath(owner, profile);
            if (protectedHistories.Contains(path)) throw Storage("聊天历史受保护，未覆盖原件。");
            using (var file = new AtomicFileDocument(path, HistoryBytes, true))
            {
                var existing = ReadHistory(file, path, out string identity);
                AcceptHistoryIdentity(path, identity);
                if (clear && identity == "missing") return true;
                byte[] bytes = EncodeHistory(entries, usage ?? existing.Usage);
                try
                {
                    string committed = Commit(file, identity, bytes, "聊天历史");
                    // Replace a previous readable backup with the same empty history on an explicit clear.
                    if (clear) committed = Commit(file, committed, bytes, "聊天历史备份");
                    historyIdentities[path] = committed;
                }
                catch (LlmException)
                {
                    protectedHistories.Add(path);
                    throw;
                }
            }
            return true;
        }

        private void AcceptHistoryIdentity(string path, string identity)
        {
            if (protectedHistories.Contains(path) ||
                (historyIdentities.TryGetValue(path, out string accepted) && identity != accepted))
            {
                protectedHistories.Add(path);
                throw Storage("聊天历史已被其他进程改动，原件已保留，本次未写入。");
            }
            historyIdentities[path] = identity;
        }

        private static byte[] EncodeHistory(IReadOnlyList<LlmHistoryEntry> entries, LlmChatUsage usage)
        {
            // Keep the newest complete visible records within the actual UTF-8 budget, including JSON escapes.
            var envelope = Document(HistoryFormat, "entries", System.Array.Empty<object>());
            if (usage.TrackingStartedAt.HasValue)
                envelope["usage"] = new Dictionary<string, object>
                {
                    ["since"] = usage.TrackingStartedAt.Value.ToString("O", CultureInfo.InvariantCulture),
                    ["latest"] = UsageRow(usage.LatestConnection),
                    ["connections"] = usage.Connections.Select(UsageRow).ToArray()
                };
            int remaining = HistoryBytes - Bytes(envelope, HistoryBytes).Length - 16;
            var accepted = new List<object>();
            for (int index = entries.Count - 1; index >= 0 && accepted.Count < 500; index--)
            {
                LlmHistoryEntry entry = entries[index];
                var row = new Dictionary<string, object>
                {
                    ["id"] = entry.Id.ToString("D"), ["kind"] = entry.Kind, ["text"] = entry.Text,
                    ["time"] = entry.Time.ToString("O", CultureInfo.InvariantCulture)
                };
                int size = Utf8.GetByteCount(Write(row)) + 1;
                if (size > remaining) break;
                remaining -= size;
                accepted.Add(row);
            }
            accepted.Reverse();
            envelope["entries"] = accepted;
            return Bytes(envelope, HistoryBytes);
        }

        private LlmChatArchive ReadHistory(AtomicFileDocument file, string path, out string identity)
        {
            try
            {
                object document = Read(file, HistoryFormat, "聊天历史", out identity);
                if (document == null) return new LlmChatArchive(System.Array.Empty<LlmHistoryEntry>(), null);
                bool tracked = Has(document, "usage");
                Shape(document, tracked ? new[] { "format", "version", "entries", "usage" } : new[] { "format", "version", "entries" });
                object[] rows = Records(document, "entries", 500);
                var result = new LlmHistoryEntry[rows.Length];
                for (int i = 0; i < rows.Length; i++)
                {
                    object item = rows[i];
                    Shape(item, "id", "kind", "text", "time");
                    string kind = Text(item, "kind"), text = Text(item, "text");
                    if (string.IsNullOrEmpty(kind) || kind.Length > 32 || text == null ||
                        !DateTimeOffset.TryParseExact(Text(item, "time"), "O", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTimeOffset time))
                        throw Protocol();
                    result[i] = new LlmHistoryEntry(Id(Text(item, "id")), kind, text, time);
                }
                return new LlmChatArchive(result, tracked ? ReadUsage(Get(document, "usage")) : null);
            }
            catch (Exception exception) when (IsDocumentError(exception))
            {
                protectedHistories.Add(path);
                throw ForDocument(exception, "聊天历史");
            }
        }

        private static object UsageRow(LlmConnectionUsage row)
        {
            if (row == null) return null;
            var count = row.Totals;
            return new Dictionary<string, object>
            {
                ["id"] = row.ConnectionId.ToString("D"), ["connection"] = row.Connection, ["model"] = row.Model,
                ["requests"] = count.Requests, ["unknown"] = count.UnknownRequests,
                ["input"] = count.Input, ["output"] = count.Output, ["total"] = count.Total, ["read"] = count.CacheRead,
                ["write"] = count.CacheWrite, ["reasoning"] = count.Reasoning
            };
        }

        private static LlmChatUsage ReadUsage(object value)
        {
            Shape(value, "since", "latest", "connections");
            if (!DateTimeOffset.TryParseExact(Text(value, "since"), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var since))
                throw Protocol();
            object latest = Get(value, "latest");
            var connections = Records(value, "connections", 512).Select(ReadUsageRow).ToArray();
            if (connections.Select(row => (row.ConnectionId, row.Model)).Distinct().Count() != connections.Length) throw Protocol();
            return new LlmChatUsage(since, latest == null ? null : ReadUsageRow(latest), connections);
        }

        private static LlmConnectionUsage ReadUsageRow(object row)
        {
            Shape(row, "id", "connection", "model", "requests", "unknown", "input", "output", "total", "read", "write", "reasoning");
            string connection = Text(row, "connection"), model = Text(row, "model");
            if (connection == null || connection.Length > 240 || model == null || model.Length > 240) throw Protocol();
            long requests = UsageCounter(row, "requests") ?? throw Protocol();
            long unknown = UsageCounter(row, "unknown") ?? throw Protocol();
            if (unknown > requests) throw Protocol();
            return new LlmConnectionUsage(Id(Text(row, "id"), emptyAllowed: true), connection, model,
                new LlmUsageTotals(requests, unknown, UsageCounter(row, "input"), UsageCounter(row, "output"),
                    UsageCounter(row, "total"), UsageCounter(row, "read"), UsageCounter(row, "write"), UsageCounter(row, "reasoning")));
        }

        private static long? UsageCounter(object row, string name)
        {
            object raw = Get(row, name);
            if (raw == null) return null;
            if (raw is int integer && integer >= 0) return integer;
            if (raw is long longer && longer >= 0) return longer;
            throw Protocol();
        }

        private string HistoryPath(Guid owner, Guid profile)
        {
            if (owner == Guid.Empty || profile == Guid.Empty) throw Storage("聊天归属尚未确定。");
            return Path.Combine(directory, "history", owner.ToString("N"), profile.ToString("N") + ".json");
        }

        private static byte[] EncodeSettings(LlmStoredConfiguration configuration)
        {
            var root = Document(SettingsFormat, "connections", configuration.Connections.Values.Select(row => new Dictionary<string, object>
            {
                ["id"] = row.Id.ToString("D"), ["settings"] = row.Settings, ["secretRef"] = row.SecretVersion.ToString("D"),
                ["probes"] = row.Probes.ToDictionary(probe => probe.Key, probe => new Dictionary<string, object>
                {
                    ["success"] = probe.Value.Success, ["time"] = probe.Value.Time.ToString("O", CultureInfo.InvariantCulture)
                })
            }).ToArray());
            root["enabled"] = configuration.Enabled;
            root["defaultConnection"] = configuration.DefaultId?.ToString("D");
            root["bindings"] = configuration.Bindings.Select(row => new Dictionary<string, object>
            {
                ["owner"] = row.Key.Item1.ToString("D"), ["profile"] = row.Key.Item2.ToString("D"), ["connection"] = row.Value.ToString("D")
            }).ToArray();
            byte[] bytes = Bytes(root, SettingsBytes);
            DecodeSettings(Parse(Utf8.GetString(bytes)), new LlmStoredConfiguration());
            return bytes;
        }

        private static void DecodeSettings(object document, LlmStoredConfiguration result)
        {
            Shape(document, "format", "version", "connections", "enabled", "defaultConnection", "bindings");
            if (!(Get(document, "enabled") is bool enabled)) throw Protocol();
            result.Enabled = enabled;
            object selected = Get(document, "defaultConnection");
            result.DefaultId = selected == null ? (Guid?)null : Id(selected as string, true);
            var secretReferences = new HashSet<Guid>();
            foreach (object row in Records(document, "connections", 48))
            {
                Shape(row, "id", "settings", "secretRef", "probes");
                Guid id = Id(Text(row, "id")), reference = Id(Text(row, "secretRef"), true);
                if (reference != Guid.Empty && !secretReferences.Add(reference)) throw Protocol();
                object settings = Get(row, "settings");
                Shape(settings, PersistedOptions.Select(option => option.Name).ToArray());
                foreach (PropertyInfo option in PersistedOptions)
                {
                    object value = Get(settings, option.Name);
                    Type nullable = Nullable.GetUnderlyingType(option.PropertyType);
                    Type scalar = nullable ?? option.PropertyType;
                    bool valid = value == null ? nullable != null || scalar == typeof(string) :
                        scalar.IsInstanceOfType(value) || scalar == typeof(double) &&
                        (value is int || value is decimal || value is long);
                    if (!valid) throw Protocol();
                }
                var probes = new Dictionary<string, LlmProbeRecord>(StringComparer.Ordinal);
                foreach (var probe in RequireMap(Get(row, "probes")))
                {
                    Shape(probe.Value, "success", "time");
                    if (probe.Key != "chat" && probe.Key != "tools" && probe.Key != "models" ||
                        !(Get(probe.Value, "success") is bool success) ||
                        !DateTimeOffset.TryParseExact(Text(probe.Value, "time"), "O", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTimeOffset time)) throw Protocol();
                    probes.Add(probe.Key, new LlmProbeRecord(success, time));
                }
                result.Connections.Add(id, new LlmStoredConnection(id, Convert<LlmConnectionSettings>(settings), reference, probes));
            }
            foreach (object row in Records(document, "bindings", 512))
            {
                Shape(row, "owner", "profile", "connection");
                result.Bindings.Add(Tuple.Create(Id(Text(row, "owner")), Id(Text(row, "profile"))), Id(Text(row, "connection"), true));
            }
        }

        private static HashSet<Guid> References(LlmStoredConfiguration configuration) =>
            new HashSet<Guid>(configuration.Connections.Values.Select(row => row.SecretVersion).Where(id => id != Guid.Empty));

        private static string Protect(LlmStoredConnection connection, LlmConnectionSecrets secret)
        {
            byte[] clear = null;
            try
            {
                clear = Utf8.GetBytes(Write(new Dictionary<string, string>
                {
                    ["key"] = secret.ApiKey, ["headers"] = secret.Headers, ["proxy"] = secret.ProxyCredentials
                }));
                return System.Convert.ToBase64String(ProtectedData.Protect(clear, Entropy(connection), DataProtectionScope.CurrentUser));
            }
            catch (Exception exception) when (IsDocumentError(exception))
            {
                throw Storage("Windows 无法保护当前凭据，未保存本次连接设置。");
            }
            finally { if (clear != null) System.Array.Clear(clear, 0, clear.Length); }
        }

        private static LlmConnectionSecrets Unprotect(LlmStoredConnection connection, string ciphertext)
        {
            byte[] clear = ProtectedData.Unprotect(System.Convert.FromBase64String(ciphertext), Entropy(connection), DataProtectionScope.CurrentUser);
            try
            {
                object decoded = Parse(Utf8.GetString(clear));
                Shape(decoded, "key", "headers", "proxy");
                if (!(Get(decoded, "key") is string key) || !(Get(decoded, "headers") is string headers) ||
                    !(Get(decoded, "proxy") is string proxy))
                    throw Protocol();
                return new LlmConnectionSecrets(key, headers, proxy);
            }
            finally { System.Array.Clear(clear, 0, clear.Length); }
        }

        private static byte[] Entropy(LlmStoredConnection connection)
        {
            var scope = new LlmConnectionDraft(connection.Id);
            connection.Settings.CopyTo(scope);
            RoutingPresets.Value.TryGetValue(scope.PresetId ?? "", out LlmPreset preset);
            scope.RoutingPreset = preset;
            // A manually edited configuration or moved reference cannot decrypt at a different authority or plan.
            return Utf8.GetBytes(SecretsFormat + "/" + connection.Id.ToString("D") + "/" +
                connection.SecretVersion.ToString("D") + "/" + scope.CredentialScope());
        }

        private static Dictionary<string, object> Document(string format, string section, object value) =>
            new Dictionary<string, object> { ["format"] = format, ["version"] = 1, [section] = value };

        private static object Read(AtomicFileDocument file, string format, string label, out string identity)
        {
            PreferenceReadResult read = file.Read();
            identity = read.Identity;
            switch (read.Status)
            {
                case PreferenceReadStatus.Missing: return null;
                case PreferenceReadStatus.Loaded:
                    object document = Parse(Utf8.GetString(read.Contents));
                    if (Text(document, "format") != format) throw Protocol();
                    int version = Number(document, "version");
                    if (version > 1) throw Storage(label + "由较新版本写入，原件已保留。");
                    if (version != 1) throw Protocol();
                    return document;
                case PreferenceReadStatus.Busy:
                    throw Storage(label + "已被另一个进程打开。");
                case PreferenceReadStatus.TooLarge:
                    throw Storage(label + "超过大小上限，原件已保留。");
                default:
                    throw Storage(label + "无法读取，请检查权限或保留的恢复文件。");
            }
        }

        private static string Commit(AtomicFileDocument file, string identity, byte[] bytes, string label)
        {
            PreferenceWriteResult written = file.Write(identity, bytes);
            if (written.Status == PreferenceWriteStatus.Saved) return written.Identity;
            if (written.CommitUnconfirmed) throw Storage(label + "的保存结果未确认，已暂停写入并保留恢复文件。");
            if (written.Status == PreferenceWriteStatus.Conflict || written.Status == PreferenceWriteStatus.Busy)
                throw Storage(label + "发生写入冲突，已暂停写入。");
            throw Storage(label + "未能保存，请检查磁盘空间和目录权限。");
        }

        private static byte[] Bytes(object document, int maximum)
        {
            try
            {
                byte[] bytes = Utf8.GetBytes(Write(document));
                if (bytes.Length > maximum) throw Storage("配置或聊天历史超出保存容量。");
                return bytes;
            }
            catch (Exception exception) when (LlmHttpOptions.IsJsonError(exception))
            {
                throw Storage("配置或聊天历史超出保存容量或格式无效。");
            }
        }

        private static void Shape(object value, params string[] names)
        {
            Dictionary<string, object> map = RequireMap(value);
            if (map.Count != names.Length || names.Any(name => !map.ContainsKey(name))) throw Protocol();
        }

        private static object[] Records(object document, string name, int limit)
        {
            object section = Get(document, name);
            if (!(section is IList list) || list.Count > limit) throw Protocol();
            return Array(section);
        }

        private static Guid Id(string text, bool emptyAllowed = false)
        {
            if (!Guid.TryParseExact(text, "D", out Guid id) || (!emptyAllowed && id == Guid.Empty)) throw Protocol();
            return id;
        }

        private static bool IsDocumentError(Exception exception) =>
            exception is LlmException || exception is CryptographicException || exception is NotSupportedException ||
            exception is System.Security.SecurityException || LlmHttpOptions.IsJsonError(exception);
        private static LlmException ForDocument(Exception exception, string name) =>
            exception is LlmException safe && safe.Failure == LlmFailure.Storage ? safe :
                Storage(name + "的内容无效或含未知字段，原件已保留。");
        private static LlmException Storage(string message) => new LlmException(LlmFailure.Storage, message);

        private static async Task<T> RunAsync<T>(SemaphoreSlim queue, Func<T> work)
        {
            await queue.WaitAsync().ConfigureAwait(false);
            try { return await Task.Run(work).ConfigureAwait(false); }
            finally { queue.Release(); }
        }

        public void Dispose()
        {
            // The Host retires network and submissions before process shutdown. Locks stay with any
            // accepted background operation until it naturally finishes or the process exits.
            if (!connectionWork.Wait(0)) return;
            try { settingsFile.Dispose(); secretsFile.Dispose(); }
            finally { connectionWork.Release(); }
        }
    }
}
