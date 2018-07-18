using System;
using Xunit;
using SniffingProxy.Core;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace SniffingProxy.Tests
{
    public class HeadersServiceTests
    {
        [Fact]
        public async Task ReceiveUpToHeadersSimpleTest()
        {
            const string headersPlusChunkPart = "HTTP/1.1 200 OK\r\nServer: GitHub.com\r\nContent-Type: text/html; charset=utf-8\r\nTransfer-Encoding: chunked\r\n\r\n7748\r\n\n\n\n\n\n\n<!DOCTYPE html>\n<html lang=\"en\">\n  <head>\n    <meta charset=\"utf-8\">\n  <link rel=\"dns-prefetch\" href=\"https://assets-cdn.github.com\">\n  <link rel=\"dns-prefetch\" href=\"https://avatars0.githubusercontent.com\">\n  <link rel=\"dns-prefetch\" href=\"https://avatars1.githubusercontent.com\">\n  <link rel=\"dns-prefetch\" href=\"https://avatars2.githubusercontent.com\">\n  <link rel=\"dns-prefetch\" href=\"https://avatars3.githubusercontent.com\">\n  <link rel=\"dns-prefetch\" href=\"http";
            var expectedHeadersText = headersPlusChunkPart.Substring(0, headersPlusChunkPart.IndexOf("\r\n\r\n") + 4);

            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(headersPlusChunkPart));

            var headresService = new HeadersService();
            var buffer = await headresService.ReceiveUpToHeaders(memoryStream, 65536, CancellationToken.None);
            var actualHeadersText = Encoding.UTF8.GetString(buffer);

            Assert.Equal(expectedHeadersText, actualHeadersText);
        }


        [Fact]
        public async Task ReceiveUpToHeadersTest()
        {
            const string headersPlusChunkPart = "HTTP/1.1 200 OK\r\nServer: GitHub.com\r\nDate: Wed, 18 Jul 2018 14:10:29 GMT\r\nContent-Type: text/html; charset=utf-8\r\nTransfer-Encoding: chunked\r\nStatus: 200 OK\r\nCache-Control: no-cache\r\nVary: X-PJAX\r\nSet-Cookie: has_recent_activity=1; path=/; expires=Wed, 18 Jul 2018 15:10:29 -0000\r\nSet-Cookie: _octo=GH1.1.1860803430.1531923029; domain=.github.com; path=/; expires=Sat, 18 Jul 2020 14:10:29 -0000\r\nSet-Cookie: logged_in=no; domain=.github.com; path=/; expires=Sun, 18 Jul 2038 14:10:29 -0000; secure; HttpOnly\r\nSet-Cookie: _gh_sess=VEprbktxU3o1NmlISlBqVjczcTJtU2NoK2M4WXRHYyt1bStkUHVxbHBNam5FZU5Fb0tsREw1WDFnbHVES2QxRXVBa1NlWlk0QVZTdldQdXBKWk9HUFJnOE5KbVY2YVRONFpQL0dINmhQdjdBejYycVNhZ0w2RjczcWN6SVA4S2Nwdy9qNkgwNkU0WFdIU084WlZ2TGlVNWZacFBGRU1GUDloTVlpaTZuS0VCVXZSVXUrZTd2ZEVieXlSVEJoQTVveDlEZ3R5dzkrVnM2WENNS0tmcUZOUExITU5PdXc3R3hnT1BRMFdDcnoyT2RrVGNoelJWeWpYM0x5ZGRUOTk4TTMvenFkaW54UVpGZWV1ODIybDJxaXc9PS0tTm5KNzRlbDg2QWprK1JvTi9acGRaUT09--91fede3e983ea7b917b1551d385ac35fdec893b4; path=/; secure; HttpOnly\r\nX-Request-Id: df60f1a6-bc1a-4566-84ab-e9ca21ff8ff8\r\nX-Runtime: 0.347782\r\nStrict-Transport-Security: max-age=31536000; includeSubdomains; preload\r\nX-Frame-Options: deny\r\nX-Content-Type-Options: nosniff\r\nX-XSS-Protection: 1; mode=block\r\nExpect-CT: max-age=2592000, report-uri=\"https://api.github.com/_private/browser/errors\"\r\nContent-Security-Policy: default-src 'none'; base-uri 'self'; block-all-mixed-content; connect-src 'self' uploads.github.com status.github.com collector.githubapp.com api.github.com www.google-analytics.com github-cloud.s3.amazonaws.com github-production-repository-file-5c1aeb.s3.amazonaws.com github-production-upload-manifest-file-7fdce7.s3.amazonaws.com github-production-user-asset-6210df.s3.amazonaws.com wss://live.github.com; font-src assets-cdn.github.com; form-action 'self' github.com gist.github.com; frame-ancestors 'none'; frame-src render.githubusercontent.com; img-src 'self' data: assets-cdn.github.com identicons.github.com collector.githubapp.com github-cloud.s3.amazonaws.com *.githubusercontent.com; manifest-src 'self'; media-src 'none'; script-src assets-cdn.github.com; style-src 'unsafe-inline' assets-cdn.github.com\r\nX-Runtime-rack: 0.358282\r\nX-GitHub-Request-Id: 24E8:65A3:245BCAC:442F609:5B4F4A55\r\n\r\n7748\r\n\n\n\n\n\n\n<!DOCTYPE html>\n<html lang=\"en\">\n  <head>\n    <meta charset=\"utf-8\">\n  <link rel=\"dns-prefetch\" href=\"https://assets-cdn.github.com\">\n  <link rel=\"dns-prefetch\" href=\"https://avatars0.githubusercontent.com\">\n  <link rel=\"dns-prefetch\" href=\"https://avatars1.githubusercontent.com\">\n  <link rel=\"dns-prefetch\" href=\"https://avatars2.githubusercontent.com\">\n  <link rel=\"dns-prefetch\" href=\"https://avatars3.githubusercontent.com\">\n  <link rel=\"dns-prefetch\" href=\"http";
            var expectedHeadersText = headersPlusChunkPart.Substring(0, headersPlusChunkPart.IndexOf("\r\n\r\n") + 4);

            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(headersPlusChunkPart));

            var headresService = new HeadersService();
            var buffer = await headresService.ReceiveUpToHeaders(memoryStream, 65536, CancellationToken.None);
            var actualHeadersText = Encoding.UTF8.GetString(buffer);

            Assert.Equal(expectedHeadersText, actualHeadersText);
        }
    }
}
