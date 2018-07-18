using System;
using Xunit;
using SniffingProxy.Core;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Diagnostics;

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
        public async Task ContentLengthTest()
        {
            const string host = "raw.githubusercontent.com";
            const int port = 443;
            const string getRequestText = "GET /Sharpiro/Tools/9d490ac97f54388f415c61f4c1889ece00bd169e/interactive_scripts/csi/main.csx HTTP/1.1\r\nHost: raw.githubusercontent.com\r\n\r\n";

            var customHttpsClient = await GetClient(host, port);
            // var parsedGetRequest = Request.Parse(getRequestText);
            await customHttpsClient.HandleSend(getRequestText);
        }

        [Fact]
        public async Task TransferEncodingTest()
        {
            const string host = "github.com";
            const int port = 443;
            const string getRequestText = "GET /Sharpiro/Tools/blob/9d490ac97f54388f415c61f4c1889ece00bd169e/interactive_scripts/csi/main.csx HTTP/1.1\r\nHost: github.com\r\n\r\n";

            var customHttpsClient = await GetClient(host, port);
            // var parsedGetRequest = Request.Parse(getRequestText);
            for (var i = 0; i < 500; i++)
            {
                await customHttpsClient.HandleSend(getRequestText);
                Debug.WriteLine("----------complete-------------");
            }
        }

        [Fact]
        public async Task TransferEncodingTest2()
        {
            const string expectedData = "7\r\nMozilla\r\n9\r\nDeveloper\r\n7\r\nNetwork\r\n0\r\n\r\n";
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(expectedData));

            var encodingService = new TransferEncodingService();


            var remaining = await encodingService.TransferEncoding(memoryStream, 65536);
            var actualData = Encoding.UTF8.GetString(remaining);
            Assert.Equal(expectedData, actualData);
        }

        private async Task<CustomHttpsClient> GetClient(string host, int port)
        {
            var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
            return proxyUrl == null ? await CustomHttpsClient.CreateWithoutProxy(host, port) : await CustomHttpsClient.CreateWithProxy(host, port, proxyUrl);
        }
    }
}
