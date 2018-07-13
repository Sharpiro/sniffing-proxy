using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SniffingProxy.Core
{
    public class CustomHttpsClient
    {
        private readonly Uri _proxyUri;
        private TcpClient _client;
        private SslStream _remoteSslStream;
        private int _clientReceiveBufferSize;

        public CustomHttpsClient(string proxyUrl)
        {
            _proxyUri = new Uri(proxyUrl);
        }

        public async Task HandleConnect(string requestText, Request request)
        {
            _client = new TcpClient(_proxyUri.Host, _proxyUri.Port);
            var remoteStream = _client.GetStream();

            _clientReceiveBufferSize = _client.ReceiveBufferSize;

            await remoteStream.WriteAsync(Encoding.UTF8.GetBytes(requestText));

            var buffer = new byte[_clientReceiveBufferSize];
            var bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length);
            var bufferSlice = new Memory<byte>(buffer, 0, bytesRead);
            var connectionResponse = Encoding.UTF8.GetString(bufferSlice.Span);


            _remoteSslStream = new SslStream(remoteStream, false);
            await _remoteSslStream.AuthenticateAsClientAsync(request.Host);
        }

        public async Task<string> HandleGet(string requestText, Request request)
        {
            await _remoteSslStream.WriteAsync(Encoding.UTF8.GetBytes(requestText));

            var buffer = new byte[_clientReceiveBufferSize];
            var bytesRead = await _remoteSslStream.ReadAsync(buffer, 0, buffer.Length);
            var bufferSlice = new Memory<byte>(buffer, 0, bytesRead);
            var responseHeadersText = Encoding.UTF8.GetString(bufferSlice.Span);
            var responseHeaders = ParseRawHttp(responseHeadersText);

            var totalBytesRead = 0;
            var contentBytes = Enumerable.Empty<byte>();
            var contentlength = int.Parse(responseHeaders["content-length"]);
            while (totalBytesRead < contentlength)
            {
                bytesRead = await _remoteSslStream.ReadAsync(buffer, 0, buffer.Length);
                totalBytesRead += bytesRead;
                bufferSlice = new Memory<byte>(buffer, 0, bytesRead);
                contentBytes = contentBytes.Concat(bufferSlice.ToArray());
            }

            var content = Encoding.UTF8.GetString(contentBytes.ToArray());
            return content;
        }

        public static Dictionary<string, string> ParseRawHttp(string requestText)
        {
            var prefixToEnd = requestText.Split("\r\n", 2);
            var prefixLine = prefixToEnd[0];
            var headersAndBody = prefixToEnd[1].Split("\r\n\r\n");
            var headersText = headersAndBody[0];
            var headerLines = headersText.Split("\r\n");
            var prefixData = prefixLine.Split(" ");
            var parsedheaders = headerLines.Skip(1).Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Split(':', 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            var headersDictionary = parsedheaders.ToDictionary(kvp => kvp.First(), kvp => kvp.Last(), StringComparer.InvariantCultureIgnoreCase);
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
            return headersDictionary;
        }
    }
}
