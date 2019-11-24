﻿using System;
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

                var instrData = Store.Stores.Instrumentation.GetData();
                context.Response.ContentType = System.Net.Mime.MediaTypeNames.Application.Octet;
                context.Response.ContentLength = instrData.Length;
                await context.Response.Body.WriteAsync(instrData, 0, instrData.Length);
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
    }
}
