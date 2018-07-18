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
            // var hostAndPort = headersDictionary["host"].Split(":");
            var portColonIndex = prefixData[1].LastIndexOf(':');
            var hostAndPort = prefixData[1].Split(":");
            var request = new Request
            {
                Method = prefixData[0],
                Path = prefixData[1],
                Version = prefixData[2],
                Host = hostAndPort[0],
                Port = hostAndPort.Length > 1 ? int.Parse(hostAndPort[1]) : -1,
                Headers = headersDictionary,
                Body = headersAndBody[1]
            };
            // var jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(request);
            return request;
        }
    }
}
