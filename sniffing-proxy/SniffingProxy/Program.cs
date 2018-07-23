using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SniffingProxy.Core;
using static System.Console;

namespace SniffingProxy
{
    class Program
    {
        private const string rootCertSerialNumber = "00CC78A90D47D8159A";
        //private const string rootCertSerialNumber = "0093a3b2e3719990af";

        private static TcpListener _tcpServer;
        private static CertificateService _certificateService = new CertificateService();
        private static readonly string _proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
        private static int _clientCount;
        private static readonly object _lock = new object();
        private static readonly List<string> _acceptedHosts = new List<string>
        {
            "raw.githubusercontent.com",
            "github.com",
            "www.youtube.com"
        };
        private static readonly ConcurrentDictionary<string, ClientInfo> _clients = new ConcurrentDictionary<string, ClientInfo>();

        static async Task Main(string[] args)
        {
            try
            {
                const string localIp = "127.0.0.1";
                const int localPort = 5000;

                _tcpServer = new TcpListener(IPAddress.Parse(localIp), localPort);
                _tcpServer.Start();

                while (true)
                {
                    var clientConnection = await _tcpServer.AcceptTcpClientAsync();
                    await AcceptClient(clientConnection);
                    //await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex);
                _ = ex;
            }
        }

        static async Task AcceptClient(TcpClient client)
        {
            var clientId = 0;
            //ClientInfo clientInfo = null;
            try
            {
                var clientStream = client.GetStream();
                var cancellationTokenSource = new CancellationTokenSource(15 * 1000);
                var requestText = await ReceiveRequest(clientStream, client.ReceiveBufferSize, cancellationTokenSource.Token);
                var request = Request.Parse(requestText);

                //if (!_acceptedHosts.Any(ah => request.Host.Contains(ah)))
                //{
                //    client.Dispose();
                //    return;
                //}

                lock (_lock)
                {
                    _clientCount++;
                    clientId = _clientCount;
                }
                //_clients.TryAdd()

                switch (request.Method)
                {
                    case "CONNECT":

                        using (var clientInfo = client.AsClientInfo(clientId))
                        using (var httpsClient = await GetClient(request.Host, request.Port, request.Version))
                        using (var fakeCert = _certificateService.CreateFakeCertificate(request.Host, rootCertSerialNumber))
                        {
                            await HandleConnectRequest(clientInfo, client.ReceiveBufferSize, request.Version, cancellationTokenSource.Token);
                            var threadId = Thread.CurrentThread.ManagedThreadId;
                            WriteLine($"Client '{clientInfo.Id}: {clientInfo.Remote}' connected on thread '{threadId}' for: '{request.HostAndPort}'");
                            //var fakeCertHash = string.Join(string.Empty, System.Security.Cryptography.SHA256.Create().ComputeHash(fakeCert.RawData).Select(b => b.ToString("x2")));
                            //if (fakeCertHash != "a28a5e7ebbfe5a2cd1040ba579f58d77988b7cbd320db27fe41b94b442ff1a47")
                            //{
                            //    throw new Exception("invalid cert...");
                            //}
                            await AuthenticateAsServer(clientInfo, fakeCert);
                            await HandleHttpsRequests(clientInfo, client.ReceiveBufferSize, httpsClient);
                        }
                        break;
                    default:
                        throw new NotSupportedException("http not supported yet");
                }
            }
            catch (IOException ex)
            {
                WriteLine($"Client '{clientId}' disconnected");
                WriteLine(ex.Message);
                //clientInfo?.Dispose();
                lock (_lock)
                {
                    _clientCount--;
                }
            }
            catch (Exception ex)
            {
            }
        }

        static void HackClearCache()
        {
            var sslAssembly = Assembly.GetAssembly(typeof(SslStream));

            var sslSessionCacheClass = sslAssembly.GetType("System.Net.Security.SslSessionsCache");

            var cachedCredsInfo = sslSessionCacheClass.GetField("s_CachedCreds", BindingFlags.NonPublic | BindingFlags.Static);
            var cachedCreds = (Hashtable)cachedCredsInfo.GetValue(null);

            cachedCreds.Clear();
        }

        static async Task<string> ReceiveRequest(Stream clientStream, int receiveBufferSize, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(receiveBufferSize);
            var clientMemory = buffer.AsMemory();

            var bytesRead = await clientStream.ReadAsync(clientMemory, cancellationToken);
            var requestText = Encoding.UTF8.GetString(clientMemory.Slice(0, bytesRead).Span);
            return requestText;
        }

        static async Task HandleConnectRequest(ClientInfo clientStream, int receiveBufferSize, string version, CancellationToken cancellationToken)
        {
            var responseMemory = $"{version} 200 Connection established\r\n\r\n".AsMemory();
            await WriteResponse(clientStream.Stream, responseMemory, cancellationToken);
        }

        static async Task AuthenticateAsServer(ClientInfo clientInfo, X509Certificate2 fakeCert)
        {

            var options = new SslServerAuthenticationOptions
            {
                AllowRenegotiation = false,
                ServerCertificate = fakeCert,
                ClientCertificateRequired = false,
                EnabledSslProtocols = SslProtocols.Tls,
            };
            var clientSslStream = new SslStream(clientInfo.Stream);
            //await clientSslStream.AuthenticateAsServerAsync(fakeCert, false, SslProtocols.Tls, false);
            await clientSslStream.AuthenticateAsServerAsync(options, CancellationToken.None);
            clientInfo.Stream = clientSslStream;
        }

        static async Task HandleHttpsRequests(ClientInfo clientInfo, int receiveBufferSize, CustomHttpsClient httpsClient)
        {
            while (true)
            {
                var cts = new CancellationTokenSource(105 * 1000);
                // receive http request from client
                var requestBytes = await ReceiveHttpRequestBytes(clientInfo.Stream, receiveBufferSize, cts.Token);
                var requestText = Encoding.UTF8.GetString(requestBytes);

                var line = new string(requestText.Replace("\r\n\r\n", "").Replace("\r\n", " ").Take(95).ToArray());
                //var line = requestText.Replace("\r\n\r\n", "").Replace("\r\n", " ");
                WriteLine($"client '{clientInfo.Id}' req: '{line}'");

                // send request to remote
                var rawResponse = await httpsClient.HandleSend(requestText);
                var responseText = Encoding.UTF8.GetString(rawResponse);

                // forward response to client
                await clientInfo.Stream.WriteAsync(rawResponse, cts.Token);

                if (responseText.Contains("Connection: close"))
                {
                    throw new IOException("closing connection");
                }
            }
        }

        static async Task<CustomHttpsClient> GetClient(string host, int port, string version)
        {
            //var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
            var proxyUrl = "http://localhost:8888";
            return proxyUrl == null ? await CustomHttpsClient.CreateWithoutProxy(host, port, version) : await CustomHttpsClient.CreateWithProxy(host, port, version, proxyUrl);
        }

        static async Task<byte[]> ReceiveHttpRequestBytes(Stream sourceStream, int bufferSize, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            var bufferIndex = 0;
            var totalBytesRead = 0;
            while (true)
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

        static async Task WriteResponse(Stream clientStream, ReadOnlyMemory<char> charMemory, CancellationToken cancellationToken)
        {
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(charMemory.Length);
            var byteMemory = rentedBuffer.AsMemory();
            var bytesWritten = Encoding.UTF8.GetBytes(charMemory.Span, byteMemory.Span);
            byteMemory = byteMemory.Slice(0, bytesWritten);
            await clientStream.WriteAsync(byteMemory, cancellationToken);
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
}
