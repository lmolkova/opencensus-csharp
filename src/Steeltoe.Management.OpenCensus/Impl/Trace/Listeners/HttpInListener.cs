using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Steeltoe.Management.Census.Trace;
using Steeltoe.Management.Census.Trace.Sampler;

namespace Steeltoe.Management.Census.Impl.Trace.Listeners
{
    //todo: plugin
    internal class HttpInListener : ListenerHandler
    {
        private readonly PropertyFetcher startContextFetcher = new PropertyFetcher("HttpContext");
        private readonly PropertyFetcher stopContextFetcher = new PropertyFetcher("HttpContext");

        public HttpInListener(ITracer tracer) : base("Microsoft.AspNetCore", tracer)
        {
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            var context = this.startContextFetcher.Fetch(payload) as HttpContext;
            if (context == null)
            {
                Debug.WriteLine("context is null");
                return;
            }

            var request = context.Request;
            // TODO; sampling
            LocalScope.Value = Tracer.SpanBuilder("HttpIn").SetRecordEvents(true).SetSampler(Samplers.AlwaysSample).StartScopedSpan();
            var span = Tracer.CurrentSpan;
            span.PutServerSpanKindAttribute();
            span.PutHttpMethodAttribute(request.Method);
            span.PutHttpHostAttribute(request.Host.Value);
            span.PutHttpPathAttribute(request.Path);
            span.PutHttpUrlAttribute($"{request.Scheme}://{request.Host}/{request.Path}{request.QueryString}");
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            var context = this.stopContextFetcher.Fetch(payload) as HttpContext;
            if (context == null)
            {
                Debug.WriteLine("context is null");
                return;
            }

            var response = context.Response;

            var span = Tracer.CurrentSpan;

            //TODO status
            span.Status = new Status(((int)response.StatusCode >= 200 && (int)response.StatusCode < 300) ? CanonicalCode.OK : CanonicalCode.UNKNOWN, response.StatusCode.ToString());
            span.PutHttpStatusCodeAttribute(response.StatusCode);
            LocalScope.Value?.Dispose();
        }
    }
}
