using System.Reflection;
using Microsoft.Data.Sqlite;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.Postgres;
using Nimblesite.DataProvider.Migration.SQLite;
using Npgsql;

namespace DataProviderMigrate;

/// <summary>
/// CLI tool for database schema operations: migrate from YAML and export C# schemas to YAML.
/// This is the ONLY canonical tool for database creation - all projects MUST use this.
/// </summary>
public static class Program
{
    /// <summary>
    /// Entry point - dispatches to migrate or export subcommand.
    /// Usage:
    ///   migrate: dotnet run -- migrate --schema path/to/schema.yaml --output path/to/database.db --provider [sqlite|postgres]
    ///   export:  dotnet run -- export --assembly path/to/assembly.dll --type Namespace.SchemaClass --output path/to/schema.yaml
    /// </summary>
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            return ShowTopLevelUsage();
        }

        var command = args[0];
        var remainingArgs = args[1..];

        return command switch
        {
            "migrate" => RunMigrate(remainingArgs),
            "export" => RunExport(remainingArgs),
            _ when command.StartsWith('-') => RunMigrate(args), // backwards compat: no subcommand = migrate
            _ => ShowUnknownCommand(command),
        };
    }

    private static int RunMigrate(string[] args)
    {
        var parseResult = ParseMigrateArguments(args);

        return parseResult switch
        {
            MigrateParseResult.Success success => ExecuteMigration(success),
            MigrateParseResult.Failure failure => ShowMigrateError(failure),
            MigrateParseResult.HelpRequested => ShowMigrateUsage(),
        };
    }

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

    // ── Migrate ──────────────────────────────────────────────────────────

    private static int ExecuteMigration(MigrateParseResult.Success args)
    {
        Console.WriteLine(
            $"""
            DataProviderMigrate - Database Schema Tool
              Schema:   {args.SchemaPath}
              Output:   {args.OutputPath}
              Provider: {args.Provider}
            """
        );

        if (!File.Exists(args.SchemaPath))
        {
            Console.WriteLine($"Error: Schema file not found: {args.SchemaPath}");
            return 1;
        }

        SchemaDefinition schema;
        try
        {
            var yamlContent = File.ReadAllText(args.SchemaPath);
            schema = SchemaYamlSerializer.FromYaml(yamlContent);
            Console.WriteLine($"Loaded schema '{schema.Name}' with {schema.Tables.Count} tables");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to parse YAML schema: {ex}");
            return 1;
        }

        return args.Provider.ToLowerInvariant() switch
        {
            "sqlite" => MigrateSqliteDatabase(schema, args.OutputPath, args.AllowDestructive),
            "postgres" => MigratePostgresDatabase(schema, args.OutputPath, args.AllowDestructive),
            _ => ShowProviderError(args.Provider),
        };
    }

    private static int MigrateSqliteDatabase(
        SchemaDefinition schema,
        string outputPath,
        bool allowDestructive
    )
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var connectionString = $"Data Source={outputPath}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            Console.WriteLine($"Connected to SQLite: {outputPath}");

            return ApplyDiff(
                schema,
                allowDestructive,
                () => SqliteSchemaInspector.Inspect(connection),
                ops =>
                    MigrationRunner.Apply(
                        connection,
                        ops,
                        SqliteDdlGenerator.Generate,
                        new MigrationOptions { AllowDestructive = allowDestructive }
                    )
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: SQLite migration failed: {ex}");
            return 1;
        }
    }

    private static int MigratePostgresDatabase(
        SchemaDefinition schema,
        string connectionString,
        bool allowDestructive
    )
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            Console.WriteLine("Connected to PostgreSQL database");

            return ApplyDiff(
                schema,
                allowDestructive,
                () => PostgresSchemaInspector.Inspect(connection, "public"),
                ops =>
                    MigrationRunner.Apply(
                        connection,
                        ops,
                        PostgresDdlGenerator.Generate,
                        new MigrationOptions { AllowDestructive = allowDestructive }
                    )
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: PostgreSQL connection/migration failed: {ex}");
            return 1;
        }
    }

    /// <summary>
    /// Inspect → diff → apply pipeline shared between SQLite and Postgres.
    /// This is the only correct migration path: it surfaces RLS, indexes,
    /// FKs, and column adds via SchemaDiff and respects --allow-destructive.
    /// Implements GitHub issue #39.
    /// </summary>
    private static int ApplyDiff(
        SchemaDefinition schema,
        bool allowDestructive,
        Func<Outcome.Result<SchemaDefinition, MigrationError>> inspect,
        Func<IReadOnlyList<SchemaOperation>, Outcome.Result<bool, MigrationError>> apply
    )
    {
        var inspectResult = inspect();
        if (
            inspectResult
            is Outcome.Result<SchemaDefinition, MigrationError>.Error<
                SchemaDefinition,
                MigrationError
            > inspectErr
        )
        {
            Console.WriteLine($"Error: schema inspection failed: {inspectErr.Value}");
            return 1;
        }
        var current = (
            (Outcome.Result<SchemaDefinition, MigrationError>.Ok<
                SchemaDefinition,
                MigrationError
            >)inspectResult
        ).Value;

        var diff = SchemaDiff.Calculate(current, schema, allowDestructive);
        if (
            diff
            is Outcome.Result<IReadOnlyList<SchemaOperation>, MigrationError>.Error<
                IReadOnlyList<SchemaOperation>,
                MigrationError
            > diffErr
        )
        {
            Console.WriteLine($"Error: schema diff failed: {diffErr.Value}");
            return 1;
        }
        var operations = (
            (Outcome.Result<IReadOnlyList<SchemaOperation>, MigrationError>.Ok<
                IReadOnlyList<SchemaOperation>,
                MigrationError
            >)diff
        ).Value;

        if (operations.Count == 0)
        {
            Console.WriteLine("Schema is up to date — no operations needed");
            return 0;
        }

        Console.WriteLine($"Applying {operations.Count} operation(s):");
        foreach (var op in operations)
        {
            Console.WriteLine($"  {op.GetType().Name}");
        }

        var applyResult = apply(operations);
        if (
            applyResult is Outcome.Result<bool, MigrationError>.Error<bool, MigrationError> applyErr
        )
        {
            Console.WriteLine($"Error: migration apply failed: {applyErr.Value}");
            return 1;
        }

        Console.WriteLine("Migration completed successfully");
        return 0;
    }

    private static int ShowProviderError(string provider)
    {
        Console.WriteLine(
            $"Error: Unknown provider '{provider}'\nValid providers: sqlite, postgres"
        );
        return 1;
    }

    // ── Export ────────────────────────────────────────────────────────────

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

    // ── Usage / Errors ───────────────────────────────────────────────────

    private static int ShowTopLevelUsage()
    {
        Console.WriteLine(
            """
            DataProviderMigrate - Database Schema Tool

            Commands:
              migrate   Create database from YAML schema definition
              export    Export C# schema class to YAML file

            Usage:
              DataProviderMigrate migrate --schema schema.yaml --output database.db [--provider sqlite|postgres]
              DataProviderMigrate export --assembly assembly.dll --type Namespace.SchemaClass --output schema.yaml

            Run 'DataProviderMigrate <command> --help' for command-specific options.
            """
        );
        return 1;
    }

    private static int ShowUnknownCommand(string command)
    {
        Console.WriteLine($"Error: Unknown command '{command}'\nValid commands: migrate, export");
        return 1;
    }

    private static int ShowMigrateError(MigrateParseResult.Failure failure)
    {
        Console.WriteLine($"Error: {failure.Message}\n");
        return ShowMigrateUsage();
    }

    private static int ShowMigrateUsage()
    {
        Console.WriteLine(
            """
            Usage: DataProviderMigrate migrate [options]

            Options:
              --schema, -s         Path to YAML schema definition file (required)
              --output, -o         Path to output database file (SQLite) or connection string (Postgres)
              --provider, -p       Database provider: sqlite or postgres (default: sqlite)
              --allow-destructive  Permit DROP/DISABLE operations (drift cleanup, FORCE removal,
                                   policy drops). Off by default for safety.

            Behaviour: Inspect → Diff → Apply. Re-running against a converged database emits zero
            operations (idempotent). Adds new tables/columns/indexes/FKs/RLS policies. Drift drops
            (FKs, columns, RLS policies, FORCE removal, DISABLE RLS) require --allow-destructive.

            Examples:
              DataProviderMigrate migrate --schema my-schema.yaml --output ./build.db --provider sqlite
              DataProviderMigrate migrate --schema my-schema.yaml --output "Host=localhost;Database=mydb;Username=user;Password=pass" --provider postgres
              DataProviderMigrate migrate --schema my-schema.yaml --output "$PG_URL" --provider postgres --allow-destructive
            """
        );
        return 1;
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

    // ── Argument Parsing ─────────────────────────────────────────────────

    private static MigrateParseResult ParseMigrateArguments(string[] args)
    {
        string? schemaPath = null;
        string? outputPath = null;
        var provider = "sqlite";
        var allowDestructive = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--schema" or "-s":
                    if (i + 1 >= args.Length)
                    {
                        return new MigrateParseResult.Failure("--schema requires a path argument");
                    }

                    schemaPath = args[++i];
                    break;

                case "--output"
                or "-o":
                    if (i + 1 >= args.Length)
                    {
                        return new MigrateParseResult.Failure("--output requires a path argument");
                    }

                    outputPath = args[++i];
                    break;

                case "--provider"
                or "-p":
                    if (i + 1 >= args.Length)
                    {
                        return new MigrateParseResult.Failure(
                            "--provider requires an argument (sqlite or postgres)"
                        );
                    }

                    provider = args[++i];
                    break;

                case "--allow-destructive":
                    allowDestructive = true;
                    break;

                case "--help"
                or "-h":
                    return new MigrateParseResult.HelpRequested();

                default:
                    if (arg.StartsWith('-'))
                    {
                        return new MigrateParseResult.Failure($"Unknown option: {arg}");
                    }

                    break;
            }
        }

        if (string.IsNullOrEmpty(schemaPath))
        {
            return new MigrateParseResult.Failure("--schema is required");
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            return new MigrateParseResult.Failure("--output is required");
        }

        return new MigrateParseResult.Success(schemaPath, outputPath, provider, allowDestructive);
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
/// Migrate subcommand argument parsing result.
/// </summary>
public abstract record MigrateParseResult
{
    private MigrateParseResult() { }

    /// <summary>Successfully parsed migrate arguments.</summary>
    public sealed record Success(
        string SchemaPath,
        string OutputPath,
        string Provider,
        bool AllowDestructive
    ) : MigrateParseResult;

    /// <summary>Parse error.</summary>
    public sealed record Failure(string Message) : MigrateParseResult;

    /// <summary>Help requested.</summary>
    public sealed record HelpRequested : MigrateParseResult;
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
