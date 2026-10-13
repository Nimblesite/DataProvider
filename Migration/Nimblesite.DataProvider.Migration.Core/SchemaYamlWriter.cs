using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Reflection-free YAML writer for <see cref="SchemaDefinition" />. Implements
/// [MIG-AOT-YAML]: emits via the low-level YamlDotNet <see cref="Emitter" /> +
/// event API (no reflection, Native AOT safe). Key names, ordering, aliases and
/// the set of omitted semantic-default values match the prior reflection-based
/// serializer so existing schema files and tests round-trip unchanged.
/// </summary>
internal static class SchemaYamlWriter
{
    internal static string Write(SchemaDefinition schema)
    {
        using var sw = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var emitter = new Emitter(sw);
        emitter.Emit(new StreamStart());
        emitter.Emit(new DocumentStart(null, null, isImplicit: true));
        WriteSchema(emitter, schema);
        emitter.Emit(new DocumentEnd(isImplicit: true));
        emitter.Emit(new StreamEnd());
        return sw.ToString();
    }

    private static void WriteSchema(Emitter e, SchemaDefinition schema)
    {
        StartNamedMapping(e: e, name: schema.Name);

        WriteSeq(e, "tables", schema.Tables, WriteTable);
        WriteSeq(e, "roles", schema.Roles, WriteRole);
        WriteSeq(e, "functions", schema.Functions, WriteFunction);
        WriteSeq(e, "grants", schema.Grants, WriteGrant);
        e.Emit(new MappingEnd());
    }

    private static void WriteTable(Emitter e, TableDefinition t)
    {
        StartNamedMapping(e: e, name: t.Name);
        StrExcept(e: e, key: "schema", value: t.Schema, defaultValue: "public");
        WriteSeq(e, "columns", t.Columns, WriteColumn);
        WriteSeq(e, "indexes", t.Indexes, WriteIndex);
        if (t.PrimaryKey is { } pk)
        {
            Key(e, "primaryKey");
            WritePrimaryKey(e, pk);
        }
        WriteSeq(e, "foreignKeys", t.ForeignKeys, WriteForeignKey);
        WriteSeq(e, "uniqueConstraints", t.UniqueConstraints, WriteUnique);
        WriteSeq(e, "checkConstraints", t.CheckConstraints, WriteCheck);
        StrOpt(e, "comment", t.Comment);
        if (t.RowLevelSecurity is { } rls)
        {
            Key(e, "rowLevelSecurity");
            WriteRls(e, rls);
        }
        WriteSeq(e, "triggers", t.Triggers, WriteTrigger);
        e.Emit(new MappingEnd());
    }

    private static void WriteTrigger(Emitter e, TriggerDefinition trigger)
    {
        StartNamedMapping(e: e, name: trigger.Name);
        if (trigger.Timing != TriggerTiming.Before)
        {
            StringField(e: e, key: "timing", value: "After");
        }
        WriteSeq(
            e,
            "events",
            trigger.Events,
            (emitter, item) =>
                Str(
                    emitter,
                    item switch
                    {
                        TriggerEvent.Update => "Update",
                        TriggerEvent.Delete => "Delete",
                        _ => "Insert",
                    }
                )
        );
        BoolExcept(e: e, key: "forEachRow", value: trigger.ForEachRow, defaultValue: true);
        StrOpt(e, "raiseWhen", trigger.RaiseWhenLql);
        StrOpt(e, "errorMessage", trigger.ErrorMessage);
        e.Emit(new MappingEnd());
    }

    private static void WriteColumn(Emitter e, ColumnDefinition c)
    {
        StartNamedMapping(e: e, name: c.Name);
        StringField(e: e, key: "type", value: SchemaYamlScalars.Encode(c.Type));
        BoolExcept(e: e, key: "isNullable", value: c.IsNullable, defaultValue: true);
        StrOpt(e, "defaultValue", c.DefaultValue);
        StrOpt(e, "defaultLqlExpression", c.DefaultLqlExpression);
        BoolExcept(e: e, key: "isIdentity", value: c.IsIdentity, defaultValue: false);
        LongExcept(e: e, key: "identitySeed", value: c.IdentitySeed, defaultValue: 1);
        LongExcept(e: e, key: "identityIncrement", value: c.IdentityIncrement, defaultValue: 1);
        StrOpt(e, "computedExpression", c.ComputedExpression);
        BoolExcept(
            e: e,
            key: "isComputedPersisted",
            value: c.IsComputedPersisted,
            defaultValue: false
        );
        StrOpt(e, "collation", c.Collation);
        StrOpt(e, "checkConstraint", c.CheckConstraint);
        StrOpt(e, "checkConstraintName", c.CheckConstraintName);
        StrOpt(e, "comment", c.Comment);
        e.Emit(new MappingEnd());
    }

