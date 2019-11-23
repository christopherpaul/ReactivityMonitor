using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProfilee
{
    internal class GenericExamples
    {
        public void CallToMethodOnGenericType(List<IObservable<string>> observables)
        {
            observables.Find(_ => true).Subscribe(Console.WriteLine);
        }
    }
}
