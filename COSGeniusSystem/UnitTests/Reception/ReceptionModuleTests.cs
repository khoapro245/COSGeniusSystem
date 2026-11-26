using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public class ReceptionModuleTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private static ApplicationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ReceptionTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ClaimsPrincipal ReceptionActor => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, "1"),
        new Claim(ClaimTypes.Role, RoleConstants.ReceptionStaff)
    }, "test"));

    // SchemaFixture supplies only the existing entity graph, not a database connection.
    private static async Task AddWaitingRegistrationsAsync(ApplicationDbContext db, int count)
    {
        var record = SchemaFixture.Record();
        record.Registration.Status = "COMPLETED";
        record.Registration.BusinessDate = Today;
        db.MedicalRecords.Add(record);
        var now = DateTime.UtcNow;
        for (int i = 0; i < count; i++)
        {
            db.ExaminationRegistrations.Add(new ExaminationRegistration
            {
                RegistrationId = 100 + i,
                Patient = record.Patient,
                Doctor = record.Doctor,
                RegisteredByNavigation = record.Doctor.User!,
                RegistrationCode = $"KCB{100 + i:D5}",
                BusinessDate = Today,
                QueueNumber = i + 1,
                RegisteredAt = now.AddMinutes(-count + i),
                Status = "WAITING"
            });
        }
        await db.SaveChangesAsync();
    }
    #region GetTodayStatsAsync

    [Fact]
    public async Task Stats_UseRegistrationAndSessionStatus_InsteadOfDiagnosisOrNotes()
    {
        using var db = CreateDb();
        var record = SchemaFixture.Record();
        record.Registration.Status = "WAITING";
        record.FinalDiagnosis = "Chờ khám";
        var plan = SchemaFixture.Plan(record);
        var date = Today;
        record.Registration.BusinessDate = date;
        db.TreatmentPlans.Add(plan);
        db.TreatmentSessions.AddRange(
            new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1, ScheduledDate = date, Status = "REQUESTED", ResultNote = "Has a note" },
            new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2, ScheduledDate = date, Status = "COMPLETED" },
            new TreatmentSession { SessionId = 3, Plan = plan, SessionNumber = 3, ScheduledDate = date, Status = "CANCELLED" });
        await db.SaveChangesAsync();
        var result = await new ReceptionDashboardService(db).GetTodayStatsAsync();
        Assert.Equal(1, result.WaitingExaminationCount);
        Assert.Equal(1, result.WaitingTreatmentCount);
        Assert.Equal(1, result.CompletedVisitsCount);
        Assert.Equal(3, result.TodayTotalVisits);
        Assert.Equal(1, result.NewExaminationCount);
        Assert.Equal(2, result.TreatmentCount);
    }

    [Fact]
    public async Task Stats_EmptyDatabase_ReturnsZeroCounts()
    {
        using var db = CreateDb();
        var result = await new ReceptionDashboardService(db).GetTodayStatsAsync();
        Assert.Equal(0, result.TodayTotalVisits);
        Assert.Equal(0, result.WaitingExaminationCount);
        Assert.Equal(0, result.WaitingTreatmentCount);
        Assert.Equal(0, result.CompletedVisitsCount);
        Assert.Equal(0, result.NewExaminationCount);
        Assert.Equal(0, result.TreatmentCount);
    }

    [Fact]
    public async Task Stats_RegistrationsAndSessionsOnOtherDates_AreExcluded()
    {
        using var db = CreateDb();
        var record = SchemaFixture.Record();
        record.Registration.BusinessDate = Today.AddDays(-1);
        record.Registration.Status = "WAITING";
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        db.TreatmentSessions.Add(new TreatmentSession
        {
            SessionId = 1, Plan = plan, SessionNumber = 1,
            ScheduledDate = Today.AddDays(1), Status = "REQUESTED"
        });
        await db.SaveChangesAsync();
        var result = await new ReceptionDashboardService(db).GetTodayStatsAsync();
        Assert.Equal(0, result.TodayTotalVisits);
        Assert.Equal(0, result.WaitingExaminationCount);
        Assert.Equal(0, result.WaitingTreatmentCount);
        Assert.Equal(0, result.CompletedVisitsCount);
        Assert.Equal(0, result.NewExaminationCount);
        Assert.Equal(0, result.TreatmentCount);
    }

    #endregion

    #region GetQuickQueuesAsync

    [Fact]
    public async Task Queues_IncludeWaitingRegistrationWithoutMedicalRecord_AndRequestedSessionWithoutUsage()
    {
        using var db = CreateDb();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        var waiting = new ExaminationRegistration
        {
            RegistrationId = 2, Patient = record.Patient, Doctor = record.Doctor,
            RegisteredByNavigation = record.Doctor.User!, RegistrationCode = "KCB00002",
            BusinessDate = Today, QueueNumber = 2,
            RegisteredAt = DateTime.UtcNow, Status = "WAITING", IsPriority = true
        };
        db.ExaminationRegistrations.Add(waiting);
        db.TreatmentSessions.Add(new TreatmentSession
        {
            SessionId = 1, Plan = plan, SessionNumber = 1, ScheduledDate = waiting.BusinessDate,
            Status = "REQUESTED", AssignedDoctor = record.Doctor, RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var result = await new ReceptionDashboardService(db).GetQuickQueuesAsync();
        Assert.Equal(2, Assert.Single(result.WaitingExaminations).RegistrationId);
        Assert.Equal(1, Assert.Single(result.WaitingTreatments).SessionId);
    }

    [Fact]
    public async Task Queues_EmptyDatabase_ReturnsEmptyLists()
    {
        using var db = CreateDb();
        var result = await new ReceptionDashboardService(db).GetQuickQueuesAsync();
        Assert.Empty(result.WaitingExaminations);
        Assert.Empty(result.WaitingTreatments);
    }

    [Fact]
    public async Task Queues_OrdersPriorityThenReceptionTimeAndAppliesTop()
    {
        using var db = CreateDb();
        await AddWaitingRegistrationsAsync(db, 3);
        var registrations = await db.ExaminationRegistrations
            .Where(r => r.RegistrationId >= 100).OrderBy(r => r.RegistrationId).ToListAsync();
        registrations[0].IsPriority = false;
        registrations[1].IsPriority = true;
        registrations[2].IsPriority = true;
        await db.SaveChangesAsync();
        var result = await new ReceptionDashboardService(db).GetQuickQueuesAsync(2);
        Assert.Equal(new[] { 101, 102 }, result.WaitingExaminations.Select(r => r.RegistrationId).ToArray());
        Assert.Equal(new[] { 1, 2 }, result.WaitingExaminations.Select(r => r.Stt).ToArray());
        Assert.All(result.WaitingExaminations, r => Assert.True(r.IsPriority));
    }

    [Fact]
    public async Task Queues_OtherDatesAndNonWaitingStatuses_AreExcluded()
    {
        using var db = CreateDb();
        await AddWaitingRegistrationsAsync(db, 3);
        var registrations = await db.ExaminationRegistrations
            .Where(r => r.RegistrationId >= 100).OrderBy(r => r.RegistrationId).ToListAsync();
        registrations[0].BusinessDate = Today.AddDays(-1);
        registrations[1].Status = "IN_PROGRESS";
        await db.SaveChangesAsync();
        var result = await new ReceptionDashboardService(db).GetQuickQueuesAsync();
        Assert.Equal(102, Assert.Single(result.WaitingExaminations).RegistrationId);
        Assert.Empty(result.WaitingTreatments);
    }

    [Fact]
    public async Task Queues_NonPositiveTop_UsesDefaultLimit()
    {
        using var db = CreateDb();
        await AddWaitingRegistrationsAsync(db, 4);
        var result = await new ReceptionDashboardService(db).GetQuickQueuesAsync(0);
        Assert.Equal(3, result.WaitingExaminations.Count);
        Assert.Equal(new[] { 100, 101, 102 }, result.WaitingExaminations.Select(r => r.RegistrationId).ToArray());
    }

    #endregion

    #region ExaminationService.CancelAsync

    [Fact]
    public async Task CancelExamTicket_WhenWaiting_SucceedsAndSetsAudit()
    {
        using var db = CreateDb();
        var reg = SchemaFixture.Registration();
        reg.Status = "WAITING";
        reg.Note = "Ghi chú ban đầu";
        db.ExaminationRegistrations.Add(reg);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var result = await service.CancelAsync(ReceptionActor, reg.RegistrationId,
            new CancelTreatment { RowVersion = reg.RowVersion, Reason = "Bệnh nhân có việc bận xin về" }, default);

        Assert.NotNull(result);
        Assert.Equal("CANCELLED", result.Status);

        var saved = await db.ExaminationRegistrations.SingleAsync(r => r.RegistrationId == reg.RegistrationId);
        Assert.Equal("CANCELLED", saved.Status);
        Assert.NotNull(saved.CancelledAt);
        Assert.Contains("Bệnh nhân có việc bận xin về", saved.Note);
    }

    [Fact]
    public async Task CancelExamTicket_WhenInProgress_ThrowsConflictException()
    {
        using var db = CreateDb();
        var reg = SchemaFixture.Registration();
        reg.Status = "IN_PROGRESS";
        db.ExaminationRegistrations.Add(reg);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CancelAsync(ReceptionActor, reg.RegistrationId,
                new CancelTreatment { RowVersion = reg.RowVersion, Reason = "Hủy ngang" }, default));

        Assert.Contains("Only a pending registration whose examination has not started can be cancelled", ex.Message);
    }

    [Fact]
    public async Task CancelExamTicket_WithoutReason_IsRejected()
    {
        using var db = CreateDb();
        var reg = SchemaFixture.Registration();
        reg.Status = "WAITING";
        db.ExaminationRegistrations.Add(reg);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        // Original contract test asserts rejection, not the exact exception type.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.CancelAsync(ReceptionActor, reg.RegistrationId,
                new CancelTreatment { RowVersion = reg.RowVersion, Reason = "" }, default));
    }

    #endregion

    #region ExaminationService.ListAsync

    [Fact]
    public async Task ListAsync_OrdersNewestFirstAndPreservesQueueNumbers()
    {
        using var db = CreateDb();
        var doctor = new Doctor { DoctorId = 1, FullName = "Bác sĩ Test", IsActive = true };
        var patient = new Patient { PatientId = 1, FullName = "Bệnh nhân Test", Phone = "0901234567" };
        var user = new User { UserId = 1, Username = "staff", FullName = "Staff", PasswordHash = "hash", IsActive = true };
        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        db.Users.Add(user);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var r1 = new ExaminationRegistration
        {
            RegistrationId = 101, Doctor = doctor, Patient = patient, RegisteredByNavigation = user,
            BusinessDate = date, Status = "COMPLETED", IsPriority = true,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-30), QueueNumber = 1
        };
        var r2 = new ExaminationRegistration
        {
            RegistrationId = 102, Doctor = doctor, Patient = patient, RegisteredByNavigation = user,
            BusinessDate = date, Status = "IN_PROGRESS", IsPriority = false,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-20), QueueNumber = 2
        };
        var r3 = new ExaminationRegistration
        {
            RegistrationId = 103, Doctor = doctor, Patient = patient, RegisteredByNavigation = user,
            BusinessDate = date, Status = "WAITING", IsPriority = true,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-10), QueueNumber = 3
        };
        var r4 = new ExaminationRegistration
        {
            RegistrationId = 104, Doctor = doctor, Patient = patient, RegisteredByNavigation = user,
            BusinessDate = date, Status = "WAITING", IsPriority = false,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-15), QueueNumber = 4
        };
        db.ExaminationRegistrations.AddRange(r1, r2, r3, r4);
        await db.SaveChangesAsync();

        var service = new ExaminationService(db);
        var result = await service.ListAsync(ReceptionActor, new RegistrationQuery { PageNumber = 1, PageSize = 10 }, false, default);

        Assert.Equal(4, result.Items.Count);
        // 1st: newest registered (r3, -10 min)
        Assert.Equal(103, result.Items[0].RegistrationId);
        // 2nd: r4 (-15 min)
        Assert.Equal(104, result.Items[1].RegistrationId);
        // 3rd: r2 (-20 min)
        Assert.Equal(102, result.Items[2].RegistrationId);
        // 4th: oldest registered (r1, -30 min)
        Assert.Equal(101, result.Items[3].RegistrationId);

        // Queue numbers remain stable when list is ordered descending by registration time.
        Assert.Equal(3, result.Items[0].QueueNumber);
        Assert.Equal(4, result.Items[1].QueueNumber);
        Assert.Equal(2, result.Items[2].QueueNumber);
        Assert.Equal(1, result.Items[3].QueueNumber);
    }

    [Fact]
    public async Task ListAsync_EmptyDatabase_ReturnsEmptyPage()
    {
        using var db = CreateDb();
        var result = await new ExaminationService(db).ListAsync(ReceptionActor,
            new RegistrationQuery { PageNumber = 1, PageSize = 10 }, false, default);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListAsync_SecondPage_ReturnsRemainingRegistration()
    {
        using var db = CreateDb();
        await AddWaitingRegistrationsAsync(db, 3);
        // The fixture graph also supplies one completed registration.
        // Make it older than the waiting registrations for deterministic pagination.
        var anchor = await db.ExaminationRegistrations.SingleAsync(r => r.RegistrationId < 100);
        anchor.RegisteredAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
        var result = await new ExaminationService(db).ListAsync(ReceptionActor,
            new RegistrationQuery { PageNumber = 2, PageSize = 3 }, false, default);
        Assert.Equal(anchor.RegistrationId, Assert.Single(result.Items).RegistrationId);
    }

    #endregion

    #region TreatmentWorkflowService.CancelSessionTicketAsync

    [Fact]
    public async Task CancelTreatmentSession_WhenRequested_Succeeds()
    {
        using var db = CreateDb();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        var session = new TreatmentSession
        {
            SessionId = 50, Plan = plan, SessionNumber = 1,
            ScheduledDate = record.Registration.BusinessDate,
            Status = "REQUESTED", AssignedDoctor = record.Doctor,
            RequestedAt = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);
        var detail = new TreatmentPlanDetail
        {
            Plan = plan, ServiceId = 1, ServiceNameSnapshot = "Châm cứu",
            UnitSnapshot = "Lần", PlannedQuantity = 3, UnitPrice = 100
        };
        var invoice = new Invoice
        {
            Record = record, PaymentStatus = "PAID", PaymentMethod = "CASH",
            TotalAmount = 300, FinalAmount = 300,
            InvoiceDetails = [new InvoiceDetail
            {
                PlanDetail = detail, ItemType = "SERVICE", ItemName = "Châm cứu",
                Quantity = 3, UnitPrice = 100
            }]
        };
        db.TreatmentPlanDetails.Add(detail);
        db.Invoices.Add(invoice);
        var originalPlanStatus = plan.Status;
        var originalSessionCount = plan.PlannedSessionCount;
        await db.SaveChangesAsync();

        var service = new TreatmentWorkflowService(db);
        var result = await service.CancelSessionTicketAsync(ReceptionActor, 50,
            new CancelTicketRequest { CancelReason = "Khách xin dời lịch" }, default);

        Assert.NotNull(result);
        Assert.Equal("CANCELLED", result.Session.Status);

        db.ChangeTracker.Clear();
        var saved = await db.TreatmentSessions.AsNoTracking().SingleAsync(s => s.SessionId == 50);
        Assert.Equal("CANCELLED", saved.Status);
        Assert.Contains("Khách xin dời lịch", saved.ResultNote);
        Assert.Contains("user 1", saved.ResultNote);
        var savedInvoice = Assert.Single(await db.Invoices.AsNoTracking().ToListAsync());
        Assert.Equal("PAID", savedInvoice.PaymentStatus);
        Assert.Equal(300m, savedInvoice.TotalAmount);
        Assert.Equal(300m, savedInvoice.FinalAmount);
        var savedInvoiceLine = Assert.Single(await db.InvoiceDetails.AsNoTracking().ToListAsync());
        Assert.Equal(3m, savedInvoiceLine.Quantity);
        var savedPlan = await db.TreatmentPlans.AsNoTracking().SingleAsync(x => x.PlanId == plan.PlanId);
        Assert.Equal(originalPlanStatus, savedPlan.Status);
        Assert.Equal(originalSessionCount, savedPlan.PlannedSessionCount);
        var savedDetail = await db.TreatmentPlanDetails.AsNoTracking().SingleAsync(x => x.PlanDetailId == detail.PlanDetailId);
        Assert.Equal(3, savedDetail.PlannedQuantity);
        Assert.Empty(await db.UsageHistories.ToListAsync());
    }

    [Fact]
    public async Task CancelTreatmentSession_WhenInProgress_ThrowsBadRequestException()
    {
        using var db = CreateDb();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        var session = new TreatmentSession
        {
            SessionId = 51, Plan = plan, SessionNumber = 1,
            ScheduledDate = record.Registration.BusinessDate,
            Status = "IN_PROGRESS", AssignedDoctor = record.Doctor,
            StartedAt = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        var service = new TreatmentWorkflowService(db);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CancelSessionTicketAsync(ReceptionActor, 51,
                new CancelTicketRequest { CancelReason = "Hủy buổi trị liệu" }, default));

        Assert.Contains("The request cannot be cancelled after the doctor or technician has started treatment", ex.Message);
    }


    [Fact]
    public async Task CancelTreatmentSession_WithoutReason_ThrowsBadRequestAndPreservesStatus()
    {
        using var db = CreateDb();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentSessions.Add(new TreatmentSession
        {
            SessionId = 52, Plan = plan, SessionNumber = 1,
            ScheduledDate = Today, Status = "REQUESTED", AssignedDoctor = record.Doctor
        });
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<BadRequestException>(() =>
            new TreatmentWorkflowService(db).CancelSessionTicketAsync(ReceptionActor, 52,
                new CancelTicketRequest { CancelReason = "", Reason = "" }, default));

        Assert.Equal("A cancellation reason is required.", error.Message);
        db.ChangeTracker.Clear();
        var saved = await db.TreatmentSessions.AsNoTracking().SingleAsync(x => x.SessionId == 52);
        Assert.Equal("REQUESTED", saved.Status);
    }

    [Fact]
    public async Task CancelTreatmentSession_UnknownSession_ThrowsNotFound()
    {
        using var db = CreateDb();
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            new TreatmentWorkflowService(db).CancelSessionTicketAsync(ReceptionActor, 999,
                new CancelTicketRequest { CancelReason = "Reschedule" }, default));

        Assert.Equal("Treatment session was not found.", error.Message);
        Assert.Empty(await db.TreatmentSessions.ToListAsync());
    }

    [Fact]
    public async Task CancelTreatmentSession_RepeatedRequest_PreservesOriginalAudit()
    {
        using var db = CreateDb();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentSessions.Add(new TreatmentSession
        {
            SessionId = 53, Plan = plan, SessionNumber = 1,
            ScheduledDate = Today, Status = "REQUESTED", AssignedDoctor = record.Doctor,
            ResultNote = "Original note"
        });
        await db.SaveChangesAsync();
        var service = new TreatmentWorkflowService(db);
        await service.CancelSessionTicketAsync(ReceptionActor, 53,
            new CancelTicketRequest { CancelReason = "First cancellation" }, default);
        db.ChangeTracker.Clear();
        var first = await db.TreatmentSessions.AsNoTracking().SingleAsync(x => x.SessionId == 53);
        var originalAudit = first.ResultNote;

        var repeated = await service.CancelSessionTicketAsync(ReceptionActor, 53,
            new CancelTicketRequest { CancelReason = "Repeated cancellation" }, default);

        Assert.Equal("CANCELLED", repeated.Session.Status);
        db.ChangeTracker.Clear();
        var saved = Assert.Single(await db.TreatmentSessions.AsNoTracking().ToListAsync());
        Assert.Equal("CANCELLED", saved.Status);
        Assert.Equal(originalAudit, saved.ResultNote);
        Assert.Contains("Original note", saved.ResultNote);
        Assert.Contains("First cancellation", saved.ResultNote);
        Assert.DoesNotContain("Repeated cancellation", saved.ResultNote);
    }

    #endregion

    #region DoctorService.GetDoctorClinicOverviewAsync

    [Fact]
    public async Task Overview_SeparatesWaitingAndActiveTreatments_AndExcludesOtherDates()
    {
        using var db = CreateDb();
        var record = SchemaFixture.Record();
        record.Registration.Status = "WAITING";
        var plan = SchemaFixture.Plan(record);
        var date = Today;
        record.Registration.BusinessDate = date;
        db.TreatmentPlans.Add(plan);
        db.TreatmentSessions.AddRange(
            new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1, AssignedDoctorId = 1, ScheduledDate = date, Status = "REQUESTED" },
            new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2, AssignedDoctorId = 1, PerformedBy = 2, ScheduledDate = date, Status = "IN_PROGRESS" },
            new TreatmentSession { SessionId = 3, Plan = plan, SessionNumber = 3, AssignedDoctorId = 1, ScheduledDate = date.AddDays(1), Status = "REQUESTED" },
            new TreatmentSession { SessionId = 4, Plan = plan, SessionNumber = 4, AssignedDoctorId = 1, ScheduledDate = date, Status = "COMPLETED" });
        db.Doctors.Add(new Doctor { DoctorId = 2, FullName = "Treating doctor", IsActive = true });
        await db.SaveChangesAsync();

        var overview = await new DoctorService(db).GetDoctorClinicOverviewAsync(new DoctorClinicFilterQuery { BusinessDate = date });

        Assert.Equal(1, overview.WaitingExaminationCount);
        Assert.Equal(1, overview.WaitingTreatmentCount);
        var assigned = Assert.Single(overview.Doctors, d => d.DoctorId == 1);
        Assert.Equal(1, assigned.WaitingCount);
        Assert.Equal(1, assigned.WaitingTreatmentCount);
        Assert.Empty(assigned.CurrentTreatingPatients);
        var performer = Assert.Single(overview.Doctors, d => d.DoctorId == 2);
        Assert.Equal(0, performer.WaitingTreatmentCount);
        Assert.Equal("Patient (BN00001)", Assert.Single(performer.CurrentTreatingPatients));
    }


    [Fact]
    public async Task Overview_EmptyDatabase_ReturnsZeroCountsAndEmptyLists()
    {
        using var db = CreateDb();
        var result = await new DoctorService(db).GetDoctorClinicOverviewAsync(
            new DoctorClinicFilterQuery { BusinessDate = Today });

        Assert.Equal(0, result.TotalDoctorCount);
        Assert.Equal(0, result.OnDutyDoctorCount);
        Assert.Equal(0, result.WaitingExaminationCount);
        Assert.Equal(0, result.WaitingTreatmentCount);
        Assert.Empty(result.Doctors);
        Assert.Empty(result.Specializations);
    }

    [Fact]
    public async Task Overview_KeywordAndSpecialization_FilterDoctorsButKeepGlobalCounts()
    {
        using var db = CreateDb();
        db.Doctors.AddRange(
            new Doctor
            {
                DoctorId = 101, FullName = "Nguyen Van A",
                Specialization = "Y hoc co truyen", IsActive = true
            },
            new Doctor
            {
                DoctorId = 102, FullName = "Tran Thi B",
                Specialization = "Phuc hoi chuc nang", IsActive = false
            });
        await db.SaveChangesAsync();

        var result = await new DoctorService(db).GetDoctorClinicOverviewAsync(
            new DoctorClinicFilterQuery
            {
                BusinessDate = Today, Keyword = "NGUYEN", Specialization = "Y HOC"
            });

        Assert.Equal(101, Assert.Single(result.Doctors).DoctorId);
        Assert.Equal(2, result.TotalDoctorCount);
        Assert.Equal(1, result.OnDutyDoctorCount);
        Assert.Equal(2, result.Specializations.Count);
        Assert.Equal(0, result.WaitingExaminationCount);
        Assert.Equal(0, result.WaitingTreatmentCount);
    }

    #endregion
}
