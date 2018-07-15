using System;
using Xunit;
using SniffingProxy.Core;
using System.Threading.Tasks;

namespace SniffingProxy.Tests
{
    public class CustomHttpsClientTests
    {
        [Fact]
        public async Task ConnectTest()
        {
            const string host = "raw.githubusercontent.com";
            const int port = 443;
            // const string requestText = "CONNECT raw.githubusercontent.com:443 HTTP/1.1\r\nHost: raw.githubusercontent.com:443\r\n\r\n";
            // var parsedRequest = Request.Parse(requestText);
            var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
            // var customHttpsClient = await CustomHttpsClient.CreateWithProxy(host, port, proxyUrl);
            var customHttpsClient = new CustomHttpsClient(host, port, proxyUrl);
            await customHttpsClient.HandleConnect();
        }

        [Fact]
        public async Task SendTest()
        {

            // //connect
            // const string connectRequestText = "CONNECT raw.githubusercontent.com:443 HTTP/1.1\r\nHost: raw.githubusercontent.com:443\r\n\r\n";
            // var parsedConnectRequest = Request.Parse(connectRequestText);
            // customHttpsClient.HandleConnect(connectRequestText, parsedConnectRequest).Wait();

            const string host = "raw.githubusercontent.com";
            const int port = 443;
            const string getRequestText = "GET /Sharpiro/Tools/9d490ac97f54388f415c61f4c1889ece00bd169e/interactive_scripts/csi/main.csx HTTP/1.1\r\nHost: raw.githubusercontent.com\r\n\r\n";

            var customHttpsClient = await GetClient(host, port);
            // var parsedGetRequest = Request.Parse(getRequestText);
            await customHttpsClient.HandleSend(getRequestText);
        }

        private async Task<CustomHttpsClient> GetClient(string host, int port)
        {
            var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
            return proxyUrl == null ? await CustomHttpsClient.CreateWithoutProxy(host, port) : await CustomHttpsClient.CreateWithProxy(host, port, proxyUrl);
        }
    }
}
