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
        e.Emit(new MappingStart());
        Key(e, "name");
        Str(e, schema.Name);

        WriteSeq(e, "tables", schema.Tables, WriteTable);
        WriteSeq(e, "roles", schema.Roles, WriteRole);
        WriteSeq(e, "functions", schema.Functions, WriteFunction);
        WriteSeq(e, "grants", schema.Grants, WriteGrant);
        e.Emit(new MappingEnd());
    }

    private static void WriteTable(Emitter e, TableDefinition t)
    {
        e.Emit(new MappingStart());
        Key(e, "name");
        Str(e, t.Name);
        if (t.Schema != "public")
        {
            Key(e, "schema");
            Str(e, t.Schema);
        }
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
        e.Emit(new MappingStart());
        Key(e, "name");
        Str(e, trigger.Name);
        if (trigger.Timing != TriggerTiming.Before)
        {
            Key(e, "timing");
            Str(e, "After");
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
        if (!trigger.ForEachRow)
        {
            Key(e, "forEachRow");
            Bool(e, false);
        }
        StrOpt(e, "raiseWhen", trigger.RaiseWhenLql);
        StrOpt(e, "errorMessage", trigger.ErrorMessage);
        e.Emit(new MappingEnd());
    }

    private static void WriteColumn(Emitter e, ColumnDefinition c)
    {
        e.Emit(new MappingStart());
        Key(e, "name");
        Str(e, c.Name);
        Key(e, "type");
        Str(e, SchemaYamlScalars.Encode(c.Type));
        if (!c.IsNullable)
        {
            Key(e, "isNullable");
            Bool(e, false);
        }
        StrOpt(e, "defaultValue", c.DefaultValue);
        StrOpt(e, "defaultLqlExpression", c.DefaultLqlExpression);
        if (c.IsIdentity)
        {
            Key(e, "isIdentity");
            Bool(e, true);
        }
        if (c.IdentitySeed != 1)
        {
            Key(e, "identitySeed");
            Long(e, c.IdentitySeed);
        }
        if (c.IdentityIncrement != 1)
        {
            Key(e, "identityIncrement");
            Long(e, c.IdentityIncrement);
        }
        StrOpt(e, "computedExpression", c.ComputedExpression);
        if (c.IsComputedPersisted)
        {
            Key(e, "isComputedPersisted");
            Bool(e, true);
        }
        StrOpt(e, "collation", c.Collation);
        StrOpt(e, "checkConstraint", c.CheckConstraint);
        StrOpt(e, "checkConstraintName", c.CheckConstraintName);
        StrOpt(e, "comment", c.Comment);
        e.Emit(new MappingEnd());
    }

    private static void WriteIndex(Emitter e, IndexDefinition i)
    {
        e.Emit(new MappingStart());
        Key(e, "name");
        Str(e, i.Name);
        WriteStrSeq(e, "columns", i.Columns);
        WriteStrSeq(e, "expressions", i.Expressions);
        if (i.IsUnique)
        {
            Key(e, "isUnique");
            Bool(e, true);
        }
        StrOpt(e, "filter", i.Filter);
        e.Emit(new MappingEnd());
    }

    private static void WritePrimaryKey(Emitter e, PrimaryKeyDefinition pk)
    {
        e.Emit(new MappingStart());
        StrOpt(e, "name", pk.Name);
        WriteStrSeq(e, "columns", pk.Columns);
        e.Emit(new MappingEnd());
    }

    private static void WriteForeignKey(Emitter e, ForeignKeyDefinition fk)
    {
        e.Emit(new MappingStart());
        StrOpt(e, "name", fk.Name);
        WriteStrSeq(e, "columns", fk.Columns);
        Key(e, "referencedTable");
        Str(e, fk.ReferencedTable);
        if (fk.ReferencedSchema != "public")
        {
            Key(e, "referencedSchema");
            Str(e, fk.ReferencedSchema);
        }
        WriteStrSeq(e, "referencedColumns", fk.ReferencedColumns);
        if (fk.OnDelete != ForeignKeyAction.NoAction)
        {
            Key(e, "onDelete");
            Str(e, SchemaYamlScalars.Encode(fk.OnDelete));
        }
        if (fk.OnUpdate != ForeignKeyAction.NoAction)
        {
            Key(e, "onUpdate");
            Str(e, SchemaYamlScalars.Encode(fk.OnUpdate));
        }
        e.Emit(new MappingEnd());
    }

    private static void WriteUnique(Emitter e, UniqueConstraintDefinition u)
    {
        e.Emit(new MappingStart());
        StrOpt(e, "name", u.Name);
        WriteStrSeq(e, "columns", u.Columns);
        e.Emit(new MappingEnd());
    }

    private static void WriteCheck(Emitter e, CheckConstraintDefinition c)
    {
        e.Emit(new MappingStart());
        Key(e, "name");
        Str(e, c.Name);
        Key(e, "expression");
        Str(e, c.Expression);
        e.Emit(new MappingEnd());
    }

    private static void WriteRole(Emitter e, PostgresRoleDefinition r)
    {
        e.Emit(new MappingStart());
        Key(e, "name");
        Str(e, r.Name);
        if (r.Login)
        {
            Key(e, "login");
            Bool(e, true);
        }
        if (r.BypassRls)
        {
            Key(e, "bypassRls");
            Bool(e, true);
        }
        WriteStrSeq(e, "grantTo", r.GrantTo);
        e.Emit(new MappingEnd());
    }

    private static void WriteFunction(Emitter e, PostgresFunctionDefinition f)
    {
        e.Emit(new MappingStart());
        if (f.Schema != "public")
        {
            Key(e, "schema");
            Str(e, f.Schema);
        }
        Key(e, "name");
        Str(e, f.Name);
        WriteSeq(e, "arguments", f.Arguments, WriteFunctionArg);
        if (f.Returns != "void")
        {
            Key(e, "returns");
            Str(e, f.Returns);
        }
        if (f.Language != "sql")
        {
            Key(e, "language");
            Str(e, f.Language);
        }
        if (f.Volatility != "stable")
        {
            Key(e, "volatility");
            Str(e, f.Volatility);
        }
        if (f.SecurityDefiner)
        {
            Key(e, "securityDefiner");
            Bool(e, true);
        }
        if (!string.IsNullOrEmpty(f.Body))
        {
            Key(e, "body");
            Str(e, f.Body);
        }
        StrOpt(e, "bodyLql", f.BodyLql);
        WriteStrSeq(e, "executeRoles", f.ExecuteRoles);
        if (!f.RevokePublicExecute)
        {
            Key(e, "revokePublicExecute");
            Bool(e, false);
        }
        e.Emit(new MappingEnd());
    }

    private static void WriteFunctionArg(Emitter e, PostgresFunctionArgumentDefinition a)
    {
        e.Emit(new MappingStart());
        if (!string.IsNullOrEmpty(a.Name))
        {
            Key(e, "name");
            Str(e, a.Name);
        }
        Key(e, "type");
        Str(e, a.Type);
        e.Emit(new MappingEnd());
    }

    private static void WriteGrant(Emitter e, PostgresGrantDefinition g)
    {
        e.Emit(new MappingStart());
        if (g.Schema != "public")
        {
            Key(e, "schema");
            Str(e, g.Schema);
        }
        if (g.Target != PostgresGrantTarget.Table)
        {
            Key(e, "target");
            Str(e, SchemaYamlScalars.Encode(g.Target));
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
        if (!rls.Enabled)
        {
            Key(e, "enabled");
            Bool(e, false);
        }
        WriteSeq(e, "policies", rls.Policies, WritePolicy);
        if (rls.Forced)
        {
            Key(e, "forced");
            Bool(e, true);
        }
        e.Emit(new MappingEnd());
    }

    private static void WritePolicy(Emitter e, RlsPolicyDefinition p)
    {
        e.Emit(new MappingStart());
        Key(e, "name");
        Str(e, p.Name);
        // permissive defaults true and is omitted at that default; only a
        // restrictive (false) policy writes the key.
        if (!p.IsPermissive)
        {
            Key(e, "permissive");
            Bool(e, false);
        }
        if (!(p.Operations.Count == 1 && p.Operations[0] == RlsOperation.All))
        {
            Key(e, "operations");
            e.Emit(new SequenceStart(null, null, isImplicit: true, SequenceStyle.Block));
            foreach (var op in p.Operations)
            {
                Str(e, SchemaYamlScalars.Encode(op));
            }
            e.Emit(new SequenceEnd());
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
            Key(e, key);
            Str(e, value);
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
        if (items.Count == 0)
        {
            return;
        }
        Key(e, key);
        e.Emit(new SequenceStart(null, null, isImplicit: true, SequenceStyle.Block));
        foreach (var item in items)
        {
            Str(e, item);
        }
        e.Emit(new SequenceEnd());
    }
}
