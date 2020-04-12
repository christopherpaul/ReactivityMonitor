using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactivityMonitor.Infrastructure;
using ReactivityMonitor.Model;
using ReactivityMonitor.Services;
using ReactivityMonitor.Utility.Flyweights;
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
using Utility.SmartSearch;

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
                SearchString = string.Empty;
                SelectedMethod = null;
                methods.Clear();

                var whenSearchStringChanges = this.WhenAnyValue(x => x.SearchString)
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler);

                whenSearchStringChanges
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .Select(ScoreMethod)
                    .Select(scorer =>
                        Model.Modules
                            .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                            .SelectMany(m => m.InstrumentedMethods)
                            .Select(m => (method: m, score: scorer(m)))
                            .Where(m => m.score.HasValue)
                            .ToObservableChangeSet(m => m.method.InstrumentedMethodId)
                            .Transform(m => new MethodItem(m.method, m.score.Value))
                            .Sort(Utility.Comparer<MethodItem>.ByKey(item => -item.Score)))
                    .Select(items =>
                        Observable.Defer(() =>
                        {
                            methods.Clear();
                            return items
                                .ObserveOn(concurrencyService.DispatcherRxScheduler)
                                .Bind(methods);
                        }).SubscribeOn(concurrencyService.DispatcherRxScheduler))
                    .Switch()
                    .Subscribe()
                    .DisposeWith(disposables);
                            
                Func<IInstrumentedMethod, int?> ScoreMethod(string s)
                {
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        return Funcs<IInstrumentedMethod>.DefaultOf<int?>();
                    }

                    var scorer = MatchScorerFactory.Default.Create(s);

                    return m =>
                    {
                        int score = scorer.Score(m.Name);
                        if (score != int.MinValue)
                        {
                            return score;
                        }

                        return null;
                    };
                }

                whenUserCancels.Subscribe(_ => Cancel())
                    .DisposeWith(disposables);

                this.WhenAnyValue(x => x.SelectedMethod)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Select(m =>
                    {
                        if (m == null)
                        {
                            return Methods.ToObservableChangeSet()
                                .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                                .Top(1)
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
            set 
            { 
                this.RaiseAndSetIfChanged(ref mSearchString, value);
                SearchStringIsEmpty = string.IsNullOrWhiteSpace(value);
            }
        }

        private bool mSearchStringIsEmpty = true;
        public bool SearchStringIsEmpty
        {
            get => mSearchStringIsEmpty;
            private set
            {
                if (value != mSearchStringIsEmpty)
                {
                    mSearchStringIsEmpty = value;
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(NoItemsPlaceholderText));
                }
            }
        }

        public string NoItemsPlaceholderText => SearchStringIsEmpty ? string.Empty : "No matching methods found";

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

            public MethodItem(IInstrumentedMethod method, int score)
            {
                mMethod = method;
                Score = score;
                var typeParts = mMethod.ParentType.Split('.');
                TypeName = typeParts.LastOrDefault() ?? string.Empty;
                Namespace = string.Join(".", typeParts.Take(typeParts.Length - 1));
            }

            public IInstrumentedMethod Method => mMethod;

            public string MethodName => mMethod.Name;
            public string TypeName { get; }
            public string TypeAndName => $"{TypeName}.{MethodName}";
            public string FullTypeName => mMethod.ParentType;
            public string Namespace { get; }
            public int Score { get; }
        }
    }
}
