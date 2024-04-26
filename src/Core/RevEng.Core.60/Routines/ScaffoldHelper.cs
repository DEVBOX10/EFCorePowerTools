﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RevEng.Core.Abstractions.Metadata;

namespace RevEng.Core.Routines
{
    internal static class ScaffoldHelper
    {
        private static readonly HashSet<string> KeyWords = new HashSet<string>
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while",
        };

        public static string GenerateIdentifierName(Routine routine, RoutineModel model)
        {
            ArgumentNullException.ThrowIfNull(routine);

            ArgumentNullException.ThrowIfNull(model);

            return CreateIdentifier(GenerateUniqueName(routine, model)).Item1;
        }

        public static Tuple<string, string> GeneratePropertyName(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return CreateIdentifier(propertyName);
        }

        private static Tuple<string, string> CreateIdentifier(string name)
        {
            var original = name;

            var isValid = System.CodeDom.Compiler.CodeGenerator.IsValidLanguageIndependentIdentifier(name);

            string columAttribute = null;

            if (!isValid)
            {
                columAttribute = $"[Column(\"{name}\")]";

                // File name contains invalid chars, remove them
                var regex = new Regex(@"[^\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nd}\p{Nl}\p{Mn}\p{Mc}\p{Cf}\p{Pc}\p{Lm}]", RegexOptions.None, TimeSpan.FromSeconds(5));
                name = regex.Replace(name, string.Empty);

                if (string.IsNullOrWhiteSpace(name))
                {
                    // we cannot fix it
                    name = original;
                }
                else if (!char.IsLetter(name, 0))
                {
                    name = name.Insert(0, "_");
                }
            }

            if (KeyWords.Contains(name))
            {
                name = "@" + name;
            }

            return new Tuple<string, string>(name.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase), columAttribute);
        }

        private static string GenerateUniqueName(Routine routine, RoutineModel model)
        {
            if (!string.IsNullOrEmpty(routine.NewName))
            {
                return routine.NewName;
            }

            var numberOfNames = model.Routines.Count(p => p.Name == routine.Name);

            if (numberOfNames > 1)
            {
                return routine.Name + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(routine.Schema);
            }

            return routine.Name;
        }
    }
}
