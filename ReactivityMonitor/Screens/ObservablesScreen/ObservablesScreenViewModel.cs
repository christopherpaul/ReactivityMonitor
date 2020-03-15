using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Screens.MarbleDiagramScreen;
using ReactivityMonitor.Services;
using ReactivityMonitor.Utility.Extensions;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    public sealed class ObservablesScreenViewModel : ReactiveViewModel, IObservablesScreen
    {
        public ObservablesScreenViewModel(IObservablesList observablesList, IMarbleDiagramScreen marbleDiagram)
        {
            ObservablesList = observablesList;
            DetailContent = marbleDiagram;

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                marbleDiagram.ObservableInstances = observablesList.WhenSelectionChanges;

                observablesList.Activator.Activate().DisposeWith(disposables);
                marbleDiagram.Activator.Activate().DisposeWith(disposables);
            });
        }

        public IObservable<IChangeSet<IObservableInstance, long>> Observables { get; set; }

        public IObservablesList ObservablesList { get; }

        public object DetailContent { get; }
    }
}
