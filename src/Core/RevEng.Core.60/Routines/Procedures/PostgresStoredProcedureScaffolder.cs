﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using RevEng.Core.Abstractions.Metadata;
using RevEng.Core.Routines.Extensions;

namespace RevEng.Core.Routines.Procedures
{
    public class PostgresStoredProcedureScaffolder : ProcedureScaffolder, IProcedureScaffolder
    {
        private const string ParameterPrefix = "parameter";

        public PostgresStoredProcedureScaffolder([NotNull] ICSharpHelper code, IClrTypeMapper typeMapper)
            : base(code, typeMapper)
        {
            FileNameSuffix = "Procedures";
        }

        public new SavedModelFiles Save(ScaffoldedModel scaffoldedModel, string outputDir, string nameSpaceValue, bool useAsyncCalls)
        {
            ArgumentNullException.ThrowIfNull(scaffoldedModel);

            var files = base.Save(scaffoldedModel, outputDir, nameSpaceValue, useAsyncCalls);

            var contextDir = Path.GetDirectoryName(Path.Combine(outputDir, scaffoldedModel.ContextFile.Path));
            var dbContextExtensionsText = GetDbContextExtensionsText(useAsyncCalls);
            var dbContextExtensionsName = useAsyncCalls ? "DbContextExtensions.cs" : "DbContextExtensions.Sync.cs";
            var dbContextExtensionsPath = Path.Combine(contextDir ?? string.Empty, dbContextExtensionsName);
            File.WriteAllText(dbContextExtensionsPath, dbContextExtensionsText.Replace("#NAMESPACE#", nameSpaceValue, StringComparison.OrdinalIgnoreCase), Encoding.UTF8);
            files.AdditionalFiles.Add(dbContextExtensionsPath);

            return files;
        }

        protected override void GenerateProcedure(Routine procedure, RoutineModel model, bool signatureOnly, bool useAsyncCalls)
        {
            ArgumentNullException.ThrowIfNull(procedure);

            var paramStrings = procedure.Parameters.Where(p => !p.Output)
                .Select(p => $"{Code.Reference(p.ClrTypeFromNpgsqlParameter(asMethodParameter: true))} {Code.Identifier(p.Name)}")
                .ToList();

            var allOutParams = procedure.Parameters.Where(p => p.Output).ToList();

            var outParams = allOutParams.SkipLast(1).ToList();

            var outParamStrings = outParams
                .Select(p => $"OutputParameter<{Code.Reference(p.ClrTypeFromNpgsqlParameter())}> {Code.Identifier(p.Name)}")
                .ToList();

            var fullExec = GenerateProcedureStatement(procedure, useAsyncCalls);

            var identifier = ScaffoldHelper.GenerateIdentifierName(procedure, model);

            var returnClass = identifier + "Result";

            if (!string.IsNullOrEmpty(procedure.MappedType))
            {
                returnClass = procedure.MappedType;
            }

            var line = GenerateMethodSignature(procedure, outParams, paramStrings, outParamStrings, identifier, signatureOnly, useAsyncCalls, returnClass);

            if (signatureOnly)
            {
                Sb.Append(line);
                return;
            }

            using (Sb.Indent())
            {
                Sb.AppendLine();

                Sb.AppendLine(line);
                Sb.AppendLine("{");

                using (Sb.Indent())
                {
                    foreach (var parameter in allOutParams)
                    {
                        GenerateParameterVar(parameter);
                    }

                    Sb.AppendLine();

                    Sb.AppendLine("var npgsqlParameters = new []");
                    Sb.AppendLine("{");
                    using (Sb.Indent())
                    {
                        foreach (var parameter in procedure.Parameters)
                        {
                            if (parameter.Output)
                            {
                                Sb.Append($"{ParameterPrefix}{parameter.Name}");
                            }
                            else
                            {
                                GenerateParameter(parameter);
                            }

                            Sb.AppendLine(",");
                        }
                    }

                    Sb.AppendLine("};");

                    if (procedure.HasValidResultSet && (procedure.Results.Count == 0 || procedure.Results[0].Count == 0))
                    {
                        Sb.AppendLine(useAsyncCalls
                            ? $"var _ = await _context.Database.ExecuteSqlRawAsync({fullExec});"
                            : $"var _ = _context.Database.ExecuteSqlRaw({fullExec});");
                    }
                    else
                    {
                        Sb.AppendLine(useAsyncCalls
                            ? $"var _ = await _context.SqlQueryAsync<{returnClass}>({fullExec});"
                            : $"var _ = _context.SqlQuery<{returnClass}>({fullExec});");
                    }

                    Sb.AppendLine();

                    foreach (var parameter in outParams)
                    {
                        Sb.AppendLine($"{Code.Identifier(parameter.Name)}.SetValue({ParameterPrefix}{parameter.Name}.Value);");
                    }

                    Sb.AppendLine();

                    Sb.AppendLine("return _;");
                }

                Sb.AppendLine("}");
            }
        }

