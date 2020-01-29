using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProfilee
{
    public interface IInterfaceWithObservableProperty
    {
        IObservable<string> WhenNameChanges { get; }
    }

    public class ObjectWithObservableProperty : IInterfaceWithObservableProperty
    {
        public IObservable<string> WhenNameChanges => Observable.Return("ConstrainedGeneric");
    }

    public static class ConstrainedGenericExample
    {
        public static void Test<T>(T thing) where T : IInterfaceWithObservableProperty
        {
            thing.WhenNameChanges.Subscribe(Console.WriteLine);
        }
    }
}
