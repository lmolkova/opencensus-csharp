using System.Collections.Generic;
using Steeltoe.Management.Census.Trace;

namespace Steeltoe.Management.Census.Impl.Trace.Listeners
{
    internal static class ListenerHandlerFactory
    {
        private static readonly Dictionary<string, ListenerHandler> KnownHandlers =
            new Dictionary<string, ListenerHandler>();

        // todo: pluggable
        public static ListenerHandler GetHandler(string name, ITracer tracer)
        {
            if (!KnownHandlers.TryGetValue(name, out var handler))
            {
                switch (name)
                {
                    case "HttpHandlerDiagnosticListener":
                        handler = new HttpOutListener(tracer);
                        break;
                    case "Microsoft.AspNetCore":
                        handler = new HttpInListener(tracer);
                        break;
                    default:
                        handler = new ListenerHandler(name, tracer);
                        break;
                }
                KnownHandlers[name] = handler;
            }
            return handler;
        }
    }
}