    private static void WriteIndex(Emitter e, IndexDefinition i)
    {
        StartNamedMapping(e: e, name: i.Name);
        WriteStrSeq(e, "columns", i.Columns);
        WriteStrSeq(e, "expressions", i.Expressions);
        BoolExcept(e: e, key: "isUnique", value: i.IsUnique, defaultValue: false);
        StrOpt(e, "filter", i.Filter);
        e.Emit(new MappingEnd());
    }

    private static void WritePrimaryKey(Emitter e, PrimaryKeyDefinition pk)
    {
        StartNamedColumns(e: e, name: pk.Name, columns: pk.Columns);
        e.Emit(new MappingEnd());
    }

    private static void WriteForeignKey(Emitter e, ForeignKeyDefinition fk)
    {
        StartNamedColumns(e: e, name: fk.Name, columns: fk.Columns);
        StringField(e: e, key: "referencedTable", value: fk.ReferencedTable);
        StrExcept(
            e: e,
            key: "referencedSchema",
            value: fk.ReferencedSchema,
            defaultValue: "public"
        );
        WriteStrSeq(e, "referencedColumns", fk.ReferencedColumns);
        if (fk.OnDelete != ForeignKeyAction.NoAction)
        {
            StringField(e: e, key: "onDelete", value: SchemaYamlScalars.Encode(fk.OnDelete));
        }
        if (fk.OnUpdate != ForeignKeyAction.NoAction)
        {
            StringField(e: e, key: "onUpdate", value: SchemaYamlScalars.Encode(fk.OnUpdate));
        }
        e.Emit(new MappingEnd());
    }

    private static void WriteUnique(Emitter e, UniqueConstraintDefinition u)
    {
        StartNamedColumns(e: e, name: u.Name, columns: u.Columns);
        e.Emit(new MappingEnd());
    }

    private static void WriteCheck(Emitter e, CheckConstraintDefinition c)
    {
        StartNamedMapping(e: e, name: c.Name);
        StringField(e: e, key: "expression", value: c.Expression);
        e.Emit(new MappingEnd());
    }

    private static void WriteRole(Emitter e, PostgresRoleDefinition r)
    {
        StartNamedMapping(e: e, name: r.Name);
        BoolExcept(e: e, key: "login", value: r.Login, defaultValue: false);
        BoolExcept(e: e, key: "bypassRls", value: r.BypassRls, defaultValue: false);
        WriteStrSeq(e, "grantTo", r.GrantTo);
        e.Emit(new MappingEnd());
    }

    private static void WriteFunction(Emitter e, PostgresFunctionDefinition f)
    {
        StartSchemaMapping(e: e, schema: f.Schema);
        StringField(e: e, key: "name", value: f.Name);
        WriteSeq(e, "arguments", f.Arguments, WriteFunctionArg);
        StrExcept(e: e, key: "returns", value: f.Returns, defaultValue: "void");
        StrExcept(e: e, key: "language", value: f.Language, defaultValue: "sql");
        StrExcept(e: e, key: "volatility", value: f.Volatility, defaultValue: "stable");
        BoolExcept(e: e, key: "securityDefiner", value: f.SecurityDefiner, defaultValue: false);
        StrOpt(e, "body", f.Body);
        StrOpt(e, "bodyLql", f.BodyLql);
        WriteStrSeq(e, "executeRoles", f.ExecuteRoles);
        BoolExcept(
            e: e,
            key: "revokePublicExecute",
            value: f.RevokePublicExecute,
            defaultValue: true
        );
        e.Emit(new MappingEnd());
    }

    private static void WriteFunctionArg(Emitter e, PostgresFunctionArgumentDefinition a)
    {
        e.Emit(new MappingStart());
        StrOpt(e, "name", a.Name);
        StringField(e: e, key: "type", value: a.Type);
        e.Emit(new MappingEnd());
    }

    private static void WriteGrant(Emitter e, PostgresGrantDefinition g)
    {
        StartSchemaMapping(e: e, schema: g.Schema);
        if (g.Target != PostgresGrantTarget.Table)
        {
            StringField(e: e, key: "target", value: SchemaYamlScalars.Encode(g.Target));
        }
        StrOpt(e, "objectName", g.ObjectName);
        WriteStrSeq(e, "privileges", g.Privileges);
        WriteStrSeq(e, "roles", g.Roles);
        StrOpt(e, "runAs", g.RunAs);
        e.Emit(new MappingEnd());
    }

    private static void WriteRls(Emitter e, RlsPolicySetDefinition rls)
    {
        e.Emit(new MappingStart());
        // enabled defaults true and is omitted at that default (only false is
        // written); forced defaults false and is written only when true.
        BoolExcept(e: e, key: "enabled", value: rls.Enabled, defaultValue: true);
        WriteSeq(e, "policies", rls.Policies, WritePolicy);
        BoolExcept(e: e, key: "forced", value: rls.Forced, defaultValue: false);
        e.Emit(new MappingEnd());
    }

