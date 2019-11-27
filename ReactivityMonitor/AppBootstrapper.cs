namespace ReactivityMonitor
{
    using System;
    using System.Collections.Generic;
    using Caliburn.Micro;
    using ReactivityMonitor.Infrastructure;
    using ReactivityMonitor.Screens.ConnectionScreen;
    using ReactivityMonitor.Screens.HomeScreen;
    using ReactivityMonitor.Services;

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

        protected override void Configure()
        {
            mContainer = new SimpleContainer();

            // Framework services
            mContainer.Singleton<IWindowManager, WindowManager>();
            mContainer.Singleton<IEventAggregator, EventAggregator>();

            // Application services
            mContainer.Singleton<IConnectionService, ConnectionService>();

            // Screens etc.
            mContainer.PerRequest<IShell, ShellViewModel>();
            mContainer.PerRequest<IConnectionScreen, ConnectionScreenViewModel>();
            mContainer.PerRequest<IHomeScreen, HomeScreenViewModel>();
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

        protected override void OnStartup(object sender, System.Windows.StartupEventArgs e)
        {
            DisplayRootViewFor<IShell>();
        }
    }
}