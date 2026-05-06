namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Fluent builder for schema definitions.
/// </summary>
/// <example>
/// <code>
/// var schema = SchemaFactory.Define("MyDatabase")
///     .Table("users", t => t
///         .Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
///         .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
///         .Column("created_at", PortableTypes.DateTime, c => c.DefaultLql("now()"))
///         .Index("idx_users_email", "email", unique: true))
///     .Table("orders", t => t
///         .Column("id", PortableTypes.Int, c => c.Identity().PrimaryKey())
///         .Column("user_id", PortableTypes.Uuid, c => c.NotNull())
///         .Column("total", PortableTypes.Decimal(10, 2))
///         .ForeignKey("user_id", "users", "id", onDelete: ForeignKeyAction.Cascade))
///     .Build();
/// </code>
/// </example>
public static class SchemaFactory
{
    /// <summary>
    /// Start defining a schema with the given name.
    /// </summary>
    /// <param name="name">The name of the schema/database.</param>
    /// <returns>A <see cref="SchemaBuilder"/> for fluent configuration.</returns>
    public static SchemaBuilder Define(string name) => new(name);
}

/// <summary>
/// Backward-compatible alias for SchemaFactory.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1724:Type names should not match namespaces",
    Justification = "Schema is the natural name for this entry point"
)]
public static class Schema
{
    /// <summary>
    /// Start defining a schema with the given name.
    /// </summary>
    public static SchemaBuilder Define(string name) => SchemaFactory.Define(name);
}

/// <summary>
/// Builder for creating schema definitions with tables, columns, indexes, and constraints.
/// </summary>
/// <example>
/// <code>
/// var schema = Schema.Define("inventory")
///     .Table("products", t => t
///         .Column("id", PortableTypes.Int, c => c.Identity().PrimaryKey())
///         .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
///         .Column("price", PortableTypes.Decimal(10, 2), c => c.NotNull().Check("price >= 0"))
///         .Column("sku", PortableTypes.VarChar(50), c => c.NotNull())
///         .Unique("uq_products_sku", "sku"))
///     .Build();
/// </code>
/// </example>
public sealed class SchemaBuilder
{
    private readonly string _name;
    private readonly List<TableDefinition> _tables = [];

    internal SchemaBuilder(string name) => _name = name;

