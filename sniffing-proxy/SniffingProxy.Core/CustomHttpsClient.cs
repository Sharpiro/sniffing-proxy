using System;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SniffingProxy.Core
{
    // Currently doing 1 CustomHttpsClient per connection
    public class CustomHttpsClient : IDisposable
    {
        private readonly Uri _proxyUri;
        private TcpClient _client;
        private SslStream _remoteSslStream;
        private int _clientReceiveBufferSize;

        private string _host { get; }
        private int _port { get; }
        private string _version { get; }

        public CustomHttpsClient(string host, int port, string version, string proxyUrl = null)
        {
            _host = host;
            _port = port;
            _version = version;
            if (proxyUrl != null) _proxyUri = new Uri(proxyUrl);
        }

        public async Task HandleConnect()
        {
            if (_remoteSslStream != null) throw new InvalidOperationException("The remote ssl stream has already been initialized");

            var connectRequest = $"CONNECT {_host}:{_port} {_version}\r\nHost: {_host}:{_port}\r\n\r\n";
            _client = new TcpClient(_proxyUri.Host, _proxyUri.Port);
            var remoteStream = _client.GetStream();

            _clientReceiveBufferSize = _client.ReceiveBufferSize;

            await remoteStream.WriteAsync(Encoding.UTF8.GetBytes(connectRequest));

            var buffer = new byte[_clientReceiveBufferSize];
            var bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length);
            var bufferSlice = new Memory<byte>(buffer, 0, bytesRead);
            var connectionResponse = Encoding.UTF8.GetString(bufferSlice.Span);


            _remoteSslStream = new SslStream(remoteStream);
            await _remoteSslStream.AuthenticateAsClientAsync(_host);
        }

        public async Task<byte[]> HandleSend(string requestText)
        {
            await _remoteSslStream.WriteAsync(Encoding.UTF8.GetBytes(requestText));

            var headresService = new HeadersService();
            var buffer = await headresService.ReceiveUpToHeaders(_remoteSslStream, _clientReceiveBufferSize, CancellationToken.None);
            var initialRawHttpResponse = Encoding.UTF8.GetString(buffer);
            var httpData = HttpData.ParseRawHttp(initialRawHttpResponse);

            if (httpData.StatusCode == (int)HttpStatusCode.NotModified)
            {
                return buffer;
            }


            if (httpData.StatusCode == (int)HttpStatusCode.Continue)
            {
                return buffer;
            }

            byte[] rawResponse;
            if (int.TryParse(httpData.HeadersList.SingleOrDefault(h => h.Key == "Content-Length").Value, out int contentLength))
            {
                var encodingService = new ContentLengthService();
                rawResponse = await encodingService.ParseByContentLength(_remoteSslStream, _clientReceiveBufferSize, contentLength);
            }
            else
            {
                if (!initialRawHttpResponse.Contains("Transfer-Encoding: chunked"))
                {
                    throw new Exception("invalid encoding");
                }

                var contentSlice = buffer.AsSpan(buffer.Length - httpData.ContentLength).ToArray();
                var encodingService = new TransferEncodingService();
                rawResponse = await encodingService.TransferEncoding(_remoteSslStream, _clientReceiveBufferSize, contentSlice);
            }

            var headersSlice = buffer.AsSpan(0, buffer.Length - httpData.ContentLength).ToArray();

            var fullResponse = headersSlice.Concat(rawResponse).ToArray();
            var fullResText = Encoding.UTF8.GetString(fullResponse);
            return fullResponse;
        }

        public async Task InitializeWithoutProxy(string host, int port)
        {
            _client = new TcpClient(host, port);
            _clientReceiveBufferSize = _client.ReceiveBufferSize;
            var remoteStream = _client.GetStream();
            _remoteSslStream = new SslStream(remoteStream);
            await _remoteSslStream.AuthenticateAsClientAsync(host);
        }

        public void Dispose()
        {
            _client?.Dispose();
            _remoteSslStream?.Dispose();
        }

        public static async Task<CustomHttpsClient> CreateWithoutProxy(string host, int port, string version)
        {
            var httpsClient = new CustomHttpsClient(host, port, version);
            await httpsClient.InitializeWithoutProxy(host, port);
            return httpsClient;
        }


        public static async Task<CustomHttpsClient> CreateWithProxy(string host, int port, string version, string proxyUrl)
        {
            var httpsClient = new CustomHttpsClient(host, port, version, proxyUrl);
            await httpsClient.HandleConnect();
            return httpsClient;
        }
    }
}
