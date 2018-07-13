using System;
using Xunit;
using SniffingProxy.Core;

namespace SniffingProxy.Tests
{
    public class CustomHttpsClientTests
    {
        [Fact]
        public void ConnectTest()
        {
            const string requestText = "CONNECT raw.githubusercontent.com:443 HTTP/1.1\r\nHost: raw.githubusercontent.com:443\r\n\r\n";
            var parsedRequest = Request.Parse(requestText);
            var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
            var customHttpsClient = new CustomHttpsClient(proxyUrl);
            customHttpsClient.HandleConnect(requestText, parsedRequest).Wait();
        }

        [Fact]
        public void GetTest()
        {
            var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
            var customHttpsClient = new CustomHttpsClient(proxyUrl);

            //connect
            const string connectRequestText = "CONNECT raw.githubusercontent.com:443 HTTP/1.1\r\nHost: raw.githubusercontent.com:443\r\n\r\n";
            var parsedConnectRequest = Request.Parse(connectRequestText);
            customHttpsClient.HandleConnect(connectRequestText, parsedConnectRequest).Wait();

            //get
            const string getRequestText = "GET /Sharpiro/Tools/9d490ac97f54388f415c61f4c1889ece00bd169e/interactive_scripts/csi/main.csx HTTP/1.1\r\nHost: raw.githubusercontent.com\r\n\r\n";
            var parsedGetRequest = Request.Parse(connectRequestText);
            customHttpsClient.HandleGet(getRequestText, parsedGetRequest).Wait();
        }
    }
}
