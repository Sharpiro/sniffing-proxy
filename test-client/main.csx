#r "System.Net.Http"

using System.Net;
using System.Net.Http;
using System.Net.Sockets;

const string proxyUrl = "http://localhost:5000/";
// var proxyUrl = Environment.GetEnvironmentVariable("http_proxy");
const string httpUrl = "http://www.httpvshttps.com/";
// const string httpsUrl = "https://raw.githubusercontent.com/Sharpiro/Tools/master/.gitignore";
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
    "https://www.google.com/search?safe=active&rlz=1C1GCEB_enUS801US801&ei=F0pOW--oLdCp_QbUv6zADA&q=how+to+run+unit+test+in+visual+studio+2017+code+lens&oq=how+to+run+unit+test+in+visual+studio+2017+code+lens&gs_l=psy-ab.3...4672.5971.0.6070.10.8.0.0.0.0.152.248.1j1.2.0....0...1.1.64.psy-ab..8.2.248...0i22i30k1j33i22i29i30k1.0.th9g-j5liQk",
    "https://github.com/Sharpiro/Tools/blob/9d490ac97f54388f415c61f4c1889ece00bd169e/interactive_scripts/csi/main.csx",
    // "http://www.httpvshttps.com/",
    "https://raw.githubusercontent.com/Sharpiro/Tools/master/.gitignore",
    "https://raw.githubusercontent.com/Sharpiro/Tools/master/youtube_downloader/Pipfile",
    "https://en.wikipedia.org/wiki/EICAR_test_file",
    // "http://www.httpvshttps.com/",
    // "http://www.httpvshttps.com/",
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
    // WriteLine(data);
}
// var res = await httpClient.GetAsync(requestUrl);
// var res = await httpClient.GetAsync("http://www.httpvshttps.com/");
// var data = await res.Content.ReadAsStringAsync();
// var x = 5;
// var res = await httpClient.GetAsync(requestUrl);
// res.EnsureSuccessStatusCode();
// res = await httpClient.GetAsync("https://en.wikipedia.org/wiki/EICAR_test_file");
// res.EnsureSuccessStatusCode();