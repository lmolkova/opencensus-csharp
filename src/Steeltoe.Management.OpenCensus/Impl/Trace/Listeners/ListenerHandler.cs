using System;
using System.Diagnostics;
using System.Threading;
using Steeltoe.Management.Census.Common;
using Steeltoe.Management.Census.Trace;
using Steeltoe.Management.Census.Trace.Sampler;

namespace Steeltoe.Management.Census.Impl.Trace.Listeners
{
    internal class ListenerHandler
    {
        public string SourceName { get; }
        protected readonly ITracer Tracer;
        // TODO: fix IScope and AsyncLocalContext so that current is always available
        protected readonly AsyncLocal<IScope> LocalScope = new AsyncLocal<IScope>();

        public ListenerHandler(string sourceName, ITracer tracer)
        {
            SourceName = sourceName;
            Tracer = tracer;
        }

        public virtual void OnStartActivity(Activity activity, object payload)
        {
            LocalScope.Value = Tracer.SpanBuilder(activity.OperationName).SetRecordEvents(true).SetSampler(Samplers.AlwaysSample).StartScopedSpan();
        }

        public virtual void OnStopActivity(Activity activity, object payload)
        {
            var span = Tracer.CurrentSpan;
            foreach (var tag in activity.Tags)
            {
                span.PutAttribute(tag.Key, AttributeValue.StringAttributeValue(tag.Value));
            }

            LocalScope.Value?.Dispose();
        }
    }
}
