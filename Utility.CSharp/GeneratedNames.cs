using Microsoft.CodeAnalysis.CSharp.Symbols;
using System;
using System.Collections.Generic;
using System.Text;
using Impl = Microsoft.CodeAnalysis.CSharp.Symbols.GeneratedNames;

namespace Utility.CSharp
{
    public static class GeneratedNames
    {
        public static bool IsGeneratedMemberName(string memberName) => Impl.IsGeneratedMemberName(memberName);
        public static GeneratedNameKind GetKind(string name) => Impl.GetKind(name);
        public static bool TryParseGeneratedName(
            string name,
            out GeneratedNameKind kind,
            out int openBracketOffset,
            out int closeBracketOffset) => Impl.TryParseGeneratedName(name, out kind, out openBracketOffset, out closeBracketOffset);
        public static bool TryParseSourceMethodNameFromGeneratedName(string generatedName, GeneratedNameKind requiredKind, out string methodName) => Impl.TryParseSourceMethodNameFromGeneratedName(generatedName, requiredKind, out methodName);
        public static bool TryParseLocalFunctionNameFromGeneratedName(string generatedName, out string localFunctionName) =>
            Impl.TryParseLocalFunctionNameFromGeneratedName(generatedName, out localFunctionName);
    }
}
