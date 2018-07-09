using System;
using System.Buffers;
using System.Buffers.Text;
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

namespace SniffingProxy
{
    class Program
    {
        private static HttpClient _httpClient;
        private static TcpListener _tcpServer;
        private static TcpClient _clientConnection;
        private static byte[] _clientBuffer;

        static async Task Main(string[] args)
        {
            try
            {
                const string rootCertSerialNumber = "00CC78A90D47D8159A";
                const string localIp = "127.0.0.1";
                const int localPort = 5000;

                var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
                var httpClientHandler = new HttpClientHandler { Proxy = new WebProxy(proxyUrl) };
                _httpClient = new HttpClient(httpClientHandler, disposeHandler: true);

                _tcpServer = new TcpListener(IPAddress.Parse(localIp), localPort);
                _tcpServer.Start();

                _clientConnection = await _tcpServer.AcceptTcpClientAsync();

                var clientStream = _clientConnection.GetStream();

                _clientBuffer = new byte[_clientConnection.ReceiveBufferSize];

                var cancellationTokenSource = new CancellationTokenSource();

                var requestInfo = await Connect(clientStream, cancellationTokenSource.Token);
                var fakeCert = CreateFakeCertificate(requestInfo.Host, rootCertSerialNumber);
                await SecondConnectStep(clientStream, fakeCert, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }

        static async Task TcpGetRequest(CancellationToken cancellationToken)
        {
            var request = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: httpvshttps.com\r\n\r\n");
            var tempClient = new TcpClient("httpvshttps.com", 80);
            var tempStream = tempClient.GetStream();
            await tempStream.WriteAsync(request, cancellationToken);

            var buffer = new byte[5000];
            var bytesRead = await tempStream.ReadAsync(buffer, 0, buffer.Length);
            var slice = new Memory<byte>(buffer, 0, bytesRead);
            var receivedText = Encoding.UTF8.GetString(slice.Span);
        }

        static async Task<dynamic> Connect(NetworkStream clientStream, CancellationToken cancellationToken)
        {
            var clientMemory = _clientBuffer.AsMemory();
            var bytesRead = await clientStream.ReadAsync(clientMemory, cancellationToken);
            var requestText = Encoding.UTF8.GetString(clientMemory.Slice(0, bytesRead).Span);
            var request = ParseRequest(requestText);

            var responseMemory = "HTTP/1.1 200 Connection established\r\n\r\n".AsMemory();
            await WriteResponse(clientStream, responseMemory, cancellationToken);
            return request;
        }

        static async Task SecondConnectStep(Stream clientStream, X509Certificate2 fakeCert, CancellationToken cancellationToken)
        {
            var sslStream = new SslStream(clientStream, true);
            sslStream.AuthenticateAsServer(fakeCert, false, SslProtocols.Tls, true);

            var requestText = await ReceiveHttpRequest(sslStream, _clientConnection.ReceiveBufferSize, cancellationToken);
            var request = ParseRequest(requestText);

            // modify request here if you'd like, and then forward it to the remote
            var url = $"https://{request.Host}{request.Path}";
            var res = await _httpClient.GetAsync(url, cancellationToken);
            var contentBuffer = await res.Content.ReadAsByteArrayAsync();

            var headersBuffer = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {contentBuffer.Length}\r\n\r\n");
            var allBuffer = headersBuffer.Concat(contentBuffer).ToArray();
            await sslStream.WriteAsync(allBuffer, cancellationToken);
        }

        static X509Certificate2 CreateFakeCertificate(string fakeCN, string rootCertSerialNumber)
        {
            var userStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            userStore.Open(OpenFlags.MaxAllowed);
            var rootCert = userStore.Certificates.Cast<X509Certificate2>().Single(c => c.SerialNumber == rootCertSerialNumber);
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

        static async Task WriteResponse(NetworkStream clientStream, ReadOnlyMemory<char> charMemory, CancellationToken cancellationToken)
        {
            var rentedArray = ArrayPool<byte>.Shared.Rent(charMemory.Length);
            var byteMemory = rentedArray.AsMemory();
            var bytesWritten = Encoding.UTF8.GetBytes(charMemory.Span, byteMemory.Span);
            byteMemory = byteMemory.Slice(0, bytesWritten);
            await clientStream.WriteAsync(byteMemory, cancellationToken);
            ArrayPool<byte>.Shared.Return(rentedArray);
        }

        static dynamic ParseRequest(string requestText)
        {
            var lines = requestText.Split("\r\n");
            var linesAndSpaces = lines.Select(l => l.Split(" ")).ToArray();
            var hostAndPort = linesAndSpaces[1][1].Split(':');
            var request = new
            {
                Method = linesAndSpaces[0][0],
                Path = linesAndSpaces[0][1],
                Version = linesAndSpaces[0][2],

                Nothing = linesAndSpaces[1][0],
                Host = hostAndPort[0],
                Port = hostAndPort.Length > 1 ? int.Parse(hostAndPort[1]) : -1
            };
            return request;
        }
    }
}
