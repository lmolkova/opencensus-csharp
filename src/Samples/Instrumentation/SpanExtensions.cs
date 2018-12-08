using System;
using System.Threading.Tasks;
using OpenCensus.Trace;

namespace Samples.Instrumentation
{
    internal static class SpanExtensions
    {
        /// <summary>
        /// Converts exception to OpenCensus status based on it's type.
        /// </summary>
        /// <param name="e">Exception instance.</param>
        /// <returns>OpenCensus status.</returns>
        public static Status ToStatus(this Exception e)
        {
            switch (e)
            {
                case TimeoutException toe:
                    return Status.DeadlineExceeded.WithDescription(toe.ToString());
                case TaskCanceledException tce:
                    return Status.Cancelled.WithDescription(tce.ToString());
                default:
                    return Status.Unknown.WithDescription(e.ToString());
            }
        }

    }
}
