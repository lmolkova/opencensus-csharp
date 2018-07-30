using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using LocalForwarderProto.grpc;
using Microsoft.ApplicationInsights.Extensibility;
using Opencensus.Proto.Exporter;

namespace LocalForwarderProto
{
    public class Program
    {
        public static Server StartServer()
        {
            TelemetryConfiguration.Active.InstrumentationKey = "3c5adc81-72a1-46e4-b786-f717fdc11e06";
            Server server = new Server
            {
                Services = { Export.BindService(new OcdSpanExporter()) },
                Ports = { new ServerPort("localhost", 50051, ServerCredentials.Insecure) }
            };
            server.Start();

            return server;
        }


        static void Main(string[] args)
        {
            var server = StartServer();
            Console.WriteLine("Greeter server listening on port " + 50051);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}
