// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Tests;

public partial class SqliteCrudE2ETests
{
    private const string Data_CreateSchema_1 = """
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

    private const string Data_MultiTableWorkflow_PatientsAppointmentsMedications_RelatedDataManagement_1 = """
        SELECT p.Name, a.AppointmentDate, a.Notes
        FROM Patients p
        INNER JOIN Appointments a ON p.Id = a.PatientId
        ORDER BY a.AppointmentDate
        """;

    private const string Data_MultiTableWorkflow_PatientsAppointmentsMedications_RelatedDataManagement_2 = """
        SELECT p.Name, m.DrugName, m.Dosage
        FROM Patients p
        INNER JOIN Medications m ON p.Id = m.PatientId
        ORDER BY m.DrugName
        """;
}
