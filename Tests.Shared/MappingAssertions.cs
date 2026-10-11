namespace Tests.Shared;

// Implements [CI-TEST-SHARED-MAPPED-PAYLOAD].
internal static class MappingAssertions
{
    internal static void Payload(
        MappingResult result,
        Action<global::System.Text.Json.JsonElement> verify
    )
    {
        var success = Xunit.Assert.IsType<MappingSuccess>(@object: result);
        var payload = Xunit.Assert.IsType<string>(@object: success.Entries[0].MappedPayload);
        using var document = global::System.Text.Json.JsonDocument.Parse(json: payload);
        verify(obj: document.RootElement);
    }

    // Implements [CI-TEST-SHARED-MAPPED-PAYLOAD].
    internal static void Customer(
        global::Nimblesite.Sync.Core.MappedEntry mapped,
        string pkSubstring
    )
    {
        Xunit.Assert.Equal(expected: "customer", actual: mapped.TargetTable);
        Xunit.Assert.Contains(expectedSubstring: "customer_id", actualString: mapped.TargetPkValue);
        Xunit.Assert.Contains(expectedSubstring: pkSubstring, actualString: mapped.TargetPkValue);
    }
}
