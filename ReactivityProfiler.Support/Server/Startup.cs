using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ReactivityProfiler.Support.Server
{
    internal sealed class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Map("/instrumentation", ConfigureInstrumentation);
            app.Map("/subscriptions", ConfigureSubscriptions);
            app.Map("/observable", ConfigureObservable);

            app.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });
        }

        private void ConfigureInstrumentation(IApplicationBuilder app)
        {
            app.Run(async (context) =>
            {
                if (!string.Equals(context.Request.Method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                    return;
                }

                context.Response.ContentType = System.Net.Mime.MediaTypeNames.Text.Plain;

                var instrStore = Store.Stores.Instrumentation;
                int eventCount = instrStore.GetEventCount();
                for (int i = 0; i < eventCount; i++)
                {
                    Trace.WriteLine($"Event {i}");
                    object e = instrStore.GetEvent(i);
                    Trace.WriteLine($"Event type {e.GetType()}");
                    switch (e)
                    {
                        case Store.ModuleLoadEvent mle:
                            Trace.WriteLine($"Writing module load");
                            await context.Response.WriteAsync($"ModuleLoad: {mle.ModuleId:x8} from {mle.ModulePath}\r\n");
                            break;

                        case Store.MethodCallInstrumentedEvent mcie:
                            Trace.WriteLine($"Writing method call instr");
                            await context.Response.WriteAsync($"MethodCallInstr ID={mcie.InstrumentationPointId}: {mcie.ModuleId:x8}:{mcie.FunctionToken:x4}:IL_{mcie.InstructionOffset:x4} calling {mcie.CalledMethodName}\r\n");
                            break;
                    }
                }
            });
        }

        private void ConfigureSubscriptions(IApplicationBuilder app)
        {
            app.Run(async (context) =>
            {
                if (!string.Equals(context.Request.Method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                    return;
                }

                foreach (var sub in Store.Stores.Subscriptions.GetAllSubs())
                {
                    await context.Response.WriteAsync($"{sub.Details.Timestamp.Ticks}:{sub.Details.ThreadId}:{sub.Observable.InstrumentationPoint}:{sub.Observable.ObservableId}:{sub.SubscriptionId}\r\n");
                }
            });
        }

        private void ConfigureObservable(IApplicationBuilder app)
        {
            app.Run(async (context) =>
            {
                if (!string.Equals(context.Request.Method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                    return;
                }

                if (!long.TryParse(context.Request.Path.Value.Trim('/'), out long subId))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync($"Bad id, path = {context.Request.Path}");
                    return;
                }

                var sub = Store.Stores.Subscriptions.GetSub(subId);
                if (sub == null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsync($"Sub not found, ID = {subId}");
                    return;
                }

                var obs = sub.Observable;

                await context.Response.WriteAsync($"{obs.InstrumentationPoint}:{obs.ObservableId}\r\n");
                foreach (var input in obs.Inputs)
                {
                    await context.Response.WriteAsync($"<<{input.InstrumentationPoint}:{input.ObservableId}\r\n");
                }
            });
        }
    }
}
