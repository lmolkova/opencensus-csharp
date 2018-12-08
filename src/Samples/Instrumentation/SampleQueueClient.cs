using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenCensus.Trace;
using OpenCensus.Trace.Propagation;
using OpenCensus.Trace.Sampler;

namespace Samples.Instrumentation
{
    public class SampleQueueClient
    {
        private readonly IAttributeValue<string> endpoint;
        private readonly ITracer tracer;
        private readonly ISampler sampler;
        private readonly ITextFormat propagatorFormat;
        private readonly ConcurrentQueue<Message> fakeService = new ConcurrentQueue<Message>();

        private static readonly Action<Dictionary<string, string>, string, string> TraceContextSetter =
            (headers, key, value) => headers[key] = value;

        private static readonly Func<Dictionary<string, string>, string, IEnumerable<string>> TraceContextGetter =
            (headers, key) => new []{headers[key]};

        public SampleQueueClient(string endpoint)
        {
            // Cache endpoint attribute.
            // Assuming client calls backend service, this is
            // service endpoint that includes user's account and/or queue name
            // Storage request endpoint (URI) is a good  candidate. 
            this.endpoint = AttributeValue.StringAttributeValue(endpoint);

            this.tracer = Tracing.Tracer;
            this.sampler = Samplers.AlwaysSample; //TODO!
            this.propagatorFormat = Tracing.PropagationComponent.TextFormat;

            // initialization
        }

        public async Task SendMessagesAsync(List<Message> messages)
        {
            // First, start the scoped span
            // Span name follows 'component.operation name' pattern.
            using (this.tracer.SpanBuilder("SampleQueue.Send")
                // Sampling could be configurable on per-library, per-span basis, so let's set sample 
                .SetSampler(this.sampler)
                .StartScopedSpan())
            {
                // Span is stored in AsyncLocal and we can always get current one:
                var currentSpan = this.tracer.CurrentSpan;

                // check if span is sampled, if not - this is Noop span
                bool isSampled = (currentSpan.Options & SpanOptions.RecordEvents) != 0;

                // Let's augment sampled spans only
                if (isSampled)
                {
                    currentSpan.Kind = SpanKind.Client;
                    currentSpan.PutAttribute("endpoint", endpoint);

                    foreach (var msg in messages)
                    {
                        this.propagatorFormat.Inject(
                            msg.SpanContext, 
                            msg.Headers, 
                            TraceContextSetter);
                        currentSpan.AddLink(Link.FromSpanContext(msg.SpanContext, LinkType.ParentLinkedSpan));
                    }
                }

                try
                {
                    // Get the data
                    await this.SendInternalWithRetriesAsync(messages).ConfigureAwait(false);

                    if (isSampled)
                    {
                        currentSpan.Status = Status.Ok;
                    }
                }
                catch (Exception ex)
                {
                    if (isSampled)
                    {
                        // failed, let's fill the status
                        currentSpan.Status = ex.ToStatus();
                    }

                    throw;
                }
            }
        }

        public async Task<List<Message>> ReceiveMessagesAsync(int count)
        {
            // First, start the scoped span
            // Span name follows 'component.operation name' pattern.
            using (this.tracer.SpanBuilder("SampleQueue.Receive")
                // Sampling could be configurable on per-library, per-span basis, so let's set sample 
                .SetSampler(this.sampler)
                .StartScopedSpan())
            {
                // Span is stored in AsyncLocal and we can always get current one:
                var currentSpan = this.tracer.CurrentSpan;

                // check if span is sampled, if not - this is Noop span
                bool isSampled = (currentSpan.Options & SpanOptions.RecordEvents) != 0;

                try
                {
                    var messages = await ReceiveInternalWithRetries(count).ConfigureAwait(false);

                    if (isSampled)
                    {
                        foreach (var msg in messages)
                        {
                            var msgContext = this.propagatorFormat.Extract(
                                msg.Headers,
                                TraceContextGetter);
                            currentSpan.AddLink(Link.FromSpanContext(msgContext, LinkType.ParentLinkedSpan)); //TODO link types
                        }
                        currentSpan.Status = Status.Ok;
                    }

                    return messages;
                }
                catch (Exception ex)
                {
                    if (isSampled)
                    {
                        // failed, let's fill the status
                        currentSpan.Status = ex.ToStatus();
                    }

                    throw;
                }
            }
        }

        public void RegisterHandler(Func<Message, Task> process)
        {
            Process(process);
        }


        private Task SendInternalWithRetriesAsync(IEnumerable<Message> messages)
        {
            foreach (var msg in messages)
            {
                this.fakeService.Enqueue(msg);
            }

            return Task.CompletedTask;
        }

        private async Task<List<Message>> ReceiveInternalWithRetries(int count)
        {
            var messages = new Message[count];

            int current = 0;
            while (current < count)
            {
                if (this.fakeService.TryDequeue(out var msg))
                {
                    messages[current] = msg;
                    current++;
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            return messages.ToList();
        }

        private Task Process(Func<Message, Task> handler)
        {
            return Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    if (this.fakeService.TryDequeue(out var msg))
                    {
                        using (var span = this.tracer
                            .SpanBuilderWithRemoteParent("SampleQueue.Process", msg.SpanContext)
                            .SetSampler(this.sampler).StartScopedSpan())
                        {
                            // todo!
                            await handler(msg).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }
    }

    public class Message
    {
        public string Content { get;  private set; }

        public Dictionary<string, string> Headers { get; private set; } = new Dictionary<string, string>();

        private static readonly ITracer Tracer = Tracing.Tracer;

        internal ISpanContext SpanContext { get; private set; }

        public Message(string content)
        {
            using (Tracer.SpanBuilder("SampleQueue.Message")
                .StartScopedSpan())
            {
                Content = content;

                var currentSpan = Tracer.CurrentSpan;
                if ((currentSpan.Options & SpanOptions.RecordEvents) != 0)
                {
                    SpanContext = currentSpan.Context;
                }
            }
        }
    }
}
