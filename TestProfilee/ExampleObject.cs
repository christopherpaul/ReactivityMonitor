using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProfilee
{
    public class ExampleObject
    {
        public ExampleObject(string str, int num)
        {
            Str = str;
            Num = num;
        }

        public string Str { get; }
        public int Num { get; }

        public override string ToString()
        {
            return $"{Num}: {Str}";
        }
    }
}
