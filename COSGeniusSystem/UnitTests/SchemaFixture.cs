using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.UnitTests;

internal static class SchemaFixture
{
    internal static ApplicationDbContext Create() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    internal static MedicalRecord Record(int id = 1)
    {
        var user = new User { UserId = id, Username = $"user{id}", FullName = "Staff", PasswordHash = "hash" };
        var doctor = new Doctor { DoctorId = id, FullName = "Doctor", User = user, IsActive = true };
        var patient = new Patient { PatientId = id, FullName = "Patient" };
        var registration = new ExaminationRegistration
        {
            RegistrationId = id, Patient = patient, Doctor = doctor, RegisteredByNavigation = user,
            RegistrationCode = $"KCB{id:D5}", BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
            QueueNumber = id, RegisteredAt = DateTime.UtcNow, Status = "COMPLETED"
        };
        return new MedicalRecord
        {
            RecordId = id, Registration = registration, Patient = patient, Doctor = doctor,
            ExamDate = DateTime.UtcNow, Status = "COMPLETED", FinalDiagnosis = "Diagnosis"
        };
    }

    internal static TreatmentPlan Plan(MedicalRecord record) => new()
    {
        PlanId = 1, Record = record, Doctor = record.Doctor, PlanName = "Plan", TreatmentType = "COURSE",
        Status = "CONFIRMED", PlannedSessionCount = 3, CreatedAt = DateTime.UtcNow
    };
}
