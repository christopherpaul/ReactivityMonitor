using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ReactivityProfiler.Support.Server
{
    internal class Launcher
    {
        public int Port { get; set; } = 53551; // arbitrary

        public void Launch()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            var serverThread = new Thread(CreateAndRun);
            serverThread.IsBackground = true;
            serverThread.Name = GetType().Namespace;
            serverThread.Start();
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            Trace.WriteLine($"AssemblyResolve: {args.Name}");
            var name = new AssemblyName(args.Name);

            string path =
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    name.Name + ".dll");
            return Assembly.LoadFrom(path);
        }
         
        private void CreateAndRun()
        {
            try
            {
                IWebHost host = CreateWebHostBuilder().Build();
                host.Start();
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0}: server execution failed: {1}", GetType().FullName, ex);
            }
        }

        private IWebHostBuilder CreateWebHostBuilder()
        {
            return WebHost.CreateDefaultBuilder()
                .ConfigureLogging(ConfigureLogging)
                .UseKestrel(ConfigureKestrel)
                .ConfigureServices(ConfigureServices)
                .UseStartup<Startup>();
        }

        private void ConfigureLogging(ILoggingBuilder builder)
        {
            builder.ClearProviders();
            builder.AddProvider(new LoggerProvider());
        }

        private void ConfigureKestrel(KestrelServerOptions options)
        {
            options.ListenLocalhost(Port);
        }

        private void ConfigureServices(IServiceCollection services)
        {
        }

        class LoggerProvider : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName)
            {
                return new Logger(categoryName);
            }

            public void Dispose()
            {
            }

            class Logger : ILogger
            {
                private readonly string mCategoryName;

                public Logger(string categoryName)
                {
                    mCategoryName = categoryName;
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    return new Disp();
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return true;
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    Trace.WriteLine($"{mCategoryName}:{logLevel}:{eventId}:{formatter(state, exception)}");
                    if (exception != null)
                    {
                        Trace.TraceError("{0}", exception);
                    }
                }

                class Disp : IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
        }
    }
}
