using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace YarpBodyManipulation.Transforms
{
    public static class ReplaceRequestBodyTransform
    {
        public static void AddReplaceRequestBodyTransform(this TransformBuilderContext context)
        {
            context.AddRequestTransform(async context =>
            {
                var httpContext = context.HttpContext;

                string? requestBody = "";

                using (StreamReader sr = new StreamReader(httpContext.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
                {
                    requestBody = await sr.ReadToEndAsync();
                }

                var replacedContent = requestBody.Replace("%PLACEHOLDER%", "Hello");
                var requestContent = new StringContent(replacedContent, Encoding.UTF8, "application/json");


                httpContext.Request.Body = requestContent.ReadAsStream();
                context.ProxyRequest.Content!.Headers.ContentLength = httpContext.Request.Body.Length;
            });
        }
    }
}
