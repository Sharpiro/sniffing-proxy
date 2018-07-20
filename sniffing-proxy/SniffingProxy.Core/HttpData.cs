using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace SniffingProxy.Core
{
    public class HttpData
    {
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public IEnumerable<KeyValuePair<string, string>> HeadersList { get; set; }
        public int ContentLength { get; set; }
        public int StatusCode { get; set; }

        public static HttpData ParseRawHttp(string requestText)
        {
            var headersEndIndex = requestText.IndexOf("\r\n\r\n");
            // var testHeaders = requestText.Substring(0, endHeaderIndex);

            var expectedBodyLength = requestText.Length - (headersEndIndex + 4);
            // var actualBody = requestText.Substring(headersEndIndex + 4);
            // var actualBodyLength = actualBody.Length;


            var prefixToEnd = requestText.Split("\r\n", 2);
            var prefixLine = prefixToEnd[0];
            var headersAndBody = prefixToEnd[1].Split("\r\n\r\n");
            // var headersText = prefixToEnd[1].Substring(0, headersEndIndex);
            var headersText = headersAndBody[0];
            var headerLines = headersText.Split("\r\n");
            var prefixData = prefixLine.Split(" ");
            var statusCode = int.Parse(prefixData[1]);
            var parsedheaders = headerLines.Select(l => l.Split(':', 2).Select(s => s.Trim()).ToArray());
            var headersKvp = parsedheaders.Select(h => KeyValuePair.Create(h[0], h[1]));
            // var headersDictionary = parsedheaders.ToDictionary(kvp => kvp.First(), kvp => kvp.Last(), StringComparer.InvariantCultureIgnoreCase);
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
            return new HttpData
            {
                // Headers = headersDictionary,
                StatusCode = statusCode,
                HeadersList = headersKvp,
                ContentLength = expectedBodyLength
            };
        }
    }
}
