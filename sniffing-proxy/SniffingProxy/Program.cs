using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
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
        private static readonly ConcurrentBag<int> _clientBag = new ConcurrentBag<int>();

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
                    AcceptClient(clientConnection);

                }
            }
            catch (Exception ex)
            {
                Error.WriteLine(ex);
                _ = ex;
            }
        }

        static async void AcceptClient(TcpClient client)
        {
            _clientBag.Add(0);
            var clientId = _clientBag.Count;
            try
            {
                var clientIdStream = client.GetIdStream(clientId);
                var cancellationTokenSource = new CancellationTokenSource();
                var requestText = await ReceiveRequest(clientIdStream.Stream, client.ReceiveBufferSize, cancellationTokenSource.Token);
                var request = Request.Parse(requestText);

                switch (request.Method)
                {
                    case "CONNECT":
                        var httpsClient = await GetClient(request.Host, request.Port);
                        await HandleConnectRequest(clientIdStream, client.ReceiveBufferSize, request, cancellationTokenSource.Token);
                        WriteLine($"Client '{clientIdStream.Id}' connected for: '{request.HostAndPort}'");
                        using (var fakeCert = _certificateService.CreateFakeCertificate(request.Host, rootCertSerialNumber))
                        {
                            var clientSslStream = await AuthenticateAsServer(clientIdStream, fakeCert);
                            await HandleHttpsRequests(clientSslStream, request, client.ReceiveBufferSize, httpsClient, fakeCert, cancellationTokenSource.Token);
                        }
                        break;
                    default:
                        throw new NotSupportedException("http not supported yet");
                }
            }
            catch (IOException ex)
            {
                WriteLine($"Client '{clientId}' disconnected");
            }
            catch (Exception ex)
            {
            }
        }

        static async Task<string> ReceiveRequest(Stream clientStream, int receiveBufferSize, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(receiveBufferSize);
            var clientMemory = buffer.AsMemory();

            var bytesRead = await clientStream.ReadAsync(clientMemory, cancellationToken);
            var requestText = Encoding.UTF8.GetString(clientMemory.Slice(0, bytesRead).Span);
            return requestText;
        }

        static async Task HandleConnectRequest(IdStream clientStream, int receiveBufferSize, Request request, CancellationToken cancellationToken)
        {
            var responseMemory = "HTTP/1.1 200 Connection established\r\n\r\n".AsMemory();
            await WriteResponse(clientStream.Stream, responseMemory, cancellationToken);
        }

        static async Task<IdStream> AuthenticateAsServer(IdStream clientStream, X509Certificate2 fakeCert)
        {
            var clientSslStream = new SslStream(clientStream.Stream);
            await clientSslStream.AuthenticateAsServerAsync(fakeCert, false, SslProtocols.Tls, true);
            return clientSslStream.AsIdStream(clientStream.Id);
        }

        static async Task HandleHttpsRequests(IdStream clientSslStream, Request request, int receiveBufferSize, CustomHttpsClient httpsClient, X509Certificate2 fakeCert, CancellationToken cancellationToken)
        {
            while (true)
            {
                // receive http request from client
                var requestBytes = await ReceiveHttpRequestBytes(clientSslStream.Stream, receiveBufferSize * 2, cancellationToken);
                var requestText = Encoding.UTF8.GetString(requestBytes);

                var line = new string(requestText.Replace("\r\n\r\n", "").Replace("\r\n", " ").Take(95).ToArray());
                WriteLine($"client '{clientSslStream.Id}' req: '{line}'");

                // send request to remote
                var rawResponse = await httpsClient.HandleSend(requestText);

                // forward response to client
                await clientSslStream.Stream.WriteAsync(rawResponse, cancellationToken);
            }
        }

        static async Task<CustomHttpsClient> GetClient(string host, int port)
        {
            var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
            return proxyUrl == null ? await CustomHttpsClient.CreateWithoutProxy(host, port) : await CustomHttpsClient.CreateWithProxy(host, port, proxyUrl);
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
