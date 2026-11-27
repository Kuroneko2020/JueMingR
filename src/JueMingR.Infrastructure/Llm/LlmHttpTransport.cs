using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Platform.Llm;
using static JueMingR.Infrastructure.Llm.LlmJson;

namespace JueMingR.Infrastructure.Llm
{
    public sealed class LlmHttpTransport : ILlmTransport, ILlmConnectionValidator, IDisposable
    {
        private readonly HttpClient system = MakeClient(true);
        private readonly HttpClient direct = MakeClient(false);
        private readonly Func<LlmConnectionSnapshot, HttpMessageHandler> handlerFactory;
        private readonly SemaphoreSlim slots = new SemaphoreSlim(4);

        public LlmHttpTransport() : this(null) { }

        public LlmHttpTransport(Func<LlmConnectionSnapshot, HttpMessageHandler> handlerFactory)
        {
            this.handlerFactory = handlerFactory;
        }

        public void Validate(LlmConnectionSnapshot connection)
        {
            LlmConnectionSettings settings = connection.CopySettings();
            _ = new LlmHttpOptions(connection, settings);
        }

        public Task<LlmTurn> CompleteAsync(LlmConnectionSnapshot connection, LlmRequest request,
            Action<string> onText, CancellationToken cancellation) =>
            Task.Run(() => CompleteCoreAsync(connection, request, onText, cancellation), cancellation);

        private async Task<LlmTurn> CompleteCoreAsync(LlmConnectionSnapshot connection, LlmRequest request,
            Action<string> onText, CancellationToken cancellation)
        {
            if (connection.Preset.Policy == "reference_only" || connection.Endpoint == null)
                throw LlmHttpOptions.Configuration("此连接无法发起推理请求。");
            LlmConnectionSettings settings = connection.CopySettings();
            var options = new LlmHttpOptions(connection, settings);
            LlmProtocol protocol = LlmProtocol.For(settings.Protocol);
            string json;
            try
            {
                Dictionary<string, object> body = protocol.Encode(connection, settings, request);
                foreach (var parameter in options.Parameters) body.Add(parameter.Key, parameter.Value);
                json = Write(body);
            }
            catch (Exception exception) when (LlmHttpOptions.IsJsonError(exception))
            {
                throw new LlmException(LlmFailure.Protocol, "本轮上下文或工具结构无法编码。请新开会话后重试。");
            }
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new LlmException(LlmFailure.Protocol, "本轮上下文超过连接可发送的大小，请新开会话。");

            var redactor = new LlmPublicText(options.SensitiveValues);
            void Visible(string text)
            {
                if (onText == null || cancellation.IsCancellationRequested) return;
                string safe = redactor.Next(text);
                if (safe.Length != 0) onText(safe);
            }

            LlmTurn result = await WithClientAsync(connection, settings, cancellation, (client, total) =>
                ExchangeAsync(client, connection, settings, options, connection.Endpoint, json, total, async reader =>
                {
                    if (settings.Stream) return await reader.ReadEventsAsync(protocol, Visible).ConfigureAwait(false);
                    return protocol.Decode(Parse(await reader.ReadBodyAsync().ConfigureAwait(false)));
                })).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (settings.Stream && onText != null)
            {
                string tail = redactor.Flush();
                if (tail.Length != 0) onText(tail);
            }
            return new LlmTurn(redactor.Final(result.Text), result.ToolCalls, result.WireJson);
        }

        public Task<IReadOnlyList<LlmModelInfo>> DiscoverAsync(LlmConnectionSnapshot connection, CancellationToken cancellation) =>
            Task.Run(() => DiscoverCoreAsync(connection, cancellation), cancellation);

