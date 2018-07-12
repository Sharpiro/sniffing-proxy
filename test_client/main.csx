#r "System.Net.Http"

using System.Net;
using System.Net.Http;
using System.Net.Sockets;

const string proxyUrl = "http://localhost:5000/";
// var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
const string httpUrl = "http://www.httpvshttps.com/";
const string httpsUrl = "https://raw.githubusercontent.com/Sharpiro/Tools/master/.gitignore";
// const string requestUrl = "https://raw.githubusercontent.com/Sharpiro/Tools/9d490ac97f54388f415c61f4c1889ece00bd169e/interactive_scripts/csi/main.csx";
// const string requestUrl = "https://github.com/Sharpiro/Tools/blob/9d490ac97f54388f415c61f4c1889ece00bd169e/interactive_scripts/csi/main.csx";
// const string requestUrl = "https://github.com/Sharpiro/Tools/blob/9d490ac97f54388f415c61f4c1889ece00bd169e/shared/Blake2Scripted.csx";
// const string requestUrl = "https://wrong.host.badssl.com/";
// const string requestUrl = "https://en.wikipedia.org/wiki/EICAR_test_file";
// const string requestUrl ="http://localhost:5000/monkeypage";
var httpClientHandler = new HttpClientHandler { Proxy = new WebProxy(proxyUrl) };
var httpClient = new HttpClient(httpClientHandler, disposeHandler: true);
// var httpClient = new HttpClient();

var requestList = new string[]
{
    "https://raw.githubusercontent.com/Sharpiro/Tools/master/.gitignore",
    "http://www.httpvshttps.com/",
    "https://raw.githubusercontent.com/Sharpiro/Tools/master/.gitignore",
    "https://en.wikipedia.org/wiki/EICAR_test_file",
    "http://www.httpvshttps.com/",
    "http://www.httpvshttps.com/",
};

foreach (var url in requestList)
{
    // var tempUrl = i % 2 == 0 ? httpUrl : httpsUrl;
    var res = await httpClient.GetAsync(url);
    if (res.StatusCode != HttpStatusCode.OK)
    {
        var reason = await res.Content.ReadAsStringAsync();
        throw new Exception($"Response was not successful.  Reason: '{reason}'");
    }

    var data = await res.Content.ReadAsStringAsync();

    WriteLine($"success: {url}");
}
// var res = await httpClient.GetAsync(requestUrl);
// var res = await httpClient.GetAsync("http://www.httpvshttps.com/");
// var data = await res.Content.ReadAsStringAsync();
// var x = 5;
// var res = await httpClient.GetAsync(requestUrl);
// res.EnsureSuccessStatusCode();
// res = await httpClient.GetAsync("https://en.wikipedia.org/wiki/EICAR_test_file");
// res.EnsureSuccessStatusCode();