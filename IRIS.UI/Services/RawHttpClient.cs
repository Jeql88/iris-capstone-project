using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IRIS.UI.Services
{
    /// <summary>
    /// Lightweight HTTP GET client using raw TCP sockets.
    /// Bypasses .NET's HttpClient/HttpWebRequest which can be intercepted
    /// by endpoint security software (e.g. Sophos Web Protection).
    /// Properly parses HTTP responses (Content-Length and chunked encoding)
    /// so it returns as soon as the body is fully received — no dependency
    /// on TCP connection close timing.
    /// </summary>
    internal static class RawHttpClient
    {
        public static async Task<byte[]?> GetBytesAsync(
            string host, int port, string path,
            (string Key, string Value)[]? headers = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(effectiveTimeout);
            var ct = cts.Token;

            using var tcp = new TcpClient { NoDelay = true };
            // TCP keep-alive so a half-dead peer (agent crashed, switch dropped flow state)
            // is detected during the response read instead of blocking the full request timeout.
            tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            await tcp.ConnectAsync(host, port, ct);

            var stream = tcp.GetStream();

            // Send HTTP/1.1 request with Connection: close so the server releases
            // the connection immediately after the response. Without this, HTTP.sys
            // holds connections open (keep-alive) and they accumulate on the agent.
            var sb = new StringBuilder();
            sb.Append($"GET {path} HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n");
            if (headers != null)
            {
                foreach (var (key, value) in headers)
                {
                    sb.Append($"{key}: {value}\r\n");
                }
            }
            sb.Append("\r\n");

            var requestBytes = Encoding.ASCII.GetBytes(sb.ToString());
            await stream.WriteAsync(requestBytes, ct);

            // --- Read response headers ---
            // Accumulate bytes until we see \r\n\r\n.
            var headerBuf = new MemoryStream();
            var readBuf = new byte[4096];
            int headerEndIndex = -1;

            while (headerEndIndex < 0)
            {
                int n = await stream.ReadAsync(readBuf, ct);
                if (n == 0) return null; // connection closed before headers complete
                headerBuf.Write(readBuf, 0, n);

                var accumulated = headerBuf.GetBuffer();
                int len = (int)headerBuf.Length;
                // Search from where the previous read left off (minus 3 for overlap).
                int searchStart = Math.Max(0, len - n - 3);
                for (int i = searchStart; i < len - 3; i++)
                {
                    if (accumulated[i] == '\r' && accumulated[i + 1] == '\n'
                        && accumulated[i + 2] == '\r' && accumulated[i + 3] == '\n')
                    {
                        headerEndIndex = i;
                        break;
                    }
                }
            }

            var allHeader = headerBuf.GetBuffer();
            int totalHeaderBytes = (int)headerBuf.Length;
            var headerText = Encoding.ASCII.GetString(allHeader, 0, headerEndIndex);

            // Check for 200 status.
            if (!headerText.StartsWith("HTTP/") || !headerText.Contains(" 200 "))
                return null;

            int bodyOffset = headerEndIndex + 4; // skip \r\n\r\n
            int extraBytes = totalHeaderBytes - bodyOffset; // bytes already read past headers

            // --- Determine body transfer mode ---
            bool isChunked = headerText.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase);
            int contentLength = -1;
            if (!isChunked)
            {
                foreach (var line in headerText.Split("\r\n"))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(line.AsSpan("Content-Length:".Length), out var cl))
                            contentLength = cl;
                        break;
                    }
                }
            }

            // Build a BufferedReader over the network stream, seeded with leftover bytes.
            var reader = new BufferedStreamReader(stream, allHeader, bodyOffset, extraBytes);

            if (contentLength >= 0)
            {
                return await ReadContentLengthBodyAsync(reader, contentLength, ct);
            }
            if (isChunked)
            {
                return await ReadChunkedBodyAsync(reader, ct);
            }

            // Fallback: read until connection closes (shouldn't happen with HTTP.sys).
            return await ReadUntilCloseAsync(reader, ct);
        }

        private static async Task<byte[]?> ReadContentLengthBodyAsync(
            BufferedStreamReader reader, int contentLength, CancellationToken ct)
        {
            var body = new byte[contentLength];
            int filled = 0;
            while (filled < contentLength)
            {
                int n = await reader.ReadAsync(body.AsMemory(filled, contentLength - filled), ct);
                if (n == 0) break;
                filled += n;
            }
            return filled > 0 ? body.AsMemory(0, filled).ToArray() : null;
        }

        private static async Task<byte[]> ReadChunkedBodyAsync(
            BufferedStreamReader reader, CancellationToken ct)
        {
            var body = new MemoryStream();

            while (true)
            {
                // Read chunk size line.
                var sizeLine = await reader.ReadLineAsync(ct);
                if (sizeLine == null) break;

                // Parse hex chunk size (ignore extensions after semicolon).
                var sizeStr = sizeLine.Trim();
                int semi = sizeStr.IndexOf(';');
                if (semi >= 0) sizeStr = sizeStr[..semi];

                if (!int.TryParse(sizeStr, System.Globalization.NumberStyles.HexNumber, null, out int chunkSize))
                    break;

                if (chunkSize == 0)
                    break; // Last chunk — done.

                // Read chunk data.
                var chunk = new byte[chunkSize];
                int filled = 0;
                while (filled < chunkSize)
                {
                    int n = await reader.ReadAsync(chunk.AsMemory(filled, chunkSize - filled), ct);
                    if (n == 0) break;
                    filled += n;
                }
                body.Write(chunk, 0, filled);

                // Consume trailing \r\n after chunk data.
                await reader.ReadLineAsync(ct);
            }

            return body.ToArray();
        }

        private static async Task<byte[]?> ReadUntilCloseAsync(
            BufferedStreamReader reader, CancellationToken ct)
        {
            var ms = new MemoryStream();
            var buf = new byte[8192];
            int n;
            while ((n = await reader.ReadAsync(buf, ct)) > 0)
            {
                ms.Write(buf, 0, n);
            }
            return ms.Length > 0 ? ms.ToArray() : null;
        }

        /// <summary>
        /// Wrapper that reads from the network in bulk (8KB chunks) and serves
        /// both bulk and line-oriented reads from an internal buffer. This avoids
        /// the massive overhead of one async call per byte.
        /// </summary>
        private sealed class BufferedStreamReader
        {
            private readonly NetworkStream _stream;
            private readonly byte[] _buf = new byte[8192];
            private int _pos;
            private int _len;

            public BufferedStreamReader(NetworkStream stream, byte[] seed, int offset, int count)
            {
                _stream = stream;
                if (count > 0)
                {
                    if (count > _buf.Length)
                        _buf = new byte[count];
                    Buffer.BlockCopy(seed, offset, _buf, 0, count);
                }
                _pos = 0;
                _len = count;
            }

            private async ValueTask EnsureBufferAsync(CancellationToken ct)
            {
                if (_pos >= _len)
                {
                    _len = await _stream.ReadAsync(_buf.AsMemory(), ct);
                    _pos = 0;
                }
            }

            public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken ct)
            {
                await EnsureBufferAsync(ct);
                if (_len == 0) return 0; // stream closed

                int avail = _len - _pos;
                int toCopy = Math.Min(avail, destination.Length);
                _buf.AsMemory(_pos, toCopy).CopyTo(destination);
                _pos += toCopy;
                return toCopy;
            }

            public async Task<string?> ReadLineAsync(CancellationToken ct)
            {
                // Fast path: scan for \n within the current buffer.
                // Only allocates a MemoryStream if the line spans multiple buffer fills.
                MemoryStream? overflow = null;

                while (true)
                {
                    await EnsureBufferAsync(ct);
                    if (_len == 0)
                    {
                        // Stream ended — return whatever we accumulated.
                        return overflow is { Length: > 0 } ? FinishLine(overflow) : null;
                    }

                    // Scan buffer for \n starting at _pos.
                    for (int i = _pos; i < _len; i++)
                    {
                        if (_buf[i] == '\n')
                        {
                            // Found end of line. Build result from overflow + this segment.
                            int segLen = i - _pos; // excludes the \n itself
                            if (overflow == null)
                            {
                                // Entire line is in the current buffer — fast path, no copies.
                                var span = _buf.AsSpan(_pos, segLen);
                                _pos = i + 1; // consume past \n
                                if (span.Length > 0 && span[^1] == (byte)'\r')
                                    span = span[..^1];
                                return Encoding.ASCII.GetString(span);
                            }

                            overflow.Write(_buf, _pos, segLen);
                            _pos = i + 1;
                            return FinishLine(overflow);
                        }
                    }

                    // No \n found — append entire remaining buffer to overflow and refill.
                    overflow ??= new MemoryStream();
                    overflow.Write(_buf, _pos, _len - _pos);
                    _pos = _len; // buffer exhausted, next EnsureBufferAsync will refill
                }
            }

            private static string FinishLine(MemoryStream ms)
            {
                var span = ms.ToArray().AsSpan();
                if (span.Length > 0 && span[^1] == (byte)'\r')
                    span = span[..^1];
                return Encoding.ASCII.GetString(span);
            }
        }
    }
}
