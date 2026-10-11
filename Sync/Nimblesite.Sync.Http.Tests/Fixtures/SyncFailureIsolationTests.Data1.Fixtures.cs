// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Sync.Http.Tests;

public partial class SyncFailureIsolationTests
{
    private const string Data_InitializeDatabase_1 = """
        CREATE TABLE fhir_Encounter (
            Id TEXT PRIMARY KEY,
            PatientReference TEXT NOT NULL,
            Status TEXT NOT NULL,
            Class TEXT,
            ServiceType TEXT,
            ReasonCode TEXT,
            StartDate TEXT,
            EndDate TEXT,
            Version INTEGER DEFAULT 1
        );

        CREATE TABLE fhir_Practitioner (
            Id TEXT PRIMARY KEY,
            Identifier TEXT,
            Active INTEGER DEFAULT 1,
            NameFamily TEXT,
            NameGiven TEXT,
            Qualification TEXT,
            Specialty TEXT,
            TelecomEmail TEXT,
            TelecomPhone TEXT,
            Version INTEGER DEFAULT 1
        );

        CREATE TABLE fhir_Appointment (
            Id TEXT PRIMARY KEY,
            ServiceCategory TEXT,
            ServiceType TEXT,
            Priority TEXT,
            Description TEXT,
            Start TEXT,
            End TEXT,
            Status TEXT DEFAULT 'pending',
            PatientReference TEXT,
            PractitionerReference TEXT,
            Version INTEGER DEFAULT 1
        );
        """;

    private const string Data_CrossServiceSync_ClinicalToScheduling_ShouldSyncAllEntityTypes_1 = """
        CREATE TABLE IF NOT EXISTS fhir_Patient (
            Id TEXT PRIMARY KEY,
            Active INTEGER DEFAULT 1,
            GivenName TEXT,
            FamilyName TEXT,
            Gender TEXT
        );
        """;

    private const string Data_CrossServiceSync_ClinicalToScheduling_ShouldSyncAllEntityTypes_2 = """
        CREATE TABLE IF NOT EXISTS fhir_Patient (
            Id TEXT PRIMARY KEY,
            Active INTEGER DEFAULT 1,
            GivenName TEXT,
            FamilyName TEXT,
            Gender TEXT
        );
        """;
}
