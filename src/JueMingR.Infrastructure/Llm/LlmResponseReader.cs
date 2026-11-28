using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JueMingR.Platform.Llm;

namespace JueMingR.Infrastructure.Llm
{
    internal sealed class LlmResponseReader : IDisposable
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly Stream stream;
        private readonly byte[] buffer = new byte[8192];
        private readonly CancellationToken cancellation;
        private readonly CancellationTokenRegistration interruption;
        private readonly CancellationTokenSource deadline;
        private readonly int idleSeconds;
        private int offset;
        private int length;
        private int received;
        private bool firstRead = true;

        public LlmResponseReader(Stream stream, CancellationToken cancellation, CancellationTokenSource deadline, int idleSeconds)
        {
            this.stream = stream;
            this.cancellation = cancellation;
            this.deadline = deadline;
            this.idleSeconds = idleSeconds;
            // On .NET Framework the HTTP response stream may not honor a pending ReadAsync token by itself.
            interruption = cancellation.Register(() =>
            {
                try { stream.Dispose(); }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
                catch (HttpRequestException) { }
            });
        }

        public async Task<string> ReadBodyAsync(int maximumBytes = LlmJson.MaximumBytes)
        {
            using (var bytes = new MemoryStream())
            {
                while (await ReadNextAsync().ConfigureAwait(false))
                {
                    if (received > maximumBytes) return null;
                    bytes.Write(buffer, 0, length);
                }
                return Utf8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length);
            }
        }

        public async Task<LlmTurn> ReadEventsAsync(LlmProtocol protocol, Action<string> delta)
        {
            string eventName = "";
            var data = new StringBuilder();
            while (true)
            {
                string line = await ReadLineAsync().ConfigureAwait(false);
                if (line == null) throw LlmJson.Incomplete();
                if (line.Length == 0)
                {
                    if (data.Length != 0 && protocol.Accept(eventName, data.ToString(), delta, out LlmTurn turn))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        return turn;
                    }
                    data.Clear();
                    eventName = "";
                }
                else if (line.StartsWith("event:", StringComparison.Ordinal)) eventName = Field(line, 6);
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (data.Length != 0) data.Append('\n');
                    data.Append(Field(line, 5));
                }
            }
        }

        private async Task<string> ReadLineAsync()
        {
            using (var bytes = new MemoryStream())
            {
                while (true)
                {
                    if (offset == length && !await ReadNextAsync().ConfigureAwait(false))
                        return null;
                    int newline = System.Array.IndexOf(buffer, (byte)'\n', offset, length - offset);
                    int end = newline < 0 ? length : newline;
                    bytes.Write(buffer, offset, end - offset);
                    offset = newline < 0 ? length : newline + 1;
                    if (newline < 0) continue;
                    int size = (int)bytes.Length;
                    byte[] line = bytes.GetBuffer();
                    if (size != 0 && line[size - 1] == '\r') size--;
                    return Utf8.GetString(line, 0, size).TrimStart('\uFEFF');
                }
            }
        }

        private async Task<bool> ReadNextAsync()
        {
            cancellation.ThrowIfCancellationRequested();
            if (!firstRead) deadline.CancelAfter(TimeSpan.FromSeconds(idleSeconds));
            firstRead = false;
            length = await stream.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            offset = 0;
            received += length;
            if (received > LlmJson.MaximumBytes)
                throw new LlmException(LlmFailure.Protocol, "回复超过本机可处理的大小，未提交新的工具动作。");
            return length != 0;
        }

        private static string Field(string line, int offset)
        {
            if (line.Length > offset && line[offset] == ' ') offset++;
            return line.Substring(offset);
        }

        public void Dispose() { interruption.Dispose(); stream.Dispose(); }
    }
}
