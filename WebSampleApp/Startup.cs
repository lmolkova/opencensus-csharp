using System.Collections.Generic;
using System.Threading;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Steeltoe.Management.Census.Impl.Trace.Export.Grpc;
using Steeltoe.Management.Census.Impl.Trace.Listeners;
using Steeltoe.Management.Census.Trace;
using Steeltoe.Management.Census.Trace.Export;

namespace WebSampleApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITraceComponent, TraceComponent>();
            services.AddSingleton<ITracer>(sp =>
            {
                var component = sp.GetRequiredService<ITraceComponent>();
                return component.Tracer;
            });

            services.AddSingleton<IHandler>(new OcdHandler("127.0.0.1:50051", ChannelCredentials.Insecure));

            //var server = LocalForwarderProto.Program.StartServer();
            //services.AddSingleton(server);
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app,
            IHostingEnvironment env,
//            TelemetryConfiguration config,
            ITraceComponent traceComponent,
            IHandler ocd,
            //Server ocdServer,
            IApplicationLifetime applicationLifetime)
        {
            traceComponent.ExportComponent.SpanExporter.RegisterHandler("ocd", ocd);
            var subscriber = new DiagnosticSourceSubscriber(new HashSet<string> { "Microsoft.AspNetCore", "HttpHandlerDiagnosticListener" },
                traceComponent.Tracer);

            subscriber.Subscribe();
//            config.DisableTelemetry = true;
//            TelemetryConfiguration.Active.DisableTelemetry = true;


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();

            applicationLifetime.ApplicationStopping.Register( () =>
            {
                subscriber.Dispose();
                //ocdServer.ShutdownAsync().Wait();
            });
        }
    }
}
