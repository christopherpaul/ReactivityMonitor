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
            var methods = new ObservableCollectionExtended<Item>();
            Methods = new ReadOnlyObservableCollection<Item>(methods);

            var whenUserCancels = CommandHelper.CreateTriggerCommand(out var cancelCommand);
            CancelCommand = cancelCommand;
            var whenUserAccepts = CommandHelper.CreateTriggerCommand(out var acceptCommand);
            AcceptCommand = acceptCommand;

            mSelectedMethodHelper = this.WhenAnyValue(x => x.SelectedItem)
                .Select(item => item as MethodItem)
                .ToProperty(this, nameof(SelectedMethod));
            mSelectedTypeHelper = this.WhenAnyValue(x => x.SelectedItem)
                .Select(item => item as TypeItem)
                .ToProperty(this, nameof(SelectedType));
            mAcceptCommandTextHelper = this.WhenAnyValue(x => x.SelectedItem)
                .Select(item => item is TypeItem ? "See methods" : "Add")
                .ToProperty(this, nameof(AcceptCommandText));

            this.WhenActivated((CompositeDisposable disposables) =>
            {
                SearchString = string.Empty;
                SelectedItem = null;
                ChosenType = null;
                methods.Clear();

                var whenSearchStringChanges = this.WhenAnyValue(x => x.SearchString)
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler);

                var whenChosenTypeChanges = this.WhenAnyValue(x => x.ChosenType)
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler);

                whenSearchStringChanges
                    .CombineLatest(whenChosenTypeChanges, (searchString, chosenType) => (searchString, chosenType))
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .Select(inputs => MakeScorers(inputs.searchString, inputs.chosenType))
                    .Select(scorers =>
                        Model.Modules
                            .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                            .SelectMany(m => m.InstrumentedMethods)
                            .Publish(ms =>
                            {
                                var methodItems = ms
                                    .Select(m => m.SourceMethod)
                                    .Distinct()
                                    .Select(m => (method: m, score: scorers.scoreMethod(m)))
                                    .Where(m => m.score.HasValue)
                                    .Select(m => (Item)new MethodItem(m.method, m.score.Value, scorers.scorer));

                                var typeItems = ms
                                    .Select(m => m.SourceMethod.ParentType)
                                    .Distinct()
                                    .Select(t => (type: t, score: scorers.scoreType(t)))
                                    .Where(t => t.score.HasValue)
                                    .Select(t => (Item)new TypeItem(t.type, t.score.Value, scorers.scorer));

                                return methodItems.Merge(typeItems);
                            })
                            .ToObservableChangeSet()
                            .Sort(Utility.Comparer<Item>.ByKey(item => -item.Score)))
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
                            
                (Func<ISourceMethod, int?> scoreMethod, Func<string, int?> scoreType, IMatchScorer scorer) MakeScorers(string s, TypeItem chosenType)
                {
                    var scorer = MatchScorerFactory.Default.Create(s);

                    if (chosenType != null)
                    {
                        string typeToMatch = chosenType.FullTypeName;
                        if (string.IsNullOrWhiteSpace(s))
                        {
                            return (m => m.ParentType == typeToMatch ? (int?)0 : null, Funcs<string>.DefaultOf<int?>(), scorer);
                        }

                        return (m => m.ParentType == typeToMatch ? GetScoreOrNull(m.Name) : null, Funcs<string>.DefaultOf<int?>(), scorer);
                    }

                    if (string.IsNullOrWhiteSpace(s))
                    {
                        return (Funcs<ISourceMethod>.DefaultOf<int?>(), Funcs<string>.DefaultOf<int?>(), scorer);
                    }

                    return (m => GetScoreOrNull(m.Name), GetScoreOrNull, scorer);

                    int? GetScoreOrNull(string n)
                    {
                        int score = scorer.Score(n);
                        if (score != int.MinValue)
                        {
                            return score;
                        }

                        return null;
                    };
                }

                whenUserCancels.Subscribe(_ => Cancel())
                    .DisposeWith(disposables);

                Methods.ToObservableChangeSet()
                    .ObserveOn(concurrencyService.TaskPoolRxScheduler)
                    .Top(1)
                    .OnItemAdded(firstItem => SelectedItem = firstItem)
                    .Subscribe()
                    .DisposeWith(disposables);

                this.WhenAnyValue(x => x.SelectedItem)
                    .ObserveOn(concurrencyService.DispatcherRxScheduler)
                    .Select(item =>
                    {
                        if (item is MethodItem m)
                        {
                            return whenUserAccepts.Do(_ => Proceed(m.Method));
                        }

                        if (item is TypeItem t)
                        {
                            return whenUserAccepts.Do(_ =>
                            {
                                ChosenType = t;
                                SearchString = string.Empty;
                            });
                        }

                        return Observable.Empty<Unit>();
                    })
                    .Switch()
                    .Subscribe()
                    .DisposeWith(disposables);
            });
        }

        public IReactivityModel Model { get; set; }
        public Action<ISourceMethod> Proceed { get; set; }
        public Action Cancel { get; set; }

        public ICommand CancelCommand { get; }
        public ICommand AcceptCommand { get; }

        private readonly ObservableAsPropertyHelper<string> mAcceptCommandTextHelper;
        public string AcceptCommandText => mAcceptCommandTextHelper.Value;

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

        public string NoItemsPlaceholderText => SearchStringIsEmpty ? string.Empty : "No matching types or methods found";

        public ReadOnlyObservableCollection<Item> Methods { get; }

        private Item mSelectedItem;
        public Item SelectedItem
        {
            get => mSelectedItem;
            set => this.RaiseAndSetIfChanged(ref mSelectedItem, value);
        }

        private ObservableAsPropertyHelper<MethodItem> mSelectedMethodHelper;
        public MethodItem SelectedMethod => mSelectedMethodHelper.Value;

        private ObservableAsPropertyHelper<TypeItem> mSelectedTypeHelper;
        public TypeItem SelectedType => mSelectedTypeHelper.Value;

        private TypeItem mChosenType;
        public TypeItem ChosenType
        {
            get => mChosenType;
            set => this.RaiseAndSetIfChanged(ref mChosenType, value);
        }

        public abstract class Item
        {
            private readonly Lazy<(string text, IEnumerable<int> positions)> mMatchPositionsInfo;

            protected Item(string matchText, IMatchScorer scorer)
            {
                mMatchPositionsInfo = new Lazy<(string, IEnumerable<int>)>(() =>
                {
                    string s = matchText;
                    var positions = scorer.GetMatchPositions(ref s);
                    return (s, positions);
                });
            }

            public abstract int Score { get; }

            public string MatchText => mMatchPositionsInfo.Value.text;
            public IEnumerable<int> MatchPositions => mMatchPositionsInfo.Value.positions;
        }

        public sealed class TypeItem : Item
        {
            public TypeItem(string type, int score, IMatchScorer scorer) : base(type, scorer)
            {
                Score = score;
                string[] parts = type.Split('.');
                Name = parts[parts.Length - 1];
                Namespace = string.Join(".", parts.Take(parts.Length - 1));
                FullTypeName = type;
            }

            public string FullTypeName { get; }

            public string Name { get; }
            public string Namespace { get; }

            public override int Score { get; }
        }

        public sealed class MethodItem : Item
        {
            private readonly ISourceMethod mMethod;

            public MethodItem(ISourceMethod method, int score, IMatchScorer scorer)
                : base(method.Name, scorer)
            {
                mMethod = method;
                Score = score;
                var typeParts = mMethod.ParentType.Split('.');
                TypeName = typeParts.LastOrDefault() ?? string.Empty;
                Namespace = string.Join(".", typeParts.Take(typeParts.Length - 1));
            }

            public ISourceMethod Method => mMethod;

            public string MethodName => mMethod.Name;
            public string TypeName { get; }
            public string TypeAndName => $"{TypeName}.{MethodName}";
            public string FullTypeName => mMethod.ParentType;
            public string Namespace { get; }
            public override int Score { get; }
        }
    }
}
