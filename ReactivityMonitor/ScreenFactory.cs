using ReactivityMonitor.Connection;
using ReactivityMonitor.Screens;
using ReactivityMonitor.Screens.ConnectionScreen;
using ReactivityMonitor.Screens.HomeScreen;
using ReactivityMonitor.Screens.MonitoringScreen;
using ReactivityMonitor.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor
{
    internal sealed class ScreenFactory : Factory, IScreenFactory
    {
        public ScreenFactory(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public IConnectionScreen CreateConnectionScreen() => GetInstance<IConnectionScreen>();

        public IHomeScreen CreateHomeScreen(IWorkspace workspace)
        {
            var homeScreen = GetInstance<IHomeScreen>();
            homeScreen.Workspace = workspace;

            return homeScreen;
        }

        public IWorkspaceDocumentScreen CreateDocumentScreen(IWorkspaceDocument document)
        {
            switch (document)
            {
                case IMonitoringConfiguration monitoringConfiguration:
                    return CreateSpecifiedDocumentScreen(monitoringConfiguration);

                case IEventsDocument eventsDocument:
                    return CreateSpecifiedDocumentScreen(eventsDocument);

                case null:
                    throw new ArgumentNullException(nameof(document));

                default:
                    throw new ArgumentException($"Unsupported document type {document.GetType()}");
            }
        }

        private IWorkspaceDocumentScreen CreateSpecifiedDocumentScreen<TDocument>(TDocument document)
            where TDocument : class, IWorkspaceDocument
        {
            var builder = GetInstance<IWorkspaceDocumentScreenBuilder<TDocument>>();
            builder.SetDocument(document);
            return builder;
        }
    }
}
