using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO.Compression;
using SniffingProxy.Core;

namespace SniffingProxy
{
    class Program
    {
        private const string rootCertSerialNumber = "00CC78A90D47D8159A";
        // private const string rootCertSerialNumber = "00ed57f3562fd3d663";

        private static HttpClient _httpClient;
        private static TcpListener _tcpServer;
        private static CertificateService _certificateService = new CertificateService();
        private static string _proxyUrl = Environment.GetEnvironmentVariable("http_proxy");


        static async Task Main(string[] args)
        {
            try
            {
                // ParseRequest("CONNECT raw.githubusercontent.com:443 HTTP/1.1\r\nHost: raw.githubusercontent.com:443\r\n\r\n");
                const string localIp = "127.0.0.1";
                const int localPort = 5000;

                var httpClientHandler = new HttpClientHandler
                {
                    // Proxy = new WebProxy(proxyUrl),
                    // AllowAutoRedirect = false
                };
                _httpClient = new HttpClient(httpClientHandler, disposeHandler: true);
                // var customHttpsClient = new CustomHttpsClient();
                // customHttpsClient.HandleConnect();

                // var requestJson = "{\"Method\":\"GET\",\"Path\":\"/\",\"Version\":\"HTTP/1.1\",\"Host\":\"gmail.com\",\"Port\":-1,\"Headers\":{\"User-Agent\":\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.140 Safari/537.36 Edge/17.17134\",\"Accept-Language\":\"en-US\",\"Accept\":\"text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8\",\"Upgrade-Insecure-Requests\":\"1\",\"Accept-Encoding\":\"gzip, deflate, br\",\"Host\":\"gmail.com\",\"Connection\":\"Keep-Alive\",\"Cache-Control\":\"no-cache\"},\"Body\":\"\"}";
                // var request = JsonConvert.DeserializeObject<Request>(requestJson);
                // var x = await HandleRequest("https", request, CancellationToken.None);

                // var handler = new HttpClientHandler { AllowAutoRedirect = false };
                // var res = await new HttpClient(handler).GetAsync("https://mail.google.com/mail/");

                _tcpServer = new TcpListener(IPAddress.Parse(localIp), localPort);
                _tcpServer.Start();

                while (true)
                {
                    var clientConnection = await _tcpServer.AcceptTcpClientAsync();
                    AcceptClient(clientConnection);

                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                _ = ex;
            }
        }

        static async Task AcceptClient(TcpClient client)
        {
            try
            {
                var clientStream = client.GetStream();
                var cancellationTokenSource = new CancellationTokenSource();
                var request = await ReceiveRequest(clientStream, client.ReceiveBufferSize, cancellationTokenSource.Token);
                Console.WriteLine("Client connected");
                // var fakeCert = _certificateService.CreateFakeCertificate(request.Host, rootCertSerialNumber);
                // await HandleHttpsRequest(clientStream, request, client.ReceiveBufferSize, fakeCert, cancellationTokenSource.Token);

                switch (request.Method)
                {
                    case "CONNECT":
                        await HandleConnectRequest(clientStream, client.ReceiveBufferSize, request, cancellationTokenSource.Token);
                        var fakeCert = _certificateService.CreateFakeCertificate(request.Host, rootCertSerialNumber);
                        await HandleHttpsRequest(clientStream, request, client.ReceiveBufferSize, fakeCert, cancellationTokenSource.Token);
                        break;
                    // case "GET":
                    default:
                        await HandleHttpConnection(clientStream, client.ReceiveBufferSize, request, cancellationTokenSource.Token);
                        break;
                        //  throw new ArgumentOutOfRangeException("Invalid request method");
                }
            }
            catch (Exception ex)
            {
                // Console.Error.WriteLine(ex);
                Console.WriteLine("Client disconnected");
            }
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

        // static async Task<Request> Connect(NetworkStream clientStream, int receiveBufferSize, CancellationToken cancellationToken)
        // {

        //     // var buffer = ArrayPool<byte>.Shared.Rent(receiveBufferSize);
        //     // var clientMemory = buffer.AsMemory();

        //     // var bytesRead = await clientStream.ReadAsync(clientMemory, cancellationToken);
        //     // var requestText = Encoding.UTF8.GetString(clientMemory.Slice(0, bytesRead).Span);
        //     // var request = ParseRequest(requestText);
        //     var request = await ReceiveRequest(clientStream, receiveBufferSize, cancellationToken);

        //     var responseMemory = "HTTP/1.1 200 Connection established\r\n\r\n".AsMemory();
        //     await WriteResponse(clientStream, responseMemory, cancellationToken);
        //     // ArrayPool<byte>.Shared.Return(buffer);
        //     return request;
        // }

        static async Task<string> ReceiveRequestText(NetworkStream clientStream, int receiveBufferSize, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(receiveBufferSize);
            var clientMemory = buffer.AsMemory();

            var bytesRead = await clientStream.ReadAsync(clientMemory, cancellationToken);
            var requestText = Encoding.UTF8.GetString(clientMemory.Slice(0, bytesRead).Span);
            return requestText;
        }

        static async Task<Request> ReceiveRequest(NetworkStream clientStream, int receiveBufferSize, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(receiveBufferSize);
            var clientMemory = buffer.AsMemory();

            var bytesRead = await clientStream.ReadAsync(clientMemory, cancellationToken);
            var requestText = Encoding.UTF8.GetString(clientMemory.Slice(0, bytesRead).Span);
            var request = Request.Parse(requestText);
            return request;
        }

        static async Task HandleHttpConnection(Stream stream, int receiveBufferSize, Request request, CancellationToken cancellationToken)
        {
            var response = await HandleRequest("http", request, cancellationToken);
            await stream.WriteAsync(response);
            while (true)
            {
                var requestText = await ReceiveHttpRequestText(stream, receiveBufferSize, cancellationToken);
                request = Request.Parse(requestText);
                response = await HandleRequest("http", request, cancellationToken);
                await stream.WriteAsync(response);
            }
        }

        static async Task<byte[]> HandleRequest(string protocol, Request request, CancellationToken cancellationToken)
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

        static async Task HandleConnectRequest(Stream clientStream, int receiveBufferSize, Request request, CancellationToken cancellationToken)
        {
            var responseMemory = "HTTP/1.1 200 Connection established\r\n\r\n".AsMemory();
            await WriteResponse(clientStream, responseMemory, cancellationToken);
        }

        static async Task HandleHttpsRequest(Stream clientStream, Request request, int receiveBufferSize, X509Certificate2 fakeCert, CancellationToken cancellationToken)
        {
            // authenticate as server
            var clientSslStream = new SslStream(clientStream, false);
            await clientSslStream.AuthenticateAsServerAsync(fakeCert, false, SslProtocols.Tls, true);

            // receive http request from client
            var requestBytes = await ReceiveHttpRequestBytes(clientSslStream, receiveBufferSize * 2, cancellationToken);
            var requestText = Encoding.UTF8.GetString(requestBytes);

            // initialize https client, connect via proxy if necessary
            var httpsClient = await CustomHttpsClient.CreateWithProxy(request.Host, request.Port, _proxyUrl);
            // send request to remote
            var rawResponse = await httpsClient.HandleSend(requestText);

            await clientSslStream.WriteAsync(rawResponse, cancellationToken);

            // var responseBytes = await ReceiveHttpRequestBytesSimple(remoteSslStream, receiveBufferSize * 2, cancellationToken);
            // var tempText2 = Encoding.UTF8.GetString(responseBytes);
            // await remoteSslStream.WriteAsync(responseBytes, 0, responseBytes.Length);

            // var remoteBuffer = new byte[receiveBufferSize];
            // var remoteBytesRead = -1;
            // var counter = 0;
            // while (remoteBytesRead != 0)
            // {
            //     remoteBytesRead = await remoteSslStream.ReadAsync(remoteBuffer, 0, remoteBuffer.Length);
            //     await clientSslStream.WriteAsync(remoteBuffer, 0, remoteBytesRead);
            //     if (counter == 48)
            //     {

            //     }
            //     counter++;
            // }
            // var x = 5;


            // var requestText = await ReceiveHttpRequestText(sslStream, receiveBufferSize, cancellationToken);
            // var request = ParseRequest(requestText);
            // var requestBytes = await ReceiveHttpRequestBytes(clientSslStream, receiveBufferSize, cancellationToken);
            // var tempText = Encoding.UTF8.GetString(requestBytes);

            // await remoteSslStream.WriteAsync(requestBytes, 0, requestBytes.Length);
            // await remoteSslStream.FlushAsync();

            // var serverResponse = await ReceiveHttpRequestBytes(remoteSslStream, receiveBufferSize, cancellationToken);
            // var tempText2 = Encoding.UTF8.GetString(serverResponse);


            // modify request here if you'd like, and then forward it to the remote
            // var res = await HandleRequest("https", request, cancellationToken);

            // await clientSslStream.WriteAsync(serverResponse, cancellationToken);
        }
        // static async Task<byte[]> ReceiveHttpRequestBytesSimple(Stream sourceStream, int bufferSize, CancellationToken cancellationToken)
        // {
        //     var allBytes
        //     var memory = new Memory<byte>(new byte[bufferSize]);
        //     while (true)
        //     {
        //         var bytesRead = await sourceStream.ReadAsync(memory, cancellationToken);

        //     }
        //     memory = memory.Slice(0, bytesRead);
        //     return memory.ToArray();
        // }

        static async Task<byte[]> ReceiveHttpRequestBytesSimple(Stream sourceStream, int bufferSize, CancellationToken cancellationToken = default)
        {
            var buffer = new byte[bufferSize];
            var bytesRead = await sourceStream.ReadAsync(buffer);
            var slice = buffer.AsMemory(0, bytesRead);
            return slice.ToArray();
        }

        static async Task<byte[]> ReceiveHttpRequestBytes(Stream sourceStream, int bufferSize, CancellationToken cancellationToken)
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
            ArrayPool<byte>.Shared.Return(buffer);
            return allBytes.ToArray();
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

        static async Task WriteResponse(Stream clientStream, ReadOnlyMemory<char> charMemory, CancellationToken cancellationToken)
        {
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(charMemory.Length);
            var byteMemory = rentedBuffer.AsMemory();
            var bytesWritten = Encoding.UTF8.GetBytes(charMemory.Span, byteMemory.Span);
            byteMemory = byteMemory.Slice(0, bytesWritten);
            await clientStream.WriteAsync(byteMemory, cancellationToken);
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }


        // static Request ParseRequest(string requestText)
        // {
        //     var temp = requestText.Split("\r\n\r\n");
        //     var lines = temp[0].Split("\r\n");
        //     var linesAndSpaces = lines.First().Split(" ");
        //     var headerLines = lines.Skip(1).Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Split(':', 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
        //     var headers = headerLines.ToDictionary(kvp => kvp.First(), kvp => kvp.Last(), StringComparer.InvariantCultureIgnoreCase);
        //     var hostAndPort = headers["host"].Split(":");
        //     var request = new Request
        //     {
        //         Method = linesAndSpaces[0],
        //         Path = linesAndSpaces[1],
        //         Version = linesAndSpaces[2],
        //         Host = hostAndPort[0],
        //         Port = hostAndPort.Length > 1 ? int.Parse(hostAndPort[1]) : -1,
        //         Headers = headers,
        //         Body = temp[1]
        //     };
        //     var jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(request);
        //     return request;
        // }

        //         static Request ParseRequest(ReadOnlySpan<char> requestSpan)
        //         {
        //             var index = requestSpan.IndexOf(' ');
        //             var method = requestSpan.Slice(0, index);

        //             requestSpan = requestSpan.Slice(index + 1, requestSpan.Length - index - 1);
        //             index = requestSpan.IndexOf(' ');
        //             var path = requestSpan.Slice(0, index);

        //             requestSpan = requestSpan.Slice(index + 1, requestSpan.Length - index - 1);
        //             index = requestSpan.IndexOf('\r');
        //             var version = requestSpan.Slice(0, index);

        //             requestSpan = requestSpan.Slice(index + 2, requestSpan.Length - index - 2);
        //             index = requestSpan.IndexOf(' ');
        //             var endIndex = requestSpan.IndexOf('\r');
        //             var hostAndPort = requestSpan.Slice(index + 1, endIndex - index - 1);

        //             int port = -1;
        //             var host = hostAndPort;
        //             index = hostAndPort.IndexOf(':');
        //             if (index >= 0) // has a port
        //             {
        //                 host = hostAndPort.Slice(0, index);
        //                 port = int.Parse(hostAndPort.Slice(index + 1, hostAndPort.Length - index - 1));
        //             }

        //             var request = new Request
        //             {
        //                 Method = method.ToString(),
        //                 Path = path.ToString(),
        //                 Version = version.ToString(),
        //                 Host = host.ToString(),
        //                 Port = port
        //             };
        //             return request;
        //         }
    }
}
