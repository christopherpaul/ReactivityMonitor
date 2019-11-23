using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProfilee
{
    internal static class GenericExamples
    {
        public static void CallToMethodOnGenericType()
        {
            GenericType<int>.CreateObservable().Subscribe(n => Console.WriteLine($"{n}"));
        }

        public static class GenericType<T>
        {
            public static IObservable<T> CreateObservable()
            {
                return Observable.Return(default(T));
            }
        }
    }
}
