using System;
using System.Collections.Generic;
using System.IO;
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

        public NestedObject Nested { get; set; }

        public override string ToString()
        {
            return $"{Num}: {Str}";
        }
    }

    public class NestedObject
    {
        public NestedObject(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Stream Console { get; } = new MemoryStream();
    }
}