        private async Task<IReadOnlyList<LlmModelInfo>> DiscoverCoreAsync(LlmConnectionSnapshot connection, CancellationToken cancellation)
        {
            Uri url = connection.DiscoveryEndpoint;
            if (url == null || connection.Preset.Policy == "reference_only")
                throw new LlmException(LlmFailure.DiscoveryUnavailable, "此方案未提供当前可用的模型列表，请手工填写模型 ID。");
            if (connection.Endpoint == null || url.Scheme != connection.Endpoint.Scheme || url.IdnHost != connection.Endpoint.IdnHost ||
                url.Port != connection.Endpoint.Port)
                throw new LlmException(LlmFailure.DiscoveryUnavailable, "模型列表地址与此连接来源不同，请手工填写模型 ID。");
            LlmConnectionSettings settings = connection.CopySettings();
            var options = new LlmHttpOptions(connection, settings);
            var redactor = new LlmPublicText(options.SensitiveValues);
            return await WithClientAsync<IReadOnlyList<LlmModelInfo>>(connection, settings, cancellation, async (client, total) =>
            {
                var models = new Dictionary<string, LlmModelInfo>(StringComparer.Ordinal);
                string format = connection.Preset.DiscoveryFormat ?? "openai_models";
                string cursor = null;
                var visited = new HashSet<string>(StringComparer.Ordinal);
                const int pageSize = 100;
                for (int page = 1; page <= 12; page++)
                {
                    Uri endpoint = url;
                    if (format == "bailian_native_models")
                        endpoint = AppendQuery(url, "page_no=" + page + "&page_size=" + pageSize);
                    else if (format == "anthropic_models" && cursor != null)
                        endpoint = AppendQuery(url, "after_id=" + Uri.EscapeDataString(cursor));

                    object response = await ExchangeAsync(client, connection, settings, options, endpoint, null, total,
                        async reader => Parse(await reader.ReadBodyAsync().ConfigureAwait(false))).ConfigureAwait(false);
                    ProtocolError(response);
                    object output = Get(response, "output");
                    object records = format == "bailian_native_models" ? Get(output, "models") : Get(response, "data");
                    if (!(records is System.Collections.IList)) throw Protocol();
                    foreach (object model in Array(records))
                    {
                        string id = Text(model, "id") ?? Text(model, "model_id") ?? Text(model, "model_name") ?? Text(model, "name");
                        if (string.IsNullOrWhiteSpace(id) || id.Length > 200 || id.Any(char.IsControl) || redactor.Final(id) != id) continue;
                        if (!models.ContainsKey(id)) models.Add(id, new LlmModelInfo(id, Capabilities(model)));
                        if (models.Count >= 1200) break;
                    }

                    if (models.Count >= 1200) break;
                    if (format == "bailian_native_models" && page * pageSize < Number(output, "total")) continue;
                    if (format == "anthropic_models" && Flag(response, "has_more"))
                    {
                        cursor = Text(response, "last_id");
                        if (string.IsNullOrEmpty(cursor) || cursor.Length > 300 || !visited.Add(cursor)) throw Protocol();
                        continue;
                    }
                    break;
                }
                return models.Values.OrderBy(model => model.Id, StringComparer.OrdinalIgnoreCase).ToArray();
            }).ConfigureAwait(false);
        }

        private static void ProtocolError(object response)
        {
            if (Get(response, "error") != null)
                throw LlmProviderFailure.Classify(response) ??
                    new LlmException(LlmFailure.Service, "服务未返回有效数据，请检查当前连接。");
        }

        private static string Capabilities(object model)
        {
            string[] parameters = Strings(Get(model, "supported_parameters"));
            string[] outputs = Strings(Get(Get(model, "architecture"), "output_modalities"));
            if (parameters.Contains("tools")) return outputs.Contains("text") ? "文字 · 工具" : "工具";
            if (outputs.Contains("text")) return "文字";
            return "能力未知";
        }

        private static Uri AppendQuery(Uri url, string query)
        {
            var builder = new UriBuilder(url) { Query = url.Query.Length == 0 ? query : url.Query.Substring(1) + "&" + query };
            return builder.Uri;
        }

        private async Task<T> WithClientAsync<T>(LlmConnectionSnapshot connection, LlmConnectionSettings settings,
            CancellationToken cancellation, Func<HttpClient, CancellationToken, Task<T>> handler)
        {
            using (var total = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                total.CancelAfter(TimeSpan.FromSeconds(settings.RunTimeoutSeconds));
                bool acquired = false;
                HttpClient owned = null;
                try
                {
                    await slots.WaitAsync(total.Token).ConfigureAwait(false);
                    acquired = true;
                    HttpClient client = settings.ProxyMode == "direct" ? direct : system;
                    if (handlerFactory != null) client = owned = new HttpClient(handlerFactory(connection), true) { Timeout = Timeout.InfiniteTimeSpan };
                    else if (settings.ProxyMode == "custom")
                    {
                        var proxy = new WebProxy(settings.ProxyUrl);
                        string credentials = connection.Secrets.ProxyCredentials;
                        int separator = credentials.IndexOf(':');
                        if (separator >= 1) proxy.Credentials = new NetworkCredential(credentials.Substring(0, separator), credentials.Substring(separator + 1));
                        client = owned = MakeClient(true, proxy);
                    }
                    T result = await handler(client, total.Token).ConfigureAwait(false);
                    total.Token.ThrowIfCancellationRequested();
                    return result;
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    throw new LlmException(LlmFailure.Timeout, "本次模型请求超时，未提交新的工具动作。");
                }
                catch (Exception exception) when (LlmHttpOptions.IsJsonError(exception))
                {
                    throw Protocol();
                }
                finally
                {
                    owned?.Dispose();
                    if (acquired) slots.Release();
                }
            }
        }

