using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SniffingProxy.Core
{
    public class HttpData
    {
        // public string Method { get; set; }
        // public string Path { get; set; }
        // public string Version { get; set; }
        // public string Host { get; set; }
        // public int Port { get; set; }
        private static byte[] _crLfBuffer = new byte[] { 13, 10, 13, 10 };
        private static byte[] _crBuffer = new byte[] { 13, 10 };
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public IEnumerable<KeyValuePair<string, string>> HeadersList { get; set; }
        public int ContentLength { get; set; }
        // public string Body { get; set; }

        public static HttpData ParseRawHttp(string requestText)
        {
            var headersEndIndex = requestText.IndexOf("\r\n\r\n");
            // var testHeaders = requestText.Substring(0, endHeaderIndex);

            var expectedBodyLength = requestText.Length - (headersEndIndex + 4);
            // var actualBody = requestText.Substring(headersEndIndex + 4);
            // var actualBodyLength = actualBody.Length;


            var prefixToEnd = requestText.Split("\r\n", 2);
            var prefixLine = prefixToEnd[0];
            var headersAndBody = prefixToEnd[1].Split("\r\n\r\n");
            // var headersText = prefixToEnd[1].Substring(0, headersEndIndex);
            var headersText = headersAndBody[0];
            var headerLines = headersText.Split("\r\n");
            var prefixData = prefixLine.Split(" ");
            var parsedheaders = headerLines.Skip(1).Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Split(':', 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray());
            var headersKvp = parsedheaders.Select(h => KeyValuePair.Create(h[0], h[1]));
            // var headersDictionary = parsedheaders.ToDictionary(kvp => kvp.First(), kvp => kvp.Last(), StringComparer.InvariantCultureIgnoreCase);
            // var hostAndPort = headersDictionary["host"].Split(":");
            // var request = new Request
            // {
            //     Method = prefixData[0],
            //     Path = prefixData[1],
            //     Version = prefixData[2],
            //     Host = hostAndPort[0],
            //     Port = hostAndPort.Length > 1 ? int.Parse(hostAndPort[1]) : -1,
            //     Headers = headersDictionary,
            //     Body = headersAndBody[1]
            // };
            // var jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(request);
            return new HttpData
            {
                // Headers = headersDictionary,
                HeadersList = headersKvp,
                ContentLength = expectedBodyLength
            };
        }

        public static bool TryGetContentLength(HttpData httpData, out int contentLength)
        {
            return int.TryParse(httpData.HeadersList.SingleOrDefault(h => h.Key == "content-length").Value, out contentLength);
        }

        public static async Task<byte[]> ReceiveUpToHeaders(Stream sourceStream, int bufferSize, CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            var allBytesEnumerable = Enumerable.Empty<byte>();
            int endOfHeadersIndex;
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken);
                allBytesEnumerable = allBytesEnumerable.Concat(buffer.AsSpan(0, bytesRead).ToArray());

                // check for \r\n\r\n
                if (bytesRead < 4) continue;
                endOfHeadersIndex = buffer.AsSpan(0, bytesRead).IndexOf(_crLfBuffer);
                if (endOfHeadersIndex >= 0) break;
            }

            var allBytesBuffer = allBytesEnumerable.ToArray();
            return allBytesBuffer;
        }


        public static async Task<byte[]> ParseByContentLength(Stream sourceStream, int bufferSize, int remainingBytes)
        {
            var buffer = new byte[bufferSize];
            var allBytes = Enumerable.Empty<byte>();
            var allBytesRead = 0;
            while (allBytesRead < remainingBytes)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer);
                allBytesRead += bytesRead;
                allBytes = allBytes.Concat(buffer.AsSpan(0, bytesRead).ToArray());
            }

            return allBytes.ToArray();
        }

        public static async Task<byte[]> ParseByTransferEncoding(Stream sourceStream, int bufferSize, Memory<byte> startOfBody)
        {
            var text = Encoding.UTF8.GetString(startOfBody.Span);
            var chunkSize = startOfBody.Span.IndexOf(_crBuffer);
            var data = startOfBody.Span.Slice(0, chunkSize).ToArray();
            throw new NotImplementedException();
            // var buffer = new byte[bufferSize];
            // var allBytes = Enumerable.Empty<byte>();
            // var allBytesRead = 0;
            // while (allBytesRead < remainingBytes)
            // {
            //     var bytesRead = await sourceStream.ReadAsync(buffer);
            //     allBytesRead += bytesRead;
            //     allBytes = allBytes.Concat(buffer.AsSpan(0, bytesRead).ToArray());
            // }

            // return allBytes.ToArray();
        }
    }
}
