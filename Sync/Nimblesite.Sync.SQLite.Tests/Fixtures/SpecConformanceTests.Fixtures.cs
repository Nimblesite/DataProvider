// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Tests.Shared;
using Xunit;

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SpecConformanceTests
{
    private global::Nimblesite.Sync.Core.SyncLogEntry Fixture_Spec15_4_BatchHash_IsConsistent_1() =>
        new SyncLogEntry(
            1,
            "Person",
            "{\"Id\":\"1\"}",
            SyncOperation.Insert,
            "{\"Id\":\"1\",\"Name\":\"Alice\"}",
            "origin-1",
            "2025-01-01T00:00:00.000Z"
        );
}
