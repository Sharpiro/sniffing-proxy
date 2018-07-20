using System;
using System.Collections.Generic;
using System.Linq;

namespace SniffingProxy.Core
{
    public class Request
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string Version { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public string Body { get; set; }
        public string HostAndPort { get; set; }

        public static Request Parse(string requestText)
        {
            if (string.IsNullOrEmpty(requestText))
            {
                throw new Exception("request text was null");
            }
            var prefixToEnd = requestText.Split("\r\n", 2);
            var prefixLine = prefixToEnd[0];
            var headersAndBody = prefixToEnd[1].Split("\r\n\r\n");
            var headersText = headersAndBody[0];
            var headerLines = headersText.Split("\r\n");
            var prefixData = prefixLine.Split(" ");
            var parsedheaders = headerLines.Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Split(':', 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            var headersDictionary = parsedheaders.ToDictionary(kvp => kvp.First(), kvp => kvp.Last(), StringComparer.InvariantCultureIgnoreCase);
            var portColonIndex = prefixData[1].LastIndexOf(':');
            var hostAndPort = prefixData[1].Split(":");
            if (prefixData[1].StartsWith("http"))
            {
                throw new NotSupportedException("http not supported");
            }
            var request = new Request
            {
                Method = prefixData[0],
                Path = prefixData[1],
                Version = prefixData[2],
                Host = hostAndPort[0],
                Port = hostAndPort.Length > 1 ? int.Parse(hostAndPort[1]) : -1,
                HostAndPort = prefixData[1],
                Headers = headersDictionary,
                Body = headersAndBody[1]
            };
            return request;
        }

        // static Request ParseRequest(string requestText)
        // {
        //     var temp = requestText.Split("\r\n\r\n");
        //     var lines = temp[0].Split("\r\n");
        //     var linesAndSpaces = lines.First().Split(" ");
        //     var headerLines = lines.Skip(1).Where(l => !string.IsNullOrEmpty(l)).Select(l => l.Split(':', 2, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
        //     var headers = headerLines.ToDictionary(kvp => kvp.First(), kvp => kvp.Last(), StringComparer.InvariantCultureIgnoreCase);
        //     var hostAndPort = headers["host"].Split(":");
        //     var request = new Request
        //     {
        //         Method = linesAndSpaces[0],
        //         Path = linesAndSpaces[1],
        //         Version = linesAndSpaces[2],
        //         Host = hostAndPort[0],
        //         Port = hostAndPort.Length > 1 ? int.Parse(hostAndPort[1]) : -1,
        //         Headers = headers,
        //         Body = temp[1]
        //     };
        //     var jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(request);
        //     return request;
        // }

        //         static Request ParseRequest(ReadOnlySpan<char> requestSpan)
        //         {
        //             var index = requestSpan.IndexOf(' ');
        //             var method = requestSpan.Slice(0, index);

        //             requestSpan = requestSpan.Slice(index + 1, requestSpan.Length - index - 1);
        //             index = requestSpan.IndexOf(' ');
        //             var path = requestSpan.Slice(0, index);

        //             requestSpan = requestSpan.Slice(index + 1, requestSpan.Length - index - 1);
        //             index = requestSpan.IndexOf('\r');
        //             var version = requestSpan.Slice(0, index);

        //             requestSpan = requestSpan.Slice(index + 2, requestSpan.Length - index - 2);
        //             index = requestSpan.IndexOf(' ');
        //             var endIndex = requestSpan.IndexOf('\r');
        //             var hostAndPort = requestSpan.Slice(index + 1, endIndex - index - 1);

        //             int port = -1;
        //             var host = hostAndPort;
        //             index = hostAndPort.IndexOf(':');
        //             if (index >= 0) // has a port
        //             {
        //                 host = hostAndPort.Slice(0, index);
        //                 port = int.Parse(hostAndPort.Slice(index + 1, hostAndPort.Length - index - 1));
        //             }

        //             var request = new Request
        //             {
        //                 Method = method.ToString(),
        //                 Path = path.ToString(),
        //                 Version = version.ToString(),
        //                 Host = host.ToString(),
        //                 Port = port
        //             };
        //             return request;
        //         }
    }
}
