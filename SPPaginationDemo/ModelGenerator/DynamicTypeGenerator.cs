﻿using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SPPaginationDemo.ModelGenerator;

public class DynamicTypeGenerator
{
    public Type Model { get; private set; }
    public byte[] AssemblyBytes { get; private set; }

    public static string SqlQueryToIdentifier(string sqlQuery)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(sqlQuery));

        var hexString = string.Concat(hash.Select(b => b.ToString("X2")));

        return hexString;
    }

    public DynamicTypeGenerator(string sqlQuery, Type interfaceType, string connectionString)
    {
        var columns = AnalyzeQuery(sqlQuery, connectionString);
        var typeName = $"DynamicType_{Guid.NewGuid():N}";

        var template = File.ReadAllText("dynamic_type_template.txt");

        var properties = string.Join(Environment.NewLine, columns.Select(column =>
        {
            var (propertyName, propertyType) = column;
            var nullableType = propertyType.IsValueType ? $"Nullable<{propertyType.Name}>" : propertyType.Name;
            return $"public {nullableType} {propertyName} {{ get; set; }}";
        }));

        var code = template
            .Replace("[[Namespace]]", interfaceType.Namespace)
            .Replace("[[TypeName]]", typeName)
            .Replace("[[InterfaceName]]", interfaceType.Name)
            .Replace("[[Properties]]", properties);

        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Private.CoreLib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(interfaceType.Assembly.Location)
        };

        var assemblyName = Path.GetRandomFileName();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        //TODO: Add error handling when compilation fails
        if (!result.Success)
            throw new Exception("Compilation failed");

        ms.Seek(0, SeekOrigin.Begin);
        AssemblyBytes = ms.ToArray();

        var assembly = Assembly.Load(AssemblyBytes);
        Model = assembly.GetTypes().First(t => t.Name == typeName);
    }

    private static IEnumerable<(string ColumnName, Type DataType)> AnalyzeQuery(string sqlQuery, string connectionString)
    {
        using var connection = new SqlConnection(connectionString);

        connection.Open();

        using var command = new SqlCommand(sqlQuery, connection);

        using var reader = command.ExecuteReader(CommandBehavior.SchemaOnly);

        var schemaTable = reader.GetSchemaTable();
        var columnNames = schemaTable!.Rows.OfType<DataRow>().Select(row => row["ColumnName"].ToString());
        var dataTypes = schemaTable.Rows.OfType<DataRow>().Select(row => row["DataType"] as Type);

        return columnNames.Zip(dataTypes, (columnName, dataType) => (ColumnName: columnName, DataType: dataType))
            .ToList()!;
    }
}