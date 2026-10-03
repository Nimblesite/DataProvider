#if !AOT
using System.Reflection;
using Nimblesite.DataProvider.Migration.Core;

namespace DataProviderMigrate;

// Implements [MIG-AOT-EXPORT]. The export command loads an arbitrary external
// assembly via reflection (Assembly.LoadFrom + GetType + reflected member
// invoke). That is fundamentally incompatible with a self-contained Native AOT
// binary, so this whole file is excluded from the AOT build. The native binary
// ships migrate only; export remains in the managed `dotnet tool`.
public static partial class Program
{
    private static int RunExport(string[] args)
    {
        var parseResult = ParseExportArguments(args);

        return parseResult switch
        {
            ExportParseResult.Success success => ExecuteExport(success),
            ExportParseResult.Failure failure => ShowExportError(failure),
            ExportParseResult.HelpRequested => ShowExportUsage(),
        };
    }

    private static int ExecuteExport(ExportParseResult.Success args)
    {
        Console.WriteLine(
            $"""
            DataProviderMigrate - Export C# Schema to YAML
              Assembly: {args.AssemblyPath}
              Type:     {args.TypeName}
              Output:   {args.OutputPath}
            """
        );

        if (!File.Exists(args.AssemblyPath))
        {
            Console.WriteLine($"Error: Assembly not found: {args.AssemblyPath}");
            return 1;
        }

        try
        {
            var assembly = Assembly.LoadFrom(args.AssemblyPath);
            var schemaType = assembly.GetType(args.TypeName);

            if (schemaType is null)
            {
                Console.WriteLine($"Error: Type '{args.TypeName}' not found in assembly");
                return 1;
            }

            var schema = GetSchemaDefinition(schemaType);

            if (schema is null)
            {
                Console.WriteLine(
                    $"Error: Could not get SchemaDefinition from type '{args.TypeName}'\n  Expected: static property 'Definition' or static method 'Build()' returning SchemaDefinition"
                );
                return 1;
            }

            var directory = Path.GetDirectoryName(args.OutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SchemaYamlSerializer.ToYamlFile(schema, args.OutputPath);
            Console.WriteLine(
                $"Successfully exported schema '{schema.Name}' with {schema.Tables.Count} tables\n  Output: {args.OutputPath}"
            );
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
            return 1;
        }
    }

    private static SchemaDefinition? GetSchemaDefinition(Type schemaType)
    {
        var definitionProp = schemaType.GetProperty(
            "Definition",
            BindingFlags.Public | BindingFlags.Static
        );

        if (definitionProp?.GetValue(null) is SchemaDefinition defFromProp)
        {
            return defFromProp;
        }

        var buildMethod = schemaType.GetMethod(
            "Build",
            BindingFlags.Public | BindingFlags.Static,
            Type.EmptyTypes
        );

        if (buildMethod?.Invoke(null, null) is SchemaDefinition defFromMethod)
        {
            return defFromMethod;
        }

        return null;
    }

    private static int ShowExportError(ExportParseResult.Failure failure)
    {
        Console.WriteLine($"Error: {failure.Message}\n");
        return ShowExportUsage();
    }

    private static int ShowExportUsage()
    {
        Console.WriteLine(
            """
            Usage: DataProviderMigrate export [options]

            Options:
              --assembly, -a  Path to compiled assembly containing schema class (required)
              --type, -t      Fully qualified type name of schema class (required)
              --output, -o    Path to output YAML file (required)

            Examples:
              DataProviderMigrate export -a bin/Debug/net10.0/MyProject.dll -t MyNamespace.MySchema -o schema.yaml

            Schema Class Requirements:
              - Static property 'Definition' returning SchemaDefinition, OR
              - Static method 'Build()' returning SchemaDefinition
            """
        );
        return 1;
    }

    private static ExportParseResult ParseExportArguments(string[] args)
    {
        string? assemblyPath = null;
        string? typeName = null;
        string? outputPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--assembly" or "-a":
                    if (i + 1 >= args.Length)
                    {
                        return new ExportParseResult.Failure("--assembly requires a path argument");
                    }

                    assemblyPath = args[++i];
                    break;

                case "--type"
                or "-t":
                    if (i + 1 >= args.Length)
                    {
                        return new ExportParseResult.Failure(
                            "--type requires a type name argument"
                        );
                    }

                    typeName = args[++i];
                    break;

                case "--output"
                or "-o":
                    if (i + 1 >= args.Length)
                    {
                        return new ExportParseResult.Failure("--output requires a path argument");
                    }

                    outputPath = args[++i];
                    break;

                case "--help"
                or "-h":
                    return new ExportParseResult.HelpRequested();

                default:
                    if (arg.StartsWith('-'))
                    {
                        return new ExportParseResult.Failure($"Unknown option: {arg}");
                    }

                    break;
            }
        }

        if (string.IsNullOrEmpty(assemblyPath))
        {
            return new ExportParseResult.Failure("--assembly is required");
        }

        if (string.IsNullOrEmpty(typeName))
        {
            return new ExportParseResult.Failure("--type is required");
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            return new ExportParseResult.Failure("--output is required");
        }

        return new ExportParseResult.Success(assemblyPath, typeName, outputPath);
    }
}

/// <summary>
/// Export subcommand argument parsing result.
/// </summary>
public abstract record ExportParseResult
{
    private ExportParseResult() { }

    /// <summary>Successfully parsed export arguments.</summary>
    public sealed record Success(string AssemblyPath, string TypeName, string OutputPath)
        : ExportParseResult;

    /// <summary>Parse error.</summary>
    public sealed record Failure(string Message) : ExportParseResult;

    /// <summary>Help requested.</summary>
    public sealed record HelpRequested : ExportParseResult;
}
#endif
