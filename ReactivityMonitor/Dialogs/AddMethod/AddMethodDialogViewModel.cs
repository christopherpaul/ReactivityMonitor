using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ReactivityMonitor.Dialogs.AddMethod
{
    public sealed class AddMethodDialogViewModel : ReactiveViewModel, IAddMethodDialog, Caliburn.Micro.IHaveDisplayName
    {
        public AddMethodDialogViewModel(IConcurrencyService concurrencyService)
        {
            var methods = new ObservableCollectionExtended<MethodItem>();
            Methods = new ReadOnlyObservableCollection<MethodItem>(methods);

            var whenUserCancels = CommandHelper.CreateTriggerCommand(out var cancelCommand);
            CancelCommand = cancelCommand;
            var whenUserProceeds = CommandHelper.CreateTriggerCommand(out var proceedCommand);
            AddSelectedCommand = proceedCommand;

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                var whenSearchStringChanges = this.WhenAnyValue(x => x.SearchString)
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler);

                Model.Modules
                    .SelectMany(m => m.InstrumentedMethods)
                    .ToObservableChangeSet(m => m.InstrumentedMethodId)
                    .Filter(whenSearchStringChanges.Select(FilterMethod))
                    .Transform(m => new MethodItem(m))
                    .SubscribeOn(concurrencyService.TaskPoolRxScheduler)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Bind(methods)
                    .Subscribe()
                    .DisposeWith(disposables);

                Func<IInstrumentedMethod, bool> FilterMethod(string s) => m =>
                    m.Name.IndexOf(s, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    m.ParentType.IndexOf(s, StringComparison.CurrentCultureIgnoreCase) >= 0;

                whenUserCancels.Subscribe(_ => Cancel())
                    .DisposeWith(disposables);

                this.WhenAnyValue(x => x.SelectedMethod)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Select(m =>
                    {
                        if (m == null)
                        {
                            return Methods.ToObservableChangeSet().Top(1)
                                .OnItemAdded(firstMethod => SelectedMethod = firstMethod)
                                .Select(_ => Unit.Default);
                        }

                        return whenUserProceeds.Do(_ => Proceed(m.Method));
                    })
                    .Switch()
                    .Subscribe()
                    .DisposeWith(disposables);
            });
        }

        public IReactivityModel Model { get; set; }
        public Action<IInstrumentedMethod> Proceed { get; set; }
        public Action Cancel { get; set; }

        public ICommand CancelCommand { get; }
        public ICommand AddSelectedCommand { get; }

        public string DisplayName
        {
            get => "Add method to configuration";
            set { }
        }

        private string mSearchString = string.Empty;
        public string SearchString
        {
            get => mSearchString;
            set => this.RaiseAndSetIfChanged(ref mSearchString, value);
        }

        public ReadOnlyObservableCollection<MethodItem> Methods { get; }

        private MethodItem mSelectedMethod;
        public MethodItem SelectedMethod
        {
            get => mSelectedMethod;
            set => this.RaiseAndSetIfChanged(ref mSelectedMethod, value);
        }

        public sealed class MethodItem
        {
            private readonly IInstrumentedMethod mMethod;

            public MethodItem(IInstrumentedMethod method)
            {
                mMethod = method;
                var typeParts = mMethod.ParentType.Split('.');
                TypeName = typeParts.LastOrDefault() ?? string.Empty;
                Namespace = string.Join(".", typeParts.Take(typeParts.Length - 1));
            }

            public IInstrumentedMethod Method => mMethod;

            public string MethodName => mMethod.Name;
            public string TypeName { get; }
            public string TypeAndName => $"{TypeName}.{MethodName}";
            public string Namespace { get; }
        }
    }
}