        private static string GetDbContextExtensionsText(bool useAsyncCalls)
        {
#if CORE80
            var dbContextExtensionTemplateName = useAsyncCalls ? "RevEng.Core.DbContextExtensionsSqlQuery" : "RevEng.Core.DbContextExtensionsSqlQuery.Sync";
#else
            var dbContextExtensionTemplateName = useAsyncCalls ? "RevEng.Core.DbContextExtensions" : "RevEng.Core.DbContextExtensions.Sync";
#endif
            var assembly = typeof(SqlServerStoredProcedureScaffolder).GetTypeInfo().Assembly;
            using var stream = assembly.GetManifestResourceStream(dbContextExtensionTemplateName);
            if (stream == null)
            {
                return string.Empty;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static string GenerateProcedureStatement(Routine procedure, bool useAsyncCalls)
        {
            var paramList = new List<string>();
            int i = 1;

            foreach (var parameter in procedure.Parameters)
            {
                var p = $"${i++}";

                if (parameter.Output)
                {
                    p += " OUTPUT";
                }

                paramList.Add(p);
            }

            var fullExec = $"\"SELECT * FROM {procedure.Schema}.{procedure.Name} {string.Join(", ", paramList)}\", npgsqlParameters{(useAsyncCalls ? ", cancellationToken" : string.Empty)}".Replace(" \"", "\"", StringComparison.OrdinalIgnoreCase);
            return fullExec;
        }

        private static string GenerateMethodSignature(Routine procedure, List<ModuleParameter> outParams, IList<string> paramStrings, List<string> outParamStrings, string identifier, bool signatureOnly, bool useAsyncCalls, string returnClass)
        {
            string returnType;
            if (procedure.HasValidResultSet && (procedure.Results.Count == 0 || procedure.Results[0].Count == 0))
            {
                returnType = $"int";
            }
            else
            {
                returnType = $"List<{returnClass}>";
            }

            returnType = useAsyncCalls ? $"Task<{returnType}>" : returnType;

            var lineStart = signatureOnly ? string.Empty : $"public virtual {(useAsyncCalls ? "async " : string.Empty)}";
            var line = $"{lineStart}{returnType} {identifier}{(useAsyncCalls ? "Async" : string.Empty)}({string.Join(", ", paramStrings)}";

            if (outParams.Count > 0)
            {
                if (paramStrings.Any())
                {
                    line += ", ";
                }

                line += $"{string.Join(", ", outParamStrings)}";
            }

            if (paramStrings.Any() || outParams.Count > 0)
            {
                line += ", ";
            }

            line += useAsyncCalls ? ", CancellationToken cancellationToken = default)" : ")";

            return line;
        }

        private void GenerateParameterVar(ModuleParameter parameter)
        {
            Sb.Append($"var {ParameterPrefix}{parameter.Name} = ");
            GenerateParameter(parameter);
            Sb.AppendLine(";");
        }

        private void GenerateParameter(ModuleParameter parameter)
        {
            Sb.AppendLine("new NpgsqlParameter");
            Sb.AppendLine("{");

            var sqlDbType = parameter.GetNpgsqlDbType();

            using (Sb.Indent())
            {
                if (sqlDbType.IsScaleType())
                {
                    if (parameter.Precision > 0)
                    {
                        Sb.AppendLine($"Precision = {parameter.Precision},");
                    }

                    if (parameter.Scale > 0)
                    {
                        Sb.AppendLine($"Scale = {parameter.Scale},");
                    }
                }

                if (sqlDbType.IsVarTimeType() && parameter.Scale > 0)
                {
                    Sb.AppendLine($"Scale = {parameter.Scale},");
                }

                if (sqlDbType.IsLengthRequiredType())
                {
                    Sb.AppendLine($"Size = {parameter.Length},");
                }

                if (!parameter.IsReturnValue)
                {
                    if (parameter.Output)
                    {
                        Sb.AppendLine("Direction = System.Data.ParameterDirection.InputOutput,");
                        AppendValue(parameter);
                    }
                    else
                    {
                        AppendValue(parameter);
                    }
                }
                else
                {
                    Sb.AppendLine("Direction = System.Data.ParameterDirection.Output,");
                }

                Sb.AppendLine($"SqlDbType = NpgsqlTypes.NpgsqlDbType.{sqlDbType},");
            }

            Sb.Append("}");
        }

        private void AppendValue(ModuleParameter parameter)
        {
            var value = parameter.Nullable ? $"{Code.Identifier(parameter.Name)} ?? Convert.DBNull" : $"{Code.Identifier(parameter.Name)}";
            if (parameter.Output)
            {
                value = parameter.Nullable ? $"{Code.Identifier(parameter.Name)}?._value ?? Convert.DBNull" : $"{Code.Identifier(parameter.Name)}?._value";
            }

            Sb.AppendLine($"Value = {value},");
        }
    }
}
