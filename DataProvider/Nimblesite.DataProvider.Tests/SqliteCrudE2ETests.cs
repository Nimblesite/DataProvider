using System.Collections.Immutable;
// Implements [CI-TEST-SHARED-COMMAND].

using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: full CRUD workflows against real SQLite databases.
/// Each test is a complete user workflow with multiple operations and assertions.
/// </summary>
public sealed partial class SqliteCrudE2ETests : IDisposable
{
    private readonly SqliteTestDatabase _db = new("crud_e2e");
    private SqliteConnection _connection => _db.Connection;

    public SqliteCrudE2ETests()
    {
        CreateSchema();
    }

    public void Dispose() => _db.Dispose();

    private void CreateSchema()
    {
        DatabaseCommands.Execute(
            connection: _connection,
            configure: command => command.CommandText = Data_CreateSchema_1
        );
    }

    [Fact]
    public void FullPatientLifecycle_InsertQueryUpdateDelete_AllOperationsSucceed()
    {
        // Insert multiple patients
        var patient1Id = Guid.NewGuid().ToString();
        var patient2Id = Guid.NewGuid().ToString();
        var patient3Id = Guid.NewGuid().ToString();

        var insert1 = InsertPatient(
            id: patient1Id,
            name: "Alice Smith",
            age: 30,
            email: "alice@example.com",
            isActive: 1
        );
        var insert2 = InsertPatient(
            id: patient2Id,
            name: "Bob Jones",
            age: 45,
            email: "bob@example.com",
            isActive: 1
        );
        var insert3 = InsertPatient(
            id: patient3Id,
            name: "Charlie Brown",
            age: 60,
            email: DBNull.Value,
            isActive: 0
        );

        // Verify inserts succeeded
        Assert.Equal(1, global::Tests.Shared.ResultAssertions.ExactOk(actual: insert1).Value);
        Assert.Equal(1, global::Tests.Shared.ResultAssertions.ExactOk(actual: insert2).Value);
        Assert.Equal(1, global::Tests.Shared.ResultAssertions.ExactOk(actual: insert3).Value);

        // Query all patients
        var allPatients = _connection.Query<(string, string, int)>(
            sql: "SELECT Id, Name, Age FROM Patients ORDER BY Name",
            mapper: r => (r.GetString(0), r.GetString(1), r.GetInt32(2))
        );
        var patientsOk = global::Tests.Shared.ResultAssertions.ExactOk(actual: allPatients);
        Assert.Equal(3, patientsOk.Value.Count);
        Assert.Equal("Alice Smith", patientsOk.Value[0].Item2);
        Assert.Equal("Bob Jones", patientsOk.Value[1].Item2);
        Assert.Equal("Charlie Brown", patientsOk.Value[2].Item2);

        // Query with WHERE filter
        // Query with WHERE filter
        var activeOk = Ok(read: () =>
            _connection.Query<string>(
                sql: "SELECT Name FROM Patients WHERE IsActive = 1 ORDER BY Name",
                mapper: r => r.GetString(0)
            )
        );
        Assert.Equal(2, activeOk.Value.Count);
        Assert.Contains("Alice Smith", activeOk.Value);
        Assert.Contains("Bob Jones", activeOk.Value);

        // Count patients using Query instead of Scalar
        // Count patients using Query instead of Scalar
        var countOk = Ok(read: () =>
            _connection.Query<long>(
                sql: "SELECT COUNT(*) FROM Patients WHERE Age > @minAge",
                parameters: [new SqliteParameter("@minAge", 35)],
                mapper: r => r.GetInt64(0)
            )
        );
        Assert.Equal(2L, countOk.Value[0]);

        // Update a patient
        var updateResult = ExecuteParameters(
            connection: _connection,
            sql: "UPDATE Patients SET Age = @age, Email = @email WHERE Id = @id",
            values: [("@age", 31), ("@email", "alice.smith@example.com"), ("@id", patient1Id)]
        );
        Assert.Equal(1, global::Tests.Shared.ResultAssertions.ExactOk(actual: updateResult).Value);

        // Verify update
        // Verify update
        VerifyReusableWorkflow2(
            input0: patient1Id,
            value0: "SELECT Age FROM Patients WHERE Id = @id",
            value1: "@id",
            value3: 31L
        );

        // Delete a patient
        var deleteResult = ExecuteParameters(
            connection: _connection,
            sql: "DELETE FROM Patients WHERE Id = @id",
            values: [("@id", patient3Id)]
        );
        Assert.Equal(1, global::Tests.Shared.ResultAssertions.ExactOk(actual: deleteResult).Value);

        // Verify delete
        // Verify delete
        var finalCountOk = Ok(read: () =>
            _connection.Query<long>(
                sql: "SELECT COUNT(*) FROM Patients",
                mapper: r => r.GetInt64(0)
            )
        );
        Assert.Equal(2L, finalCountOk.Value[0]);
    }

    [Fact]
    public void MultiTableWorkflow_PatientsAppointmentsMedications_RelatedDataManagement()
    {
        // Insert patient
        var patientId = Guid.NewGuid().ToString();
        ExecuteParameters(
            connection: _connection,
            sql: "INSERT INTO Patients (Id, Name, Age, Email) VALUES (@id, @name, @age, @email)",
            values:
            [
                ("@id", patientId),
                ("@name", "Diana Prince"),
                ("@age", 35),
                ("@email", "diana@example.com"),
            ]
        );

        // Insert multiple appointments
        var apt1Id = Guid.NewGuid().ToString();
        var apt2Id = Guid.NewGuid().ToString();
        var apt3Id = Guid.NewGuid().ToString();
        InsertAppointment(
            id: apt1Id,
            patientId: patientId,
            date: "2026-01-15",
            notes: "Annual checkup"
        );
        InsertAppointment(id: apt2Id, patientId: patientId, date: "2026-02-20", notes: "Follow-up");
        InsertAppointment(
            id: apt3Id,
            patientId: patientId,
            date: "2026-03-10",
            notes: "Lab results review"
        );

        // Insert medications
        var med1Id = Guid.NewGuid().ToString();
        var med2Id = Guid.NewGuid().ToString();
        InsertMedication(id: med1Id, patientId: patientId, drug: "Aspirin", dosage: "100mg daily");
        InsertMedication(
            id: med2Id,
            patientId: patientId,
            drug: "Metformin",
            dosage: "500mg twice daily"
        );

        // Query with JOIN: patient + appointments
        var appointmentsOk = VerifySharedPhase1(
            input0: Data_MultiTableWorkflow_PatientsAppointmentsMedications_RelatedDataManagement_1,
            value3: 3
        );
        Assert.All(appointmentsOk.Value, a => Assert.Equal("Diana Prince", a.Item1));
        Assert.Equal("2026-01-15", appointmentsOk.Value[0].Item2);
        Assert.Equal("Annual checkup", appointmentsOk.Value[0].Item3);
        Assert.Equal("2026-03-10", appointmentsOk.Value[2].Item2);

        // Query with JOIN: patient + medications
        var medsOk = VerifySharedPhase1(
            input0: Data_MultiTableWorkflow_PatientsAppointmentsMedications_RelatedDataManagement_2,
            value3: 2
        );
        Assert.Equal("Aspirin", medsOk.Value[0].Item2);
        Assert.Equal("Metformin", medsOk.Value[1].Item2);

        // Aggregate: count appointments per patient using Query
        // Aggregate: count appointments per patient using Query
        VerifyReusableWorkflow2(
            input0: patientId,
            value0: "SELECT COUNT(*) FROM Appointments WHERE PatientId = @pid",
            value1: "@pid",
            value3: 3L
        );

        // Delete appointment and verify cascade-like behavior
        ExecuteParameters(
            connection: _connection,
            sql: "DELETE FROM Appointments WHERE Id = @id",
            values: [("@id", apt1Id)]
        );

        VerifyReusableWorkflow2(
            input0: patientId,
            value0: "SELECT COUNT(*) FROM Appointments WHERE PatientId = @pid",
            value1: "@pid",
            value3: 2L
        );
    }

    [Fact]
    public void ParameterizedQueryWorkflow_VariousTypes_AllParameterTypesWork()
    {
        // Insert data with various SQLite types
        var id = Guid.NewGuid().ToString();
        InsertPatient(
            id: id,
            name: "Test O'Brien",
            age: 25,
            email: "test@example.com",
            isActive: 1
        );

        // Query with string parameter containing special characters
        // Query with string parameter containing special characters
        var namesOk = Ok(read: () =>
            _connection.Query<string>(
                sql: "SELECT Name FROM Patients WHERE Name = @name",
                parameters: [new SqliteParameter("@name", "Test O'Brien")],
                mapper: r => r.GetString(0)
            )
        );
        Assert.Single(namesOk.Value);
        Assert.Equal("Test O'Brien", namesOk.Value[0]);

        // Query with integer parameter
        // Query with integer parameter
        var ageOk = Ok(read: () =>
            _connection.Query<int>(
                sql: "SELECT Age FROM Patients WHERE Age >= @minAge AND Age <= @maxAge",
                parameters:
                [
                    new SqliteParameter("@minAge", 20),
                    new SqliteParameter("@maxAge", 30),
                ],
                mapper: r => r.GetInt32(0)
            )
        );
        Assert.Single(ageOk.Value);

        // Query with LIKE parameter
        // Query with LIKE parameter
        var likeOk = Ok(read: () =>
            _connection.Query<string>(
                sql: "SELECT Name FROM Patients WHERE Name LIKE @pattern",
                parameters: [new SqliteParameter("@pattern", "%O'Brien%")],
                mapper: r => r.GetString(0)
            )
        );
        Assert.Single(likeOk.Value);

        // Average age using Query instead of Scalar
        // Average age using Query instead of Scalar
        var avgOk = Ok(read: () =>
            _connection.Query<double>(
                sql: "SELECT AVG(CAST(Age AS REAL)) FROM Patients WHERE IsActive = @active",
                parameters: [new SqliteParameter("@active", 1)],
                mapper: r => r.GetDouble(0)
            )
        );
        Assert.Equal(25.0, avgOk.Value[0]);
    }

    [Fact]
    public void ErrorHandling_InvalidSqlAndConstraintViolations_ReturnsErrors()
    {
        // Invalid SQL returns error
        var badQuery = _connection.Query<string>(
            sql: "SELECT FROM NONEXISTENT_TABLE WHERE",
            mapper: r => r.GetString(0)
        );
        _ = global::Tests.Shared.ResultAssertions.Error(actual: badQuery);

        // Constraint violation: duplicate primary key
        var id = Guid.NewGuid().ToString();
        ExecuteParameters(
            connection: _connection,
            sql: "INSERT INTO Patients (Id, Name, Age) VALUES (@id, 'First', 30)",
            values: [("@id", id)]
        );
        var duplicate = ExecuteParameters(
            connection: _connection,
            sql: "INSERT INTO Patients (Id, Name, Age) VALUES (@id, 'Second', 40)",
            values: [("@id", id)]
        );
        var err = Assert.IsType<IntError>(duplicate);
        Assert.NotNull(err.Value.Message);
        Assert.NotEmpty(err.Value.Message);

        // Invalid query returns error
        var badScalar = _connection.Query<long>(
            sql: "SELECT * FROM NOWHERE",
            mapper: r => r.GetInt64(0)
        );
        _ = global::Tests.Shared.ResultAssertions.Error(actual: badScalar);

        // Invalid execute returns error
        global::Tests.Shared.ResultAssertions.Error(
            actual: _connection.Execute(sql: "DROP TABLE IMAGINARY_TABLE")
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal Result<IReadOnlyList<(string, string, string)>, Sql.Model.SqlError>.Ok<
        IReadOnlyList<(string, string, string)>,
        Sql.Model.SqlError
    > VerifySharedPhase1(string input0, int value3)
    {
        // Query with JOIN: patient + appointments
        var joinResult = _connection.Query<(string, string, string)>(
            sql: input0,
            mapper: r => (r.GetString(0), r.GetString(1), r.GetString(2))
        );
        var appointmentsOk = global::Tests.Shared.ResultAssertions.ExactOk(actual: joinResult);
        Assert.Equal(value3, appointmentsOk.Value.Count);

        return appointmentsOk;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private Outcome.Result<int, SqlError> InsertPatient(
        string id,
        string name,
        int age,
        object? email,
        int isActive
    ) =>
        ExecuteParameters(
            connection: _connection,
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, @active)",
            values:
            [
                ("@id", id),
                ("@name", name),
                ("@age", age),
                ("@email", email),
                ("@active", isActive),
            ]
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void InsertAppointment(string id, string patientId, string date, string notes) =>
        ExecuteParameters(
            connection: _connection,
            sql: "INSERT INTO Appointments (Id, PatientId, AppointmentDate, Notes) VALUES (@id, @pid, @date, @notes)",
            values: [("@id", id), ("@pid", patientId), ("@date", date), ("@notes", notes)]
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void InsertMedication(string id, string patientId, string drug, string dosage) =>
        ExecuteParameters(
            connection: _connection,
            sql: "INSERT INTO Medications (Id, PatientId, DrugName, Dosage) VALUES (@id, @pid, @drug, @dosage)",
            values: [("@id", id), ("@pid", patientId), ("@drug", drug), ("@dosage", dosage)]
        );

    // Implements [DP-TEST-SHARED-PARAMETER-EXECUTION].
    private static Outcome.Result<int, SqlError> ExecuteParameters(
        SqliteConnection connection,
        string sql,
        System.Collections.Immutable.ImmutableArray<(string Name, object? Value)> values
    ) =>
        connection.Execute(
            sql: sql,
            parameters: values
                .Select(value => new SqliteParameter(name: value.Name, value: value.Value))
                .ToImmutableArray()
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifyReusableWorkflow2(string input0, string value0, string value1, long value3)
    {
        // Verify update
        // Verify update
        var updatedAgeOk = Ok(read: () =>
            _connection.Query<long>(
                sql: value0,
                parameters: [new SqliteParameter(value1, input0)],
                mapper: r => r.GetInt64(0)
            )
        );
        Assert.Equal(value3, updatedAgeOk.Value[0]);
    }
}
