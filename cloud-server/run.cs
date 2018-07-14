using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System;
using System.Linq;
using System.Text;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
    
    string urlEncodedUrl;
    string base64Url;
    try
    {
        // parse query parameter
        urlEncodedUrl = req.GetQueryNameValuePairs()
            .FirstOrDefault(q => string.Compare(q.Key, "data", true) == 0)
            .Value;
        base64Url = Uri.UnescapeDataString(urlEncodedUrl);
        log.Info(base64Url);
        var url = Encoding.UTF8.GetString(Convert.FromBase64String(base64Url).Select(Swap).ToArray());
        

        await Task.Yield();
        var client = new HttpClient();
        var responseMessage = await client.GetAsync(url);
        var bytes = await responseMessage.Content.ReadAsByteArrayAsync();
        var content = new ByteArrayContent(bytes.Select(Swap).ToArray());
        
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        if (url == null) throw new NullReferenceException("url cannot be null");
        response.Content = content;
        //response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return response;
    }
    catch(Exception ex)
    {
        var responseX = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        responseX.Content = new StringContent(ex.Message);
        return responseX;
    }
}

static byte Swap(byte @byte)
{
    return (byte)(~@byte & 0xff);
}