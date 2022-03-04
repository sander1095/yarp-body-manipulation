using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using Yarp.ReverseProxy.Forwarder;


// The point of this code sample is that I can't figure out how to change the request body with YARP.
// If I actually change something YARP throws an exception.

// do a POST to /track with the following request body:

//{
//    "Key": "%PLACEHOLDER%"
//}

// and you will see everything works.
// Now change the "newValue" on line ~102 (change %PLACEHOLDER% to "HELLO" or something) and you will see that YARP throws an exception.
// How do I do this?

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
        string? requestBody = "";
        context.Request.Body.Position = 0;
        using (StreamReader sr = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            requestBody = await sr.ReadToEndAsync();
        }

        // If you change the newValue with something that is a different length than the original, YARP will throw the following exception:
        /*
         * info: Yarp.ReverseProxy.Forwarder.HttpForwarder[48]
           Request: An error was encountered before receiving a response.
           System.Net.Http.HttpRequestException: An error occurred while sending the request.
            ---> System.IO.IOException: The request was aborted.
            ---> System.Net.Http.Http2StreamException: The HTTP/2 server reset the stream. HTTP/2 error code 'PROTOCOL_ERROR' (0x1).
            --- End of inner exception stack trace ---
            at System.Net.Http.Http2Connection.ThrowRequestAborted(Exception innerException)
            at System.Net.Http.Http2Connection.Http2Stream.CheckResponseBodyState()
            at System.Net.Http.Http2Connection.Http2Stream.TryEnsureHeaders()
            at System.Net.Http.Http2Connection.Http2Stream.ReadResponseHeadersAsync(CancellationToken cancellationToken)
            at System.Net.Http.Http2Connection.SendAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
            --- End of inner exception stack trace ---
            at System.Net.Http.Http2Connection.SendAsync(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
            at System.Net.Http.HttpConnectionPool.SendWithVersionDetectionAndRetryAsync(HttpRequestMessage request, Boolean async, Boolean doRequestAuth, CancellationToken cancellationToken)
            at System.Net.Http.DiagnosticsHandler.SendAsyncCore(HttpRequestMessage request, Boolean async, CancellationToken cancellationToken)
            at Yarp.ReverseProxy.Forwarder.HttpForwarder.SendAsync(HttpContext context, String destinationPrefix, HttpMessageInvoker httpClient, ForwarderRequestConfig requestConfig, HttpTransformer transformer)
         * 
         */
        var replacedContent = requestBody.Replace("%PLACEHOLDER%", "%PLACEHOLDER%");
        var requestContent = new StringContent(replacedContent, Encoding.UTF8, "application/json");
        await using var newBody = await requestContent.ReadAsStreamAsync();
        context.Request.Body.Position = 0;
        context.Request.Body = newBody;
        context.Request.ContentLength = context.Request.Body.Length;

        await base.TransformRequestAsync(context, proxyRequest, destinationPrefix);
        proxyRequest.Headers.Host = null;
    }
}


record TrackRequest(string Key);