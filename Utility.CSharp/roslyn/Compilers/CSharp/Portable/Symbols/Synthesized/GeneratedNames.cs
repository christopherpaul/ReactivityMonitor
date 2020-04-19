// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal static partial class GeneratedNames
    {
        internal const string SynthesizedLocalNamePrefix = "CS$";
        internal const char DotReplacementInTypeNames = '-';
        private const string SuffixSeparator = "__";
        private const char LocalFunctionNameTerminator = '|';

        internal static bool IsGeneratedMemberName(string memberName)
        {
            return memberName.Length > 0 && memberName[0] == '<';
        }

        internal const string AnonymousNamePrefix = "<>f__AnonymousType";

        internal static bool TryParseAnonymousTypeTemplateName(string name, out int index)
        {
            // No callers require anonymous types from net modules,
            // so names with module id are ignored.
            if (name.StartsWith(AnonymousNamePrefix, StringComparison.Ordinal))
            {
                if (int.TryParse(name.Substring(AnonymousNamePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out index))
                {
                    return true;
                }
            }

            index = -1;
            return false;
        }

        internal static bool TryParseAnonymousTypeParameterName(string typeParameterName, out string propertyName)
        {
            if (typeParameterName.StartsWith("<", StringComparison.Ordinal) &&
                typeParameterName.EndsWith(">j__TPar", StringComparison.Ordinal))
            {
                propertyName = typeParameterName.Substring(1, typeParameterName.Length - 9);
                return true;
            }

            propertyName = null;
            return false;
        }

        // The type of generated name. See TryParseGeneratedName.
        internal static GeneratedNameKind GetKind(string name)
        {
            GeneratedNameKind kind;
            int openBracketOffset;
            int closeBracketOffset;
            return TryParseGeneratedName(name, out kind, out openBracketOffset, out closeBracketOffset) ? kind : GeneratedNameKind.None;
        }

        // Parse the generated name. Returns true for names of the form
        // [CS$]<[middle]>c[__[suffix]] where [CS$] is included for certain
        // generated names, where [middle] and [__[suffix]] are optional,
        // and where c is a single character in [1-9a-z]
        // (csharp\LanguageAnalysis\LIB\SpecialName.cpp).
        internal static bool TryParseGeneratedName(
            string name,
            out GeneratedNameKind kind,
            out int openBracketOffset,
            out int closeBracketOffset)
        {
            openBracketOffset = -1;
            if (name.StartsWith("CS$<", StringComparison.Ordinal))
            {
                openBracketOffset = 3;
            }
            else if (name.StartsWith("<", StringComparison.Ordinal))
            {
                openBracketOffset = 0;
            }

            if (openBracketOffset >= 0)
            {
                closeBracketOffset = name.IndexOfBalancedParenthesis(openBracketOffset, '>');
                if (closeBracketOffset >= 0 && closeBracketOffset + 1 < name.Length)
                {
                    int c = name[closeBracketOffset + 1];
                    if ((c >= '1' && c <= '9') || (c >= 'a' && c <= 'z')) // Note '0' is not special.
                    {
                        kind = (GeneratedNameKind)c;
                        return true;
                    }
                }
            }

            kind = GeneratedNameKind.None;
            openBracketOffset = -1;
            closeBracketOffset = -1;
            return false;
        }

        internal static bool TryParseSourceMethodNameFromGeneratedName(string generatedName, GeneratedNameKind requiredKind, out string methodName)
        {
            int openBracketOffset;
            int closeBracketOffset;
            GeneratedNameKind kind;
            if (!TryParseGeneratedName(generatedName, out kind, out openBracketOffset, out closeBracketOffset))
            {
                methodName = null;
                return false;
            }

            if (requiredKind != 0 && kind != requiredKind)
            {
                methodName = null;
                return false;
            }

            methodName = generatedName.Substring(openBracketOffset + 1, closeBracketOffset - openBracketOffset - 1);

            if (kind.IsTypeName())
            {
                methodName = methodName.Replace(DotReplacementInTypeNames, '.');
            }

            return true;
        }

        // Extracts the slot index from a name of a field that stores hoisted variables or awaiters.
        // Such a name ends with "__{slot index + 1}". 
        // Returned slot index is >= 0.
        internal static bool TryParseSlotIndex(string fieldName, out int slotIndex)
        {
            int lastUnder = fieldName.LastIndexOf('_');
            if (lastUnder - 1 < 0 || lastUnder == fieldName.Length || fieldName[lastUnder - 1] != '_')
            {
                slotIndex = -1;
                return false;
            }

            if (int.TryParse(fieldName.Substring(lastUnder + 1), NumberStyles.None, CultureInfo.InvariantCulture, out slotIndex) && slotIndex >= 1)
            {
                slotIndex--;
                return true;
            }

            slotIndex = -1;
            return false;
        }

        internal static bool IsSynthesizedLocalName(string name)
        {
            return name.StartsWith(SynthesizedLocalNamePrefix, StringComparison.Ordinal);
        }

        internal static bool TryParseLocalFunctionNameFromGeneratedName(string generatedName, out string localFunctionName)
        {
            int openBracketOffset;
            int closeBracketOffset;
            GeneratedNameKind kind;
            if (!TryParseGeneratedName(generatedName, out kind, out openBracketOffset, out closeBracketOffset) || kind != GeneratedNameKind.LocalFunction)
            {
                localFunctionName = null;
                return false;
            }

            int suffixSeparatorOffset = generatedName.IndexOf(SuffixSeparator, closeBracketOffset);
            if (suffixSeparatorOffset < 0)
            {
                localFunctionName = null;
                return false;
            }

            int localFunctionNameOffset = suffixSeparatorOffset + SuffixSeparator.Length;

            int suffixTerminatorOffset = generatedName.IndexOf(LocalFunctionNameTerminator);
            if (suffixTerminatorOffset < 0)
            {
                localFunctionName = null;
                return false;
            }

            localFunctionName = generatedName.Substring(localFunctionNameOffset, suffixTerminatorOffset - localFunctionNameOffset);
            return true;
        }
    }
}