        private static async Task<T> ExchangeAsync<T>(HttpClient client, LlmConnectionSnapshot connection,
            LlmConnectionSettings settings, LlmHttpOptions options, Uri uri, string body,
            CancellationToken total, Func<LlmResponseReader, Task<T>> consume)
        {
            bool discovery = body == null;
            for (int attempt = 0; ; attempt++)
            {
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(total))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(settings.FirstByteSeconds));
                    bool gotHeaders = false;
                    try
                    {
                        using (var request = new HttpRequestMessage(discovery ? HttpMethod.Get : HttpMethod.Post, uri))
                        {
                            options.Apply(request, connection, settings, discovery);
                            if (!discovery) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
                                !discovery && settings.Stream ? "text/event-stream" : "application/json"));
                            using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                                .ConfigureAwait(false))
                            {
                                gotHeaders = true;
                                int status = (int)response.StatusCode;
                                if (discovery && attempt < settings.RetryCount && (status == 502 || status == 503 || status == 504))
                                {
                                    await Backoff(attempt, total).ConfigureAwait(false);
                                    continue;
                                }
                                if (!response.IsSuccessStatusCode)
                                {
                                    if (status >= 400 && status < 500)
                                    {
                                        LlmException classified = await ClassifyHttpErrorAsync(response, status, deadline, settings.IdleSeconds).ConfigureAwait(false);
                                        if (classified != null) throw classified;
                                    }
                                    throw Status(status, discovery, connection.Preset.Provider);
                                }
                                if (response.Content == null) throw Incomplete();
                                string contentType = response.Content.Headers.ContentType?.MediaType;
                                Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                                using (var reader = new LlmResponseReader(stream, deadline.Token, deadline, settings.IdleSeconds))
                                {
                                    if (!discovery && settings.Stream && string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
                                    {
                                        object error = Parse(await reader.ReadBodyAsync().ConfigureAwait(false));
                                        ProtocolError(error);
                                        throw new LlmException(LlmFailure.Protocol, "服务没有按配置返回流式事件。");
                                    }
                                    return await consume(reader).ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch (Exception exception) when (exception is HttpRequestException || exception is IOException ||
                        exception is ObjectDisposedException || exception is OperationCanceledException)
                    {
                        total.ThrowIfCancellationRequested();
                        if (discovery && !gotHeaders && attempt < settings.RetryCount)
                        {
                            await Backoff(attempt, total).ConfigureAwait(false);
                            continue;
                        }
                        if (deadline.IsCancellationRequested)
                            throw new LlmException(LlmFailure.Timeout, gotHeaders
                                ? "等待模型回复超时，本次未提交新工具动作。" : "连接服务超时，请检查地址、网络或代理。");
                        throw new LlmException(LlmFailure.Network, "与模型服务的连接中断，请检查地址、网络或代理。");
                    }
                }
            }
        }

        private static async Task<LlmException> ClassifyHttpErrorAsync(HttpResponseMessage response, int status,
            CancellationTokenSource deadline, int idleSeconds)
        {
            if (response.Content == null) return null;
            try
            {
                Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using (var reader = new LlmResponseReader(stream, deadline.Token, deadline, idleSeconds))
                {
                    string body = await reader.ReadBodyAsync(16384).ConfigureAwait(false);
                    return body == null ? null : LlmProviderFailure.Classify(Parse(body), status);
                }
            }
            catch (Exception exception) when (exception is LlmException || LlmHttpOptions.IsJsonError(exception))
            {
                // Malformed or unknown error envelopes retain the accurate HTTP status fallback.
                return null;
            }
        }

        // Only safe enumeration is retried. A POST may have incurred charges before a local transport failure.
        private static Task Backoff(int attempt, CancellationToken token) => Task.Delay(300 * (attempt + 1), token);

        private static LlmException Status(int status, bool discovery, string provider)
        {
            if (status >= 300 && status < 400)
                return new LlmException(LlmFailure.Configuration, "服务地址发生重定向。请在当前连接确认正确地址后重试。", status);
            switch (status)
            {
                case 401: case 403:
                    return new LlmException(LlmFailure.Authentication, "服务拒绝当前凭据或权限，请核对站点、计费方案和工作空间。", status);
                case 402:
                    return new LlmException(LlmFailure.Balance, "服务要求检查当前账户的付款或余额状态。", status);
                case 429:
                    return provider == "deepseek"
                        ? new LlmException(LlmFailure.RateLimited, "当前连接的请求频率或并发触发限流，请稍后重试。", status)
                        : new LlmException(LlmFailure.RateLimited, "服务返回 HTTP 429，请查看当前方案的频率限制和额度。", status);
                case 404: case 405:
                    return discovery
                        ? new LlmException(LlmFailure.DiscoveryUnavailable, "服务未提供当前模型列表入口，可手工填写模型 ID。", status)
                        : new LlmException(LlmFailure.ModelUnavailable, "该模型或所选推理入口不可用，请检查模型 ID、协议和地址。", status);
                case 400: case 422:
                    return new LlmException(LlmFailure.Configuration, "服务拒绝了请求参数。请检查所选模型、协议和高级选项。", status);
                default:
                    return new LlmException(LlmFailure.Service, "模型服务暂时未能处理请求。", status);
            }
        }

        private static HttpClient MakeClient(bool useProxy, IWebProxy proxy = null)
        {
            // Credentials and headers are set on each request. Never forward them through a redirect.
            var handler = new HttpClientHandler
            {
                UseProxy = useProxy, Proxy = proxy, UseCookies = false,
                AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            return new HttpClient(handler, true) { Timeout = Timeout.InfiniteTimeSpan };
        }

        public void Dispose() { system.Dispose(); direct.Dispose(); }
    }
}
