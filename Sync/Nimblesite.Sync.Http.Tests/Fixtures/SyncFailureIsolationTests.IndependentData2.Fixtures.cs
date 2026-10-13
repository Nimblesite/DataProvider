// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Http.Tests;

public partial class SyncFailureIsolationTests
{
    private static global::System.Collections.Generic.Dictionary<
        string,
        (string table, string data)
    > Dataset_CrossServiceSync_ClinicalToScheduling_ShouldSyncAllEntityTypes_1() =>
        new Dictionary<string, (string table, string data)>
        {
            ["patient-123"] = ("fhir_Patient", ""),
            ["enc-101"] = (
                "fhir_Encounter",
                $"""
                INSERT INTO fhir_Encounter (Id, PatientReference, Status, Class)
                VALUES ('enc-101', 'patient-123', 'finished', 'ambulatory')
                """
            ),
        };
}
