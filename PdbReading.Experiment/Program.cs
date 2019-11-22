using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DebugInterfaceAccess;

namespace PdbReading.Experiment
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var dia = new DiaSourceClass();
            dia.loadDataForExe(@"E:\Documents\programming\Repos\ReactivityMonitor\TestProfilee\bin\Debug\net472\TestProfilee.exe", null, null);
            dia.openSession(out IDiaSession session);

            Dump(session.globalScope);

            session.findFile(null, null, 0, out var files);
            foreach (IDiaSourceFile file in files)
            {
                Console.WriteLine(file.fileName);

                foreach (IDiaSymbol compiland in file.compilands)
                {
                    session.findLines(compiland, file, out var lines);

                    foreach (IDiaLineNumber line in lines)
                    {
                        Console.WriteLine($"{line.lineNumber}:{line.columnNumber} {line.relativeVirtualAddress}");
                    }
                }
            }

        }

        private static void Dump(IDiaSymbol symbol, string indent = "")
        {
            Console.WriteLine($"{indent}[{symbol.symTag}] {symbol.name}");

            indent += "  ";
            try
            {
                symbol.findChildren(SymTagEnum.SymTagNull, null, 0, out var children);
                foreach (IDiaSymbol child in children)
                {
                    Dump(child, indent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to enumerate children: {ex.Message}");
            }
        }
    }
}
