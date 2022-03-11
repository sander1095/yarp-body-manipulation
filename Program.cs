using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;
using YarpBodyManipulation.Transforms;


// do a POST to /track with the following request body:

//{
//    "Key": "%PLACEHOLDER%"
//}


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builder =>
    {
        if (builder.Route.RouteId == "trackRoute")
        {
            builder.AddReplaceRequestBodyTransform();           
        }
    });

var app = builder.Build();

app.UseHttpsRedirection();

app.MapPost("/track-done", ([FromBody] TrackRequest request) =>
{
    return request.Key;
});

app.MapReverseProxy();

app.Run();

record TrackRequest(string Key);