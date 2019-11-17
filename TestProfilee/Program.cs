using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProfilee
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("I'm really here");
            Console.WriteLine(Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING"));
            Console.WriteLine(Environment.GetEnvironmentVariable("COR_PROFILER"));
            Console.ReadKey();
        }
    }
}