    private static void WritePolicy(Emitter e, RlsPolicyDefinition p)
    {
        StartNamedMapping(e: e, name: p.Name);
        // permissive defaults true and is omitted at that default; only a
        // restrictive (false) policy writes the key.
        BoolExcept(e: e, key: "permissive", value: p.IsPermissive, defaultValue: true);
        if (!(p.Operations.Count == 1 && p.Operations[0] == RlsOperation.All))
        {
            WriteSeq(
                e,
                "operations",
                p.Operations,
                (emitter, op) => Str(emitter, SchemaYamlScalars.Encode(op))
            );
        }
        WriteStrSeq(e, "roles", p.Roles);
        StrOpt(e, "using", p.UsingLql);
        StrOpt(e, "withCheck", p.WithCheckLql);
        StrOpt(e, "usingSql", p.UsingSql);
        StrOpt(e, "withCheckSql", p.WithCheckSql);
        e.Emit(new MappingEnd());
    }

    // ── Primitive emit helpers ───────────────────────────────────────────

    private static void Key(Emitter e, string name) => Str(e, name);

    private static void Str(Emitter e, string value)
    {
        // Force literal block style for multi-line values so newlines round-trip
        // verbatim and stay readable (LQL/SQL bodies); let the emitter choose for
        // single-line values.
        var style = value.Contains('\n', StringComparison.Ordinal)
            ? ScalarStyle.Literal
            : ScalarStyle.Any;
        e.Emit(
            new Scalar(
                AnchorName.Empty,
                TagName.Empty,
                value,
                style,
                isPlainImplicit: true,
                isQuotedImplicit: true
            )
        );
    }

    private static void Bool(Emitter e, bool value) => Plain(e, value ? "true" : "false");

    private static void Long(Emitter e, long value) =>
        Plain(e, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static void Plain(Emitter e, string value) =>
        e.Emit(
            new Scalar(
                AnchorName.Empty,
                TagName.Empty,
                value,
                ScalarStyle.Plain,
                isPlainImplicit: true,
                isQuotedImplicit: false
            )
        );

    private static void StrOpt(Emitter e, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            StringField(e: e, key: key, value: value);
        }
    }

    private static void WriteSeq<T>(
        Emitter e,
        string key,
        IReadOnlyList<T> items,
        Action<Emitter, T> writeItem
    )
    {
        if (items.Count == 0)
        {
            return;
        }
        Key(e, key);
        e.Emit(new SequenceStart(null, null, isImplicit: true, SequenceStyle.Block));
        foreach (var item in items)
        {
            writeItem(e, item);
        }
        e.Emit(new SequenceEnd());
    }

    private static void WriteStrSeq(Emitter e, string key, IReadOnlyList<string> items)
    {
        WriteSeq(e: e, key: key, items: items, writeItem: Str);
    }

    // Implements [MIG-YAML-SHARED-FIELD-EMISSION].
    private static void StartNamedMapping(Emitter e, string name)
    {
        e.Emit(new MappingStart());
        StringField(e: e, key: "name", value: name);
    }

    // Implements [MIG-YAML-SHARED-FIELD-EMISSION]: primary keys, unique
    // constraints and foreign keys all begin with an optional name and a column
    // sequence before their type-specific fields.
    private static void StartNamedColumns(Emitter e, string? name, IReadOnlyList<string> columns)
    {
        e.Emit(new MappingStart());
        StrOpt(e, "name", name);
        WriteStrSeq(e, "columns", columns);
    }

    // Implements [MIG-YAML-SHARED-FIELD-EMISSION].
    private static void StartSchemaMapping(Emitter e, string schema)
    {
        e.Emit(new MappingStart());
        StrExcept(e: e, key: "schema", value: schema, defaultValue: "public");
    }

    private static void StrExcept(Emitter e, string key, string value, string defaultValue)
    {
        ValueExcept(e: e, key: key, value: value, defaultValue: defaultValue, writeValue: Str);
    }

    private static void BoolExcept(Emitter e, string key, bool value, bool defaultValue)
    {
        ValueExcept(e: e, key: key, value: value, defaultValue: defaultValue, writeValue: Bool);
    }

    private static void LongExcept(Emitter e, string key, long value, long defaultValue)
    {
        ValueExcept(e: e, key: key, value: value, defaultValue: defaultValue, writeValue: Long);
    }

    // Implements [MIG-YAML-SHARED-FIELD-EMISSION].
    private static void StringField(Emitter e, string key, string value)
    {
        Key(e: e, name: key);
        Str(e: e, value: value);
    }

    // Implements [MIG-YAML-SHARED-FIELD-EMISSION].
    private static void ValueExcept<T>(
        Emitter e,
        string key,
        T value,
        T defaultValue,
        Action<Emitter, T> writeValue
    )
    {
        if (!EqualityComparer<T>.Default.Equals(x: value, y: defaultValue))
        {
            Key(e: e, name: key);
            writeValue(arg1: e, arg2: value);
        }
    }
}
