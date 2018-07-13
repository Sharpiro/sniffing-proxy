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

namespace SniffingProxy
{
    class Program
    {
        // private const string rootCertSerialNumber = "00CC78A90D47D8159A";
        private const string rootCertSerialNumber = "00ed57f3562fd3d663";

        private static HttpClient _httpClient;
        private static TcpListener _tcpServer;

        static async Task Main(string[] args)
        {
            try
            {
                // ParseRequest("CONNECT raw.githubusercontent.com:443 HTTP/1.1\r\nHost: raw.githubusercontent.com:443\r\n\r\n");
                const string localIp = "127.0.0.1";
                const int localPort = 5000;

                var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
                var httpClientHandler = new HttpClientHandler
                {
                    // Proxy = new WebProxy(proxyUrl),
                    // AllowAutoRedirect = false
                };
                _httpClient = new HttpClient(httpClientHandler, disposeHandler: true);


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
                switch (request.Method)
                {
                    case "CONNECT":
                        await HandleConnectRequest(clientStream, client.ReceiveBufferSize, request, cancellationTokenSource.Token);
                        var fakeCert = CreateFakeCertificate(request.Host, rootCertSerialNumber);
                        await HandleHttpsRequest(clientStream, client.ReceiveBufferSize, fakeCert, cancellationTokenSource.Token);
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
            var request = ParseRequest(requestText);
            return request;
        }

        static async Task HandleHttpConnection(Stream stream, int receiveBufferSize, Request request, CancellationToken cancellationToken)
        {
            var response = await HandleRequest("http", request, cancellationToken);
            await stream.WriteAsync(response);
            while (true)
            {
                var requestText = await ReceiveHttpRequest(stream, receiveBufferSize, cancellationToken);
                request = ParseRequest(requestText);
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
            try
            {
                var uncompressedStream = new MemoryStream();
                var streamBuffer = await res.Content.ReadAsStreamAsync();
                var gzipStream = new GZipStream(streamBuffer, CompressionMode.Decompress);
                gzipStream.CopyTo(uncompressedStream);
                var data = Encoding.UTF8.GetString(uncompressedStream.ToArray());
            }
            catch (Exception ex)
            {

            }
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

        static async Task HandleHttpsRequest(Stream clientStream, int receiveBufferSize, X509Certificate2 fakeCert, CancellationToken cancellationToken)
        {
            var sslStream = new SslStream(clientStream, true);
            await sslStream.AuthenticateAsServerAsync(fakeCert, false, SslProtocols.Tls, true);

            while (true)
            {
                var requestText = await ReceiveHttpRequest(sslStream, receiveBufferSize, cancellationToken);
                var request = ParseRequest(requestText);

                // modify request here if you'd like, and then forward it to the remote
                var res = await HandleRequest("https", request, cancellationToken);
                await sslStream.WriteAsync(res, cancellationToken);
            }
        }

        static X509Certificate2 CreateFakeCertificate(string fakeCN, string rootCertSerialNumber)
        {
            var userStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            userStore.Open(OpenFlags.MaxAllowed);
            var rootCert = userStore.Certificates.Cast<X509Certificate2>().Single(c => c.SerialNumber.Equals(rootCertSerialNumber, StringComparison.InvariantCultureIgnoreCase));
            using (var rsa = RSA.Create(2048))
            {
                var req = new CertificateRequest($"CN={fakeCN}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));
                req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection {
                     new Oid("1.3.6.1.5.5.7.3.1") , // server auth
                     new Oid("1.3.6.1.5.5.7.3.2") // client auth
                     }, true));
                req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

                // https://github.com/dotnet/corefx/issues/24454#issuecomment-388231655
                var corruptFakeCert = req.Create(rootCert, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90), new byte[] { 1, 2, 3, 4 }).CopyWithPrivateKey(rsa);
                var fixedFakeCert = new X509Certificate2(corruptFakeCert.Export(X509ContentType.Pkcs12));

                return fixedFakeCert;
            }
        }

        static async Task<string> ReceiveHttpRequest(Stream sourceStream, int bufferSize, CancellationToken cancellationToken)
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


        static Request ParseRequest(string requestText)
        {
            var temp = requestText.Split("\r\n\r\n");
            var lines = temp[0].Split("\r\n");
            var linesAndSpaces = lines.First().Split(" ");
            var headerLines = lines.Skip(1).Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Split(':', 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            var headers = headerLines.ToDictionary(kvp => kvp.First(), kvp => kvp.Last(), StringComparer.InvariantCultureIgnoreCase);
            var hostAndPort = headers["host"].Split(":");
            var request = new Request
            {
                Method = linesAndSpaces[0],
                Path = linesAndSpaces[1],
                Version = linesAndSpaces[2],
                Host = hostAndPort[0],
                Port = hostAndPort.Length > 1 ? int.Parse(hostAndPort[1]) : -1,
                Headers = headers,
                Body = temp[1]
            };
            var jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(request);
            return request;
        }

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

public class Request
{
    public string Method { get; set; }
    public string Path { get; set; }
    public string Version { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string Body { get; set; }
}