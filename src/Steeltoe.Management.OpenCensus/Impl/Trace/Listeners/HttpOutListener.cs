using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Steeltoe.Management.Census.Trace;
using Steeltoe.Management.Census.Trace.Sampler;

namespace Steeltoe.Management.Census.Impl.Trace.Listeners
{
    internal class HttpOutListener : ListenerHandler
    {
        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");
        private readonly PropertyFetcher stopResponseFetcher = new PropertyFetcher("Response");
        private readonly PropertyFetcher stopRequestStatusFetcher = new PropertyFetcher("RequestTaskStatus");

        public HttpOutListener(ITracer tracer) : base("HttpHandlerDiagnosticListener", tracer)
        {
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            var request = this.startRequestFetcher.Fetch(payload) as HttpRequestMessage;
            if (request == null)
            {
                Debug.WriteLine("request is null");
                return;
            }

            // TODO; sampling
            LocalScope.Value = Tracer.SpanBuilder("HttpOut").SetSampler(Samplers.AlwaysSample).StartScopedSpan();
            var span = Tracer.CurrentSpan;
            span.PutClientSpanKindAttribute();
            span.PutHttpMethodAttribute(request.Method.ToString());
            span.PutHttpHostAttribute(request.RequestUri.Host);
            span.PutHttpPathAttribute(request.RequestUri.AbsolutePath);
            span.PutHttpUrlAttribute(request.RequestUri.ToString());
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            var response = this.stopResponseFetcher.Fetch(payload) as HttpResponseMessage;
            if (response == null)
            {
                Debug.WriteLine("response is null");
                return;
            }

            var requestTaskStatus = this.stopRequestStatusFetcher.Fetch(payload) as TaskStatus?;

            var span = Tracer.CurrentSpan;
            if (requestTaskStatus.HasValue)
            {
                if (requestTaskStatus != TaskStatus.RanToCompletion)
                {
                    span.PutErrorAttribute(requestTaskStatus.ToString());
                }
            }

            //TODO status
            span.Status = new Status(((int)response.StatusCode >= 200 && (int)response.StatusCode < 300) ? CanonicalCode.OK : CanonicalCode.UNKNOWN, response.StatusCode.ToString());
            span.PutHttpStatusCodeAttribute((int)response.StatusCode);
            LocalScope.Value?.Dispose();
        }
    }
}
