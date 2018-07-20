using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SniffingProxy.Core
{
    public class CustomHttpClient
    {
        public async Task<byte[]> HandleSend(string requestText)
        {
            throw new NotSupportedException();
        }

        // static async Task TcpGetRequest(CancellationToken cancellationToken)
        // {
        //     var request = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: httpvshttps.com\r\n\r\n");
        //     var tempClient = new TcpClient("httpvshttps.com", 80);
        //     var tempStream = tempClient.GetStream();
        //     await tempStream.WriteAsync(request, cancellationToken);

        //     var buffer = new byte[5000];
        //     var bytesRead = await tempStream.ReadAsync(buffer, 0, buffer.Length);
        //     var slice = new Memory<byte>(buffer, 0, bytesRead);
        //     var receivedText = Encoding.UTF8.GetString(slice.Span);
        // }

        static async Task<string> ReceiveRequestText(NetworkStream clientStream, int receiveBufferSize, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(receiveBufferSize);
            var clientMemory = buffer.AsMemory();

            var bytesRead = await clientStream.ReadAsync(clientMemory, cancellationToken);
            var requestText = Encoding.UTF8.GetString(clientMemory.Slice(0, bytesRead).Span);
            return requestText;
        }

        static async Task HandleHttpConnection(Stream stream, int receiveBufferSize, Request request, CancellationToken cancellationToken)
        {
            var response = await HandleHttpRequest("http", request, cancellationToken);
            await stream.WriteAsync(response);
            while (true)
            {
                var requestText = await ReceiveHttpRequestText(stream, receiveBufferSize, cancellationToken);
                if (string.IsNullOrEmpty(requestText))
                {
                    throw new Exception("request was empty");
                }
                request = Request.Parse(requestText);
                response = await HandleHttpRequest("http", request, cancellationToken);
                await stream.WriteAsync(response);
            }
        }

        static async Task<string> ReceiveHttpRequestText(Stream sourceStream, int bufferSize, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            var bufferIndex = 0;
            var totalBytesRead = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await sourceStream.ReadAsync(buffer, bufferIndex, buffer.Length - bufferIndex, cancellationToken);
                bufferIndex = bytesRead;
                totalBytesRead += bytesRead;

                // check for \r\n\r\n
                if (bytesRead < 4) continue;
                var memory = new Memory<byte>(buffer, totalBytesRead - 4, 4);
                if (memory.Span[memory.Length - 1] == 10 && memory.Span[memory.Length - 2] == 13 &&
                    memory.Span[memory.Length - 3] == 10 && memory.Span[memory.Length - 4] == 13) break;
            }

            var allBytes = new Memory<byte>(buffer, 0, totalBytesRead);
            var request = Encoding.UTF8.GetString(allBytes.Span);
            ArrayPool<byte>.Shared.Return(buffer);
            return request;
        }

        static async Task<byte[]> HandleHttpRequest(string protocol, Request request, CancellationToken cancellationToken)
        {
            var url = protocol == "http" ? $"{request.Path}" : $"{protocol}://{request.Host}{request.Path}";
            var customRequest = new HttpRequestMessage(new HttpMethod(request.Method), url);
            foreach (var header in request.Headers)
            {
                if (header.Key.ToLowerInvariant().Equals("content-length"))
                {
                    continue;
                }
                // if (header.Key.ToLowerInvariant().Equals("accept-encoding"))
                // {
                //     continue;
                // }
                customRequest.Headers.Add(header.Key, header.Value);
            }
            // var res = await _httpClient.SendAsync(customRequest);

            var handler = new HttpClientHandler { AllowAutoRedirect = false };
            // var res2 = await new HttpClient(handler).GetAsync(url);
            var res = await new HttpClient(handler).SendAsync(customRequest);

            var responseHeaders = res.Headers.ToString();
            var tempContent = await res.Content.ReadAsStringAsync();
            var contentBuffer = await res.Content.ReadAsByteArrayAsync();
            // try
            // {
            //     var uncompressedStream = new MemoryStream();
            //     var streamBuffer = await res.Content.ReadAsStreamAsync();
            //     var gzipStream = new GZipStream(streamBuffer, CompressionMode.Decompress);
            //     gzipStream.CopyTo(uncompressedStream);
            //     var data = Encoding.UTF8.GetString(uncompressedStream.ToArray());
            // }
            // catch (Exception ex)
            // {

            // }
            // var headersBuffer = Encoding.UTF8.GetBytes($"HTTP/1.1 {(int)res.StatusCode}\r\n{responseHeaders}Content-Length: {contentBuffer.Length}\r\n\r\n");
            var headersBuffer = Encoding.UTF8.GetBytes($"HTTP/1.1 {(int)res.StatusCode}\r\n{responseHeaders}\r\n\r\n");
            var allBuffer = headersBuffer.Concat(contentBuffer).ToArray();
            var rawTemp = Encoding.UTF8.GetString(allBuffer);
            return allBuffer;
        }
    }
}
