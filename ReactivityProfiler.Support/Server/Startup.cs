using System;
using System.Collections.Generic;
using System.Text;
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

            app.Run(async (context) =>
            {
                foreach (var sub in Store.Stores.Subscriptions.GetAllSubs())
                {
                    await context.Response.WriteAsync($"{sub.Details.Timestamp.Ticks}:{sub.Details.ThreadId}:{sub.Observable.InstrumentationPoint}:{sub.Observable.ObservableId}:{sub.SubscriptionId}\r\n");
                }
            });
        }
    }
}
