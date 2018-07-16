using System;
using System.Collections.Generic;
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
    public class CustomHttpsClient
    {
        private readonly Uri _proxyUri;
        private TcpClient _client;
        public SslStream RemoteSslStream { get; private set; }
        private int _clientReceiveBufferSize;

        public string _host { get; }
        public int _port { get; }

        public CustomHttpsClient(string host, int port, string proxyUrl = null)
        {
            _host = host;
            _port = port;
            if (proxyUrl != null) _proxyUri = new Uri(proxyUrl);
        }

        public async Task HandleConnect()
        {
            if (RemoteSslStream != null) throw new InvalidOperationException("The remote ssl stream has already been initialized");

            // var connectRequest = "CONNECT raw.githubusercontent.com:443 HTTP/1.1\r\nHost: raw.githubusercontent.com:443\r\n\r\n";
            var connectRequest = $"CONNECT {_host}:{_port} HTTP/1.1\r\nHost: {_host}:{_port}\r\n\r\n";
            _client = new TcpClient(_proxyUri.Host, _proxyUri.Port);
            var remoteStream = _client.GetStream();

            _clientReceiveBufferSize = _client.ReceiveBufferSize;

            await remoteStream.WriteAsync(Encoding.UTF8.GetBytes(connectRequest));

            var buffer = new byte[_clientReceiveBufferSize];
            var bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length);
            var bufferSlice = new Memory<byte>(buffer, 0, bytesRead);
            var connectionResponse = Encoding.UTF8.GetString(bufferSlice.Span);


            RemoteSslStream = new SslStream(remoteStream);
            await RemoteSslStream.AuthenticateAsClientAsync(_host);
        }

        public async Task<string> HandleSend(string requestText)
        {
            await RemoteSslStream.WriteAsync(Encoding.UTF8.GetBytes(requestText));

            // var buffer = new byte[_clientReceiveBufferSize];
            // var bytesRead = await _remoteSslStream.ReadAsync(buffer, 0, buffer.Length);
            // var bufferSlice = new Memory<byte>(buffer, 0, bytesRead);
            var buffer = await HttpData.ReceiveUpToHeaders(RemoteSslStream, _clientReceiveBufferSize, CancellationToken.None);
            var initialRawHttpResponse = Encoding.UTF8.GetString(buffer);
            var httpData = HttpData.ParseRawHttp(initialRawHttpResponse);

            if (HttpData.TryGetContentLength(httpData, out int contentLength))
            {
                var encodingService = new ContentLengthService();
                var remaining = encodingService.ParseByContentLength(RemoteSslStream, _clientReceiveBufferSize, contentLength);
            }
            else
            {
                if (!initialRawHttpResponse.Contains("Transfer-Encoding: chunked"))
                {
                    throw new Exception("invalid encoding");
                }

                var contentSlice = buffer.AsSpan(buffer.Length - httpData.ContentLength).ToArray();
                var encodingService = new EncodingService();
                var remaining = await encodingService.TransferEncoding(RemoteSslStream, _clientReceiveBufferSize, contentSlice);
            }


            // var totalBytesRead = httpData.ContentLength;
            // IEnumerable<byte> contentBytes = contentSlice.ToArray();
            // // var expectedContentlength = int.Parse(httpData.Headers["content-length"]);
            // var expectedContentlength = int.Parse(httpData.HeadersList.Single(h => h.Key == "content-length").Value);
            // while (totalBytesRead < expectedContentlength)
            // {
            //     bytesRead = await _remoteSslStream.ReadAsync(buffer, 0, buffer.Length);
            //     totalBytesRead += bytesRead;
            //     bufferSlice = new Memory<byte>(buffer, 0, bytesRead);
            //     contentBytes = contentBytes.Concat(bufferSlice.ToArray());
            // }

            // var content = Encoding.UTF8.GetString(contentBytes.ToArray());
            // return content;

            return null;
            //throw new NotImplementedException();
        }

        public async Task InitializeWithoutProxy(string host, int port)
        {
            _client = new TcpClient(host, port);
            _clientReceiveBufferSize = _client.ReceiveBufferSize;
            var remoteStream = _client.GetStream();
            RemoteSslStream = new SslStream(remoteStream);
            await RemoteSslStream.AuthenticateAsClientAsync(host);
        }

        public static async Task<CustomHttpsClient> CreateWithoutProxy(string host, int port)
        {
            var httpsClient = new CustomHttpsClient(host, port);
            await httpsClient.InitializeWithoutProxy(host, port);
            return httpsClient;
        }


        public static async Task<CustomHttpsClient> CreateWithProxy(string host, int port, string proxyUrl)
        {
            var httpsClient = new CustomHttpsClient(host, port, proxyUrl);
            await httpsClient.HandleConnect();
            return httpsClient;
        }
    }
}