    /// <summary>
    /// Add a table to the schema.
    /// </summary>
    public SchemaBuilder Table(string name, Action<TableBuilder> configure)
    {
        var builder = new TableBuilder(name);
        configure(builder);
        _tables.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Add a table with explicit schema to the schema.
    /// </summary>
    public SchemaBuilder Table(string schema, string name, Action<TableBuilder> configure)
    {
        var builder = new TableBuilder(name, schema);
        configure(builder);
        _tables.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Build the schema definition.
    /// </summary>
    public SchemaDefinition Build() => new() { Name = _name, Tables = _tables.AsReadOnly() };
}

/// <summary>
/// Builder for creating table definitions with columns, indexes, foreign keys, and constraints.
/// </summary>
/// <example>
/// <code>
/// // Define a table with various column types and constraints
/// .Table("employees", t => t
///     .Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
///     .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
///     .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
///     .Column("department_id", PortableTypes.Int)
///     .Column("salary", PortableTypes.Decimal(12, 2))
///     .Column("hired_at", PortableTypes.DateTime, c => c.DefaultLql("now()"))
///     .Index("idx_employees_email", "email", unique: true)
///     .Index("idx_employees_dept", "department_id")
///     .ForeignKey("department_id", "departments", "id"))
/// </code>
/// </example>
public sealed class TableBuilder
{
    private readonly string _name;
    private readonly string _schema;
    private readonly List<ColumnDefinition> _columns = [];
    private readonly List<IndexDefinition> _indexes = [];
    private readonly List<ForeignKeyDefinition> _foreignKeys = [];
    private readonly List<UniqueConstraintDefinition> _uniqueConstraints = [];
    private readonly List<CheckConstraintDefinition> _checkConstraints = [];
    private PrimaryKeyDefinition? _primaryKey;
    private string? _comment;

    internal TableBuilder(string name, string schema = "public")
    {
        _name = name;
        _schema = schema;
    }

    /// <summary>
    /// Add a column to the table.
    /// </summary>
    public TableBuilder Column(
        string name,
        PortableType type,
        Action<ColumnBuilder>? configure = null
    )
    {
        var builder = new ColumnBuilder(name, type);
        configure?.Invoke(builder);

        var column = builder.Build();
        _columns.Add(column);

        // Auto-add to primary key if marked
        if (builder.IsPrimaryKey)
        {
            _primaryKey = new PrimaryKeyDefinition { Name = $"PK_{_name}", Columns = [name] };
        }

        return this;
    }

    /// <summary>
    /// Add an index to the table.
    /// </summary>
    public TableBuilder Index(
        string name,
        string column,
        bool unique = false,
        string? filter = null
    )
    {
        _indexes.Add(
            new IndexDefinition
            {
                Name = name,
                Columns = [column],
                IsUnique = unique,
                Filter = filter,
            }
        );
        return this;
    }

    /// <summary>
    /// Add a multi-column index to the table.
    /// </summary>
    public TableBuilder Index(
        string name,
        string[] columns,
        bool unique = false,
        string? filter = null
    )
    {
        _indexes.Add(
            new IndexDefinition
            {
                Name = name,
                Columns = columns,
                IsUnique = unique,
                Filter = filter,
            }
        );
        return this;
    }

    /// <summary>
    /// Add an expression-based index (e.g., lower(name) for case-insensitive matching).
    /// Expressions are emitted verbatim in the CREATE INDEX statement.
    /// </summary>
    public TableBuilder ExpressionIndex(
        string name,
        string expression,
        bool unique = false,
        string? filter = null
    )
    {
        _indexes.Add(
            new IndexDefinition
            {
                Name = name,
                Expressions = [expression],
                IsUnique = unique,
                Filter = filter,
            }
        );
        return this;
    }

    /// <summary>
    /// Add a multi-expression index (e.g., lower(name), suburb_id for composite expression indexes).
    /// Expressions are emitted verbatim in the CREATE INDEX statement.
    /// </summary>
    public TableBuilder ExpressionIndex(
        string name,
        string[] expressions,
        bool unique = false,
        string? filter = null
    )
    {
        _indexes.Add(
            new IndexDefinition
            {
                Name = name,
                Expressions = expressions,
                IsUnique = unique,
                Filter = filter,
            }
        );
        return this;
    }

    /// <summary>
    /// Add a foreign key to the table.
    /// </summary>
    public TableBuilder ForeignKey(
        string column,
        string referencedTable,
        string referencedColumn,
        ForeignKeyAction onDelete = ForeignKeyAction.NoAction,
        ForeignKeyAction onUpdate = ForeignKeyAction.NoAction
    )
    {
        _foreignKeys.Add(
            new ForeignKeyDefinition
            {
                Name = $"FK_{_name}_{column}",
                Columns = [column],
                ReferencedTable = referencedTable,
                ReferencedColumns = [referencedColumn],
                OnDelete = onDelete,
                OnUpdate = onUpdate,
            }
        );
        return this;
    }

    /// <summary>
    /// Define a composite primary key spanning multiple columns.
    /// </summary>
    public TableBuilder CompositePrimaryKey(params string[] columns)
    {
        _primaryKey = new PrimaryKeyDefinition { Name = $"PK_{_name}", Columns = columns };
        return this;
    }

    /// <summary>
    /// Add a unique constraint to the table.
    /// </summary>
    public TableBuilder Unique(string name, params string[] columns)
    {
        _uniqueConstraints.Add(new UniqueConstraintDefinition { Name = name, Columns = columns });
        return this;
    }

    /// <summary>
    /// Add a check constraint to the table.
    /// </summary>
    public TableBuilder Check(string name, string expression)
    {
        _checkConstraints.Add(
            new CheckConstraintDefinition { Name = name, Expression = expression }
        );
        return this;
    }

    /// <summary>
    /// Add a comment to the table.
    /// </summary>
    public TableBuilder Comment(string comment)
    {
        _comment = comment;
        return this;
    }

    internal TableDefinition Build() =>
        new()
        {
            Schema = _schema,
            Name = _name,
            Columns = _columns.AsReadOnly(),
            Indexes = _indexes.AsReadOnly(),
            ForeignKeys = _foreignKeys.AsReadOnly(),
            PrimaryKey = _primaryKey,
            UniqueConstraints = _uniqueConstraints.AsReadOnly(),
            CheckConstraints = _checkConstraints.AsReadOnly(),
            Comment = _comment,
        };
}

/// <summary>
/// Builder for creating column definitions with type, nullability, defaults, and constraints.
/// </summary>
/// <example>
/// <code>
/// // UUID primary key with auto-generation
/// .Column("id", PortableTypes.Uuid, c => c.PrimaryKey().DefaultLql("gen_uuid()"))
///
/// // Required string with max length
/// .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
///
/// // Auto-increment integer
/// .Column("sequence", PortableTypes.Int, c => c.Identity())
///
/// // Decimal with precision and check constraint
/// .Column("price", PortableTypes.Decimal(10, 2), c => c.NotNull().Check("price > 0"))
///
/// // DateTime with default to current timestamp
/// .Column("created_at", PortableTypes.DateTime, c => c.DefaultLql("now()"))
/// </code>
/// </example>
public sealed class ColumnBuilder
{
    private readonly string _name;
    private readonly PortableType _type;
    private bool _isNullable = true;
    private string? _defaultValue;
    private string? _defaultLqlExpression;
    private bool _isIdentity;
    private long _identitySeed = 1;
    private long _identityIncrement = 1;
    private string? _computedExpression;
    private bool _isComputedPersisted;
    private string? _collation;
    private string? _checkConstraint;
    private string? _checkConstraintName;
    private string? _comment;

    internal bool IsPrimaryKey { get; private set; }

    internal ColumnBuilder(string name, PortableType type)
    {
        _name = name;
        _type = type;
    }

    /// <summary>
    /// Mark column as NOT NULL.
    /// </summary>
    public ColumnBuilder NotNull()
    {
        _isNullable = false;
        return this;
    }

    /// <summary>
    /// Mark column as nullable (default).
    /// </summary>
    public ColumnBuilder Nullable()
    {
        _isNullable = true;
        return this;
    }

    /// <summary>
    /// Set default value expression (platform-specific SQL).
    /// </summary>
    public ColumnBuilder Default(string defaultValue)
    {
        _defaultValue = defaultValue;
        return this;
    }

    /// <summary>
    /// Set default value using LQL expression (platform-independent).
    /// The expression will be translated to platform-specific SQL by DDL generators.
    /// Common LQL functions: now(), gen_uuid(), lower(), upper(), coalesce().
    /// </summary>
    public ColumnBuilder DefaultLql(string lqlExpression)
    {
        _defaultLqlExpression = lqlExpression;
        return this;
    }

    /// <summary>
    /// Mark as identity/auto-increment column.
    /// </summary>
    public ColumnBuilder Identity(long seed = 1, long increment = 1)
    {
        _isIdentity = true;
        _identitySeed = seed;
        _identityIncrement = increment;
        _isNullable = false; // Identity columns are implicitly NOT NULL
        return this;
    }

    /// <summary>
    /// Mark as primary key (also sets NOT NULL).
    /// </summary>
    public ColumnBuilder PrimaryKey()
    {
        IsPrimaryKey = true;
        _isNullable = false;
        return this;
    }

    /// <summary>
    /// Set computed column expression.
    /// </summary>
    public ColumnBuilder Computed(string expression, bool persisted = false)
    {
        _computedExpression = expression;
        _isComputedPersisted = persisted;
        return this;
    }

    /// <summary>
    /// Set column collation.
    /// </summary>
    public ColumnBuilder Collation(string collation)
    {
        _collation = collation;
        return this;
    }

    /// <summary>
    /// Add check constraint to column.
    /// </summary>
    public ColumnBuilder Check(string expression)
    {
        _checkConstraint = expression;
        return this;
    }

    /// <summary>
    /// Add named check constraint to column.
    /// Implements [MIG-PG-NAMED-COLUMN-CHECK-CONSTRAINT].
    /// </summary>
    public ColumnBuilder Check(string name, string expression)
    {
        _checkConstraintName = name;
        _checkConstraint = expression;
        return this;
    }

    /// <summary>
    /// Add comment to column.
    /// </summary>
    public ColumnBuilder Comment(string comment)
    {
        _comment = comment;
        return this;
    }

    internal ColumnDefinition Build() =>
        new()
        {
            Name = _name,
            Type = _type,
            IsNullable = _isNullable,
            DefaultValue = _defaultValue,
            DefaultLqlExpression = _defaultLqlExpression,
            IsIdentity = _isIdentity,
            IdentitySeed = _identitySeed,
            IdentityIncrement = _identityIncrement,
            ComputedExpression = _computedExpression,
            IsComputedPersisted = _isComputedPersisted,
            Collation = _collation,
            CheckConstraint = _checkConstraint,
            CheckConstraintName = _checkConstraintName,
            Comment = _comment,
        };
}
