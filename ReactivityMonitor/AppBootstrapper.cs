namespace ReactivityMonitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows.Controls;
    using System.Windows.Markup;
    using System.Xml;
    using Caliburn.Micro;
    using ReactivityMonitor.Infrastructure;
    using ReactivityMonitor.Screens.CallsScreen;
    using ReactivityMonitor.Screens.ConnectionScreen;
    using ReactivityMonitor.Screens.HomeScreen;
    using ReactivityMonitor.Screens.MonitoringScreen;
    using ReactivityMonitor.Services;
    using ReactivityMonitor.Workspace;

    public class AppBootstrapper : BootstrapperBase
    {
        static AppBootstrapper()
        {
            LogManager.GetLog = _ => new TraceLogger();
        }

        private SimpleContainer mContainer;

        public AppBootstrapper()
        {
            Initialize();
        }

        protected override void OnStartup(object sender, System.Windows.StartupEventArgs e)
        {
            //// This code can be used to extract the default style for a WPF control
            //var control = Application.FindResource(typeof(ItemsControl));
            //var sw = new StringWriter();
            //using (XmlTextWriter writer = new XmlTextWriter(sw))
            //{
            //    writer.Formatting = Formatting.Indented;
            //    XamlWriter.Save(control, writer);
            //}

            //string s = sw.ToString();

            DisplayRootViewFor<IShell>();
        }

        protected override void Configure()
        {
            mContainer = new SimpleContainer();
            mContainer.Instance<IServiceProvider>(new ServiceProvider(mContainer));

            // Framework services
            mContainer.Singleton<IWindowManager, WindowManager>();
            mContainer.Singleton<IEventAggregator, EventAggregator>();

            // Application services
            mContainer.Singleton<IConnectionService, ConnectionService>();
            mContainer.Singleton<IConcurrencyService, ConcurrencyService>();
            mContainer.Singleton<IDialogService, DialogService>();

            // Units of work
            mContainer.PerRequest<IWorkspace, Workspace.Workspace>();

            // Screens etc.
            mContainer.Singleton<IScreenFactory, ScreenFactory>();
            mContainer.PerRequest<IShell, ShellViewModel>();
            mContainer.PerRequest<IConnectionScreen, ConnectionScreenViewModel>();
            mContainer.PerRequest<IHomeScreen, HomeScreenViewModel>();
            mContainer.PerRequest<ICallsScreen, CallsScreenViewModel>();
            mContainer.PerRequest<IMonitoringScreen, MonitoringScreenViewModel>();
        }

        protected override object GetInstance(Type service, string key)
        {
            return mContainer.GetInstance(service, key);
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return mContainer.GetAllInstances(service);
        }

        protected override void BuildUp(object instance)
        {
            mContainer.BuildUp(instance);
        }

        private sealed class ServiceProvider : IServiceProvider
        {
            private readonly SimpleContainer mContainer;

            public ServiceProvider(SimpleContainer container)
            {
                mContainer = container;
            }

            public object GetService(Type type) => mContainer.GetInstance(type, null);
        }
    }
}