namespace ReactivityMonitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Markup;
    using System.Xml;
    using Caliburn.Micro;
    using ReactivityMonitor.Infrastructure;
    using ReactivityMonitor.Screens.CallsScreen;
    using ReactivityMonitor.Screens.ConnectionScreen;
    using ReactivityMonitor.Screens.EventListScreen;
    using ReactivityMonitor.Screens.HomeScreen;
    using ReactivityMonitor.Screens.MarbleDiagramScreen;
    using ReactivityMonitor.Screens.MonitoringScreen;
    using ReactivityMonitor.Screens.ObservablesScreen;
    using ReactivityMonitor.Screens.PayloadScreen;
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

            GenerateViewModelDataTemplates();

            DisplayRootViewFor<IShell>();
        }

        protected override void Configure()
        {
            mContainer = new SimpleContainer();
            mContainer.Instance<IServiceProvider>(new ServiceProvider(mContainer));

            // Framework services
            mContainer.Singleton<IWindowManager, WindowManagerEx>();
            mContainer.Singleton<IEventAggregator, EventAggregator>();

            // Application services
            mContainer.Singleton<IConnectionService, ConnectionService>();
            mContainer.Singleton<IConcurrencyService, ConcurrencyService>();
            mContainer.Singleton<IDialogService, DialogService>();
            mContainer.Singleton<ICommandHandlerService, CommandHandlerService>();
            mContainer.Singleton<ISelectionService, SelectionService>();

            // Units of work
            mContainer.PerRequest<IWorkspace, Workspace.Workspace>();

            // Screens etc.
            mContainer.Singleton<IScreenFactory, ScreenFactory>();
            mContainer.PerRequest<IShell, ShellViewModel>();
            mContainer.PerRequest<IConnectionScreen, ConnectionScreenViewModel>();
            mContainer.PerRequest<IHomeScreen, HomeScreenViewModel>();
            mContainer.PerRequest<ICallsScreen, CallsScreenViewModel>();
            mContainer.PerRequest<IMonitoringScreen, MonitoringScreenViewModel>();
            mContainer.PerRequest<IEventListScreen, EventListScreenViewModel>();
            mContainer.PerRequest<IMarbleDiagramScreen, MarbleDiagramScreenViewModel>();
            mContainer.PerRequest<IObservablesScreen, ObservablesScreenViewModel>();
            mContainer.Singleton<IObservablesScreenItemFactory, ObservablesScreenItemFactory>();
            mContainer.PerRequest<ObservablesListItem>();
            mContainer.PerRequest<IPayloadScreen, PayloadScreenViewModel>();
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

        private void GenerateViewModelDataTemplates()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var viewModelAndViews = assembly.DefinedTypes
                .Where(type => type.Name.EndsWith("ViewModel", StringComparison.Ordinal))
                .Where(type => type.IsClass)
                .Select(type => new { ViewModelType = type, ViewType = assembly.GetType(type.FullName.Remove(type.FullName.Length - 5)) })
                .Where(x => x.ViewType != null && x.ViewType.IsSubclassOf(typeof(FrameworkElement)));

            foreach (var x in viewModelAndViews)
            {
                var dataTemplate = new DataTemplate
                {
                    DataType = x.ViewModelType,
                    VisualTree = new FrameworkElementFactory(x.ViewType)
                };

                Application.Resources.Add(dataTemplate.DataTemplateKey, dataTemplate);
            }
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