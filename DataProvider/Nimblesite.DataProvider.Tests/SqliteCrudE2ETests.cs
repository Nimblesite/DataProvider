using Microsoft.Data.Sqlite;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: full CRUD workflows against real SQLite databases.
/// Each test is a complete user workflow with multiple operations and assertions.
/// </summary>
public sealed class SqliteCrudE2ETests : IDisposable
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
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Patients (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Age INTEGER NOT NULL,
                Email TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE Appointments (
                Id TEXT PRIMARY KEY,
                PatientId TEXT NOT NULL,
                AppointmentDate TEXT NOT NULL,
                Notes TEXT,
                FOREIGN KEY (PatientId) REFERENCES Patients(Id)
            );
            CREATE TABLE Medications (
                Id TEXT PRIMARY KEY,
                PatientId TEXT NOT NULL,
                DrugName TEXT NOT NULL,
                Dosage TEXT NOT NULL,
                FOREIGN KEY (PatientId) REFERENCES Patients(Id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void FullPatientLifecycle_InsertQueryUpdateDelete_AllOperationsSucceed()
    {
        // Insert multiple patients
        var patient1Id = Guid.NewGuid().ToString();
        var patient2Id = Guid.NewGuid().ToString();
        var patient3Id = Guid.NewGuid().ToString();

        var insert1 = _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, 1)",
            parameters:
            [
                new SqliteParameter("@id", patient1Id),
                new SqliteParameter("@name", "Alice Smith"),
                new SqliteParameter("@age", 30),
                new SqliteParameter("@email", "alice@example.com"),
            ]
        );
        var insert2 = _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, 1)",
            parameters:
            [
                new SqliteParameter("@id", patient2Id),
                new SqliteParameter("@name", "Bob Jones"),
                new SqliteParameter("@age", 45),
                new SqliteParameter("@email", "bob@example.com"),
            ]
        );
        var insert3 = _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, 0)",
            parameters:
            [
                new SqliteParameter("@id", patient3Id),
                new SqliteParameter("@name", "Charlie Brown"),
                new SqliteParameter("@age", 60),
                new SqliteParameter("@email", DBNull.Value),
            ]
        );

        // Verify inserts succeeded
        var ok1 = Assert.IsType<IntOk>(insert1);
        Assert.Equal(1, ok1.Value);
        var ok2 = Assert.IsType<IntOk>(insert2);
        Assert.Equal(1, ok2.Value);
        var ok3 = Assert.IsType<IntOk>(insert3);
        Assert.Equal(1, ok3.Value);

        // Query all patients
        var allPatients = _connection.Query<(string, string, int)>(
            sql: "SELECT Id, Name, Age FROM Patients ORDER BY Name",
            mapper: r => (r.GetString(0), r.GetString(1), r.GetInt32(2))
        );
        var patientsOk = Assert.IsType<Result<IReadOnlyList<(string, string, int)>, SqlError>.Ok<
            IReadOnlyList<(string, string, int)>,
            SqlError
        >>(allPatients);
        Assert.Equal(3, patientsOk.Value.Count);
        Assert.Equal("Alice Smith", patientsOk.Value[0].Item2);
        Assert.Equal("Bob Jones", patientsOk.Value[1].Item2);
        Assert.Equal("Charlie Brown", patientsOk.Value[2].Item2);

        // Query with WHERE filter
        var activePatients = _connection.Query<string>(
            sql: "SELECT Name FROM Patients WHERE IsActive = 1 ORDER BY Name",
            mapper: r => r.GetString(0)
        );
        var activeOk = Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<
            IReadOnlyList<string>,
            SqlError
        >>(activePatients);
        Assert.Equal(2, activeOk.Value.Count);
        Assert.Contains("Alice Smith", activeOk.Value);
        Assert.Contains("Bob Jones", activeOk.Value);

        // Count patients using Query instead of Scalar
        var countResult = _connection.Query<long>(
            sql: "SELECT COUNT(*) FROM Patients WHERE Age > @minAge",
            parameters: [new SqliteParameter("@minAge", 35)],
            mapper: r => r.GetInt64(0)
        );
        var countOk = Assert.IsType<Result<IReadOnlyList<long>, SqlError>.Ok<
            IReadOnlyList<long>,
            SqlError
        >>(countResult);
        Assert.Equal(2L, countOk.Value[0]);

        // Update a patient
        var updateResult = _connection.Execute(
            sql: "UPDATE Patients SET Age = @age, Email = @email WHERE Id = @id",
            parameters:
            [
                new SqliteParameter("@age", 31),
                new SqliteParameter("@email", "alice.smith@example.com"),
                new SqliteParameter("@id", patient1Id),
            ]
        );
        var updateOk = Assert.IsType<IntOk>(updateResult);
        Assert.Equal(1, updateOk.Value);

        // Verify update
        var updatedAge = _connection.Query<long>(
            sql: "SELECT Age FROM Patients WHERE Id = @id",
            parameters: [new SqliteParameter("@id", patient1Id)],
            mapper: r => r.GetInt64(0)
        );
        var updatedAgeOk = Assert.IsType<Result<IReadOnlyList<long>, SqlError>.Ok<
            IReadOnlyList<long>,
            SqlError
        >>(updatedAge);
        Assert.Equal(31L, updatedAgeOk.Value[0]);

        // Delete a patient
        var deleteResult = _connection.Execute(
            sql: "DELETE FROM Patients WHERE Id = @id",
            parameters: [new SqliteParameter("@id", patient3Id)]
        );
        var deleteOk = Assert.IsType<IntOk>(deleteResult);
        Assert.Equal(1, deleteOk.Value);

        // Verify delete
        var finalCount = _connection.Query<long>(
            sql: "SELECT COUNT(*) FROM Patients",
            mapper: r => r.GetInt64(0)
        );
        var finalCountOk = Assert.IsType<Result<IReadOnlyList<long>, SqlError>.Ok<
            IReadOnlyList<long>,
            SqlError
        >>(finalCount);
        Assert.Equal(2L, finalCountOk.Value[0]);
    }

    [Fact]
    public void MultiTableWorkflow_PatientsAppointmentsMedications_RelatedDataManagement()
    {
        // Insert patient
        var patientId = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email) VALUES (@id, @name, @age, @email)",
            parameters:
            [
                new SqliteParameter("@id", patientId),
                new SqliteParameter("@name", "Diana Prince"),
                new SqliteParameter("@age", 35),
                new SqliteParameter("@email", "diana@example.com"),
            ]
        );

        // Insert multiple appointments
        var apt1Id = Guid.NewGuid().ToString();
        var apt2Id = Guid.NewGuid().ToString();
        var apt3Id = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Appointments (Id, PatientId, AppointmentDate, Notes) VALUES (@id, @pid, @date, @notes)",
            parameters:
            [
                new SqliteParameter("@id", apt1Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@date", "2026-01-15"),
                new SqliteParameter("@notes", "Annual checkup"),
            ]
        );
        _connection.Execute(
            sql: "INSERT INTO Appointments (Id, PatientId, AppointmentDate, Notes) VALUES (@id, @pid, @date, @notes)",
            parameters:
            [
                new SqliteParameter("@id", apt2Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@date", "2026-02-20"),
                new SqliteParameter("@notes", "Follow-up"),
            ]
        );
        _connection.Execute(
            sql: "INSERT INTO Appointments (Id, PatientId, AppointmentDate, Notes) VALUES (@id, @pid, @date, @notes)",
            parameters:
            [
                new SqliteParameter("@id", apt3Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@date", "2026-03-10"),
                new SqliteParameter("@notes", "Lab results review"),
            ]
        );

        // Insert medications
        var med1Id = Guid.NewGuid().ToString();
        var med2Id = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Medications (Id, PatientId, DrugName, Dosage) VALUES (@id, @pid, @drug, @dosage)",
            parameters:
            [
                new SqliteParameter("@id", med1Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@drug", "Aspirin"),
                new SqliteParameter("@dosage", "100mg daily"),
            ]
        );
        _connection.Execute(
            sql: "INSERT INTO Medications (Id, PatientId, DrugName, Dosage) VALUES (@id, @pid, @drug, @dosage)",
            parameters:
            [
                new SqliteParameter("@id", med2Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@drug", "Metformin"),
                new SqliteParameter("@dosage", "500mg twice daily"),
            ]
        );

        // Query with JOIN: patient + appointments
        var joinResult = _connection.Query<(string, string, string)>(
            sql: """
            SELECT p.Name, a.AppointmentDate, a.Notes
            FROM Patients p
            INNER JOIN Appointments a ON p.Id = a.PatientId
            ORDER BY a.AppointmentDate
            """,
            mapper: r => (r.GetString(0), r.GetString(1), r.GetString(2))
        );
        var appointmentsOk = Assert.IsType<Result<
            IReadOnlyList<(string, string, string)>,
            SqlError
        >.Ok<IReadOnlyList<(string, string, string)>, SqlError>>(joinResult);
        Assert.Equal(3, appointmentsOk.Value.Count);
        Assert.All(appointmentsOk.Value, a => Assert.Equal("Diana Prince", a.Item1));
        Assert.Equal("2026-01-15", appointmentsOk.Value[0].Item2);
        Assert.Equal("Annual checkup", appointmentsOk.Value[0].Item3);
        Assert.Equal("2026-03-10", appointmentsOk.Value[2].Item2);

        // Query with JOIN: patient + medications
        var medResult = _connection.Query<(string, string, string)>(
            sql: """
            SELECT p.Name, m.DrugName, m.Dosage
            FROM Patients p
            INNER JOIN Medications m ON p.Id = m.PatientId
            ORDER BY m.DrugName
            """,
            mapper: r => (r.GetString(0), r.GetString(1), r.GetString(2))
        );
        var medsOk = Assert.IsType<Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
            IReadOnlyList<(string, string, string)>,
            SqlError
        >>(medResult);
        Assert.Equal(2, medsOk.Value.Count);
        Assert.Equal("Aspirin", medsOk.Value[0].Item2);
        Assert.Equal("Metformin", medsOk.Value[1].Item2);

        // Aggregate: count appointments per patient using Query
        var aptCount = _connection.Query<long>(
            sql: "SELECT COUNT(*) FROM Appointments WHERE PatientId = @pid",
            parameters: [new SqliteParameter("@pid", patientId)],
            mapper: r => r.GetInt64(0)
        );
        var aptCountOk = Assert.IsType<Result<IReadOnlyList<long>, SqlError>.Ok<
            IReadOnlyList<long>,
            SqlError
        >>(aptCount);
        Assert.Equal(3L, aptCountOk.Value[0]);

        // Delete appointment and verify cascade-like behavior
        _connection.Execute(
            sql: "DELETE FROM Appointments WHERE Id = @id",
            parameters: [new SqliteParameter("@id", apt1Id)]
        );
        var remainingApts = _connection.Query<long>(
            sql: "SELECT COUNT(*) FROM Appointments WHERE PatientId = @pid",
            parameters: [new SqliteParameter("@pid", patientId)],
            mapper: r => r.GetInt64(0)
        );
        var remainingOk = Assert.IsType<Result<IReadOnlyList<long>, SqlError>.Ok<
            IReadOnlyList<long>,
            SqlError
        >>(remainingApts);
        Assert.Equal(2L, remainingOk.Value[0]);
    }

    [Fact]
    public void ParameterizedQueryWorkflow_VariousTypes_AllParameterTypesWork()
    {
        // Insert data with various SQLite types
        var id = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, @active)",
            parameters:
            [
                new SqliteParameter("@id", id),
                new SqliteParameter("@name", "Test O'Brien"),
                new SqliteParameter("@age", 25),
                new SqliteParameter("@email", "test@example.com"),
                new SqliteParameter("@active", 1),
            ]
        );

        // Query with string parameter containing special characters
        var result = _connection.Query<string>(
            sql: "SELECT Name FROM Patients WHERE Name = @name",
            parameters: [new SqliteParameter("@name", "Test O'Brien")],
            mapper: r => r.GetString(0)
        );
        var namesOk = Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<
            IReadOnlyList<string>,
            SqlError
        >>(result);
        Assert.Single(namesOk.Value);
        Assert.Equal("Test O'Brien", namesOk.Value[0]);

        // Query with integer parameter
        var ageResult = _connection.Query<int>(
            sql: "SELECT Age FROM Patients WHERE Age >= @minAge AND Age <= @maxAge",
            parameters: [new SqliteParameter("@minAge", 20), new SqliteParameter("@maxAge", 30)],
            mapper: r => r.GetInt32(0)
        );
        var ageOk = Assert.IsType<Result<IReadOnlyList<int>, SqlError>.Ok<
            IReadOnlyList<int>,
            SqlError
        >>(ageResult);
        Assert.Single(ageOk.Value);

        // Query with LIKE parameter
        var likeResult = _connection.Query<string>(
            sql: "SELECT Name FROM Patients WHERE Name LIKE @pattern",
            parameters: [new SqliteParameter("@pattern", "%O'Brien%")],
            mapper: r => r.GetString(0)
        );
        var likeOk = Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<
            IReadOnlyList<string>,
            SqlError
        >>(likeResult);
        Assert.Single(likeOk.Value);

        // Average age using Query instead of Scalar
        var avgAge = _connection.Query<double>(
            sql: "SELECT AVG(CAST(Age AS REAL)) FROM Patients WHERE IsActive = @active",
            parameters: [new SqliteParameter("@active", 1)],
            mapper: r => r.GetDouble(0)
        );
        var avgOk = Assert.IsType<Result<IReadOnlyList<double>, SqlError>.Ok<
            IReadOnlyList<double>,
            SqlError
        >>(avgAge);
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
        Assert.True(
            badQuery
                is Result<IReadOnlyList<string>, SqlError>.Error<IReadOnlyList<string>, SqlError>
        );

        // Constraint violation: duplicate primary key
        var id = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age) VALUES (@id, 'First', 30)",
            parameters: [new SqliteParameter("@id", id)]
        );
        var duplicate = _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age) VALUES (@id, 'Second', 40)",
            parameters: [new SqliteParameter("@id", id)]
        );
        var err = Assert.IsType<IntError>(duplicate);
        Assert.NotNull(err.Value.Message);
        Assert.NotEmpty(err.Value.Message);

        // Invalid query returns error
        var badScalar = _connection.Query<long>(
            sql: "SELECT * FROM NOWHERE",
            mapper: r => r.GetInt64(0)
        );
        Assert.True(
            badScalar is Result<IReadOnlyList<long>, SqlError>.Error<IReadOnlyList<long>, SqlError>
        );

        // Invalid execute returns error
        var badExec = _connection.Execute(sql: "DROP TABLE IMAGINARY_TABLE");
        Assert.True(badExec is IntError);
    }
}
