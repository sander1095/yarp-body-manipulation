using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using Yarp.ReverseProxy.Forwarder;


// do a POST to /track with the following request body:

//{
//    "Key": "%PLACEHOLDER%"
//}


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseHttpsRedirection();

var transformer = new ReplaceTokenHttpRequestBodyTransformer();
var httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
{
    UseProxy = false,
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false
});

var forwarder = app.Services.GetRequiredService<IHttpForwarder>();

// Ugly endpoint because YARP adds /track to the endpoint but whatever.
app.MapMethods("/track-done/track", new [] { "OPTIONS", "HEAD" , "GET", "POST", "PUT", "GET"}, ([FromBody] TrackRequest request) =>
{
    return request.Key;
});

app.Map("/track", async httpContext =>
{
    httpContext.Request.EnableBuffering();

    ForwarderError error = await forwarder.SendAsync(
        context: httpContext,
        destinationPrefix: "https://localhost:7154/track-done", // We forward to ourselves, but just imagine this being a different endpoint on another server..
        httpClient: httpClient,
        requestConfig: new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(100) },
        transformer: transformer);

    if (error != ForwarderError.None)
    {
        var errorFeature = httpContext.GetForwarderErrorFeature();
        var exception = errorFeature.Exception;
    }
});

app.MapReverseProxy();

app.Run();

class ReplaceTokenHttpRequestBodyTransformer : HttpTransformer
{
    // Copied from https://github.com/microsoft/reverse-proxy/issues/1473#issue-1085036615 
    public override async ValueTask TransformRequestAsync(HttpContext context, HttpRequestMessage proxyRequest, string destinationPrefix)
    {
        var requestBody = "";
        context.Request.Body.Position = 0;
        using (StreamReader sr = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            requestBody = await sr.ReadToEndAsync();
        }

        var replacedContent = requestBody.Replace("%PLACEHOLDER%", "SOME_REAL_KEY");
        var requestContent = new StringContent(replacedContent, Encoding.UTF8, "application/json");
        context.Request.Body.Position = 0;
        context.Request.Body = requestContent.ReadAsStream();
        context.Request.ContentLength = context.Request.Body.Length;

        await base.TransformRequestAsync(context, proxyRequest, destinationPrefix);
        proxyRequest.Headers.Host = null;
    }
}


record TrackRequest(string Key);