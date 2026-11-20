using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.UnitTests;

public class TreatmentAdmissionTests
{
    [Theory]
    [InlineData(false, "EXPIRED")]
    [InlineData(true, "EXPIRED")]
    [InlineData(false, "CANCELLED")]
    [InlineData(true, "CANCELLED")]
    public async Task ReRegistrationKeepsPrescribedSessionNumber(bool grouped, string abandonedStatus)
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        db.TreatmentPlans.Single(p => p.PlanId == 1).PlannedSessionCount = 2;
        db.TreatmentSessions.Add(new TreatmentSession
        {
            PlanId = 1, SessionNumber = 1, Status = "COMPLETED", AssignedDoctorId = 1, PerformedBy = 1,
            CompletedAt = DateTime.UtcNow.AddDays(-3)
        });
        // Include existing data whose stored attempt number has already exceeded the prescription.
        db.TreatmentSessions.Add(new TreatmentSession
        {
            PlanId = 1, SessionNumber = 2, Status = abandonedStatus,
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(-2)
        });
        db.TreatmentSessions.Add(new TreatmentSession
        {
            PlanId = 1, SessionNumber = 3, Status = "REQUESTED",
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(-1)
        });
        await db.SaveChangesAsync();
        var service = new TreatmentWorkflowService(db);
        SessionSummary replacement;
        if (grouped)
        {
            var request = Request(); request.Items.RemoveAt(1);
            var admission = Assert.Single(await service.CreateAdmissionsAsync(Reception, request, default));
            replacement = Assert.Single(admission.Requests);
            Assert.Equal(2, Assert.Single((await service.AdmissionAsync(Reception, admission.Group.AdmissionId!.Value, default)).Requests).SessionNumber);
        }
        else
        {
            replacement = (await service.RequestAsync(Reception, 1, new RequestTreatment
            {
                AssignedDoctorId = 1, ScheduledDate = Request().ScheduledDate, Shift = "AFTERNOON", RowVersion = new byte[8]
            }, default)).Session;
        }
        Assert.Equal(2, replacement.SessionNumber);
        Assert.Equal(4, db.TreatmentSessions.Single(s => s.SessionId == replacement.SessionId).SessionNumber);
        Assert.Equal(4, await db.TreatmentSessions.CountAsync(s => s.PlanId == 1));
        Assert.Equal("EXPIRED", db.TreatmentSessions.Single(s => s.PlanId == 1 && s.SessionNumber == 3).Status);
        Assert.Equal(2, (await service.SessionAsync(Reception, replacement.SessionId, default)).Session.SessionNumber);
        var listed = await service.SessionsAsync(Reception, new SessionQuery(), false, default);
        Assert.Equal(2, listed.Items.Single(s => s.SessionId == replacement.SessionId).SessionNumber);
        var queue = await new ReceptionDashboardService(db).GetQuickQueuesAsync();
        Assert.Equal("02 / 02", queue.WaitingTreatments.Single(s => s.SessionId == replacement.SessionId).SessionInfo);
        Assert.Equal(1, (await service.PlanAsync(Reception, 1, default)).Plan.CompletedSessions);
        if (grouped) await service.UpdateAdmissionAsync(Doctor(), replacement.AdmissionId!.Value, Version, false, default);
        await service.StartAsync(Doctor(), replacement.SessionId, Version, default);
        var completed = await service.CompleteAsync(Doctor(), replacement.SessionId, Complete(1), default);
        Assert.Equal(2, completed.Session.SessionNumber);
        Assert.Equal(2, completed.TreatmentPlan.Plan.CompletedSessions);
        Assert.False(completed.TreatmentPlan.CanRequest);
        var history = await service.SessionsAsync(Doctor(), new SessionQuery { Status = "COMPLETED" }, true, default);
        Assert.Equal(new[] { 1, 2 }, history.Items.Where(s => s.PlanId == 1).Select(s => s.SessionNumber).Order().ToArray());
    }

    [Fact]
    public void PrescribedSessionNumberProjectionTranslatesToSqlServerWithoutConnecting()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaChecks;Integrated Security=true;TrustServerCertificate=true").Options);
        var field = typeof(TreatmentWorkflowService).GetField("SessionProjection",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var projection = (System.Linq.Expressions.Expression<Func<TreatmentSession, SessionSummary>>)field.GetValue(null)!;
        var sql = db.TreatmentSessions.Select(projection).ToQueryString();
        Assert.Contains("COUNT", sql);
        Assert.Contains("EXPIRED", sql);
        Assert.Contains("CANCELLED", sql);
    }

    [Fact]
    public void GroupAndLegacyUnionTranslatesToSqlServerWithoutConnecting()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaChecks;Integrated Security=true;TrustServerCertificate=true").Options);
        var method = typeof(TreatmentWorkflowService).GetMethod("AdmissionRows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var rows = (IQueryable)method.Invoke(new TreatmentWorkflowService(db), new object?[] { 1 })!;
        var row = System.Linq.Expressions.Expression.Parameter(rows.ElementType, "r");
        var status = System.Linq.Expressions.Expression.Equal(System.Linq.Expressions.Expression.Property(row, "Status"), System.Linq.Expressions.Expression.Constant("ACCEPTED"));
        var filtered = System.Linq.Expressions.Expression.Call(typeof(Queryable), nameof(Queryable.Where), [rows.ElementType], rows.Expression,
            System.Linq.Expressions.Expression.Quote(System.Linq.Expressions.Expression.Lambda(status, row)));
        var ordered = System.Linq.Expressions.Expression.Call(typeof(Queryable), nameof(Queryable.OrderByDescending), [rows.ElementType, typeof(DateTime)], filtered,
            System.Linq.Expressions.Expression.Quote(System.Linq.Expressions.Expression.Lambda(System.Linq.Expressions.Expression.Property(row, "CreatedAt"), row)));
        var skip = System.Linq.Expressions.Expression.Call(typeof(Queryable), nameof(Queryable.Skip), [rows.ElementType], ordered, System.Linq.Expressions.Expression.Constant(10));
        var take = System.Linq.Expressions.Expression.Call(typeof(Queryable), nameof(Queryable.Take), [rows.ElementType], skip, System.Linq.Expressions.Expression.Constant(10));
        var sql = rows.Provider.CreateQuery(take).ToQueryString();
        Assert.Contains("UNION ALL", sql); Assert.Contains("TreatmentAdmission", sql); Assert.Contains("TreatmentSession", sql);
        Assert.Contains("OFFSET", sql); Assert.Contains("FETCH NEXT", sql);
    }
    private static ClaimsPrincipal Actor(int id, string role) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)], "test"));
    private static ClaimsPrincipal Reception => Actor(1, RoleConstants.ReceptionStaff);
    private static ClaimsPrincipal Doctor(int id = 1) => Actor(id, RoleConstants.Doctor);
    private static VersionRequest Version => new() { RowVersion = new byte[8] };

    private static async Task Seed(ApplicationDbContext db)
    {
        var record = SchemaFixture.Record();
        record.Doctor.User!.IsActive = true;
        var role = new Role { RoleCode = RoleConstants.Doctor, RoleName = "Bác sĩ" };
        record.Doctor.User.UserRoles.Add(new UserRole { Role = role });
        var second = SchemaFixture.Record(2).Doctor;
        second.User!.IsActive = true;
        second.User.UserRoles.Add(new UserRole { Role = role });
        db.Doctors.Add(second);
        var service = new Service { ServiceCode = "DV1", ServiceName = "Châm cứu", Unit = "buổi", Price = 100, IsActive = true };
        var invoice = new Invoice { Record = record, PaymentStatus = "PAID", PaymentMethod = "CASH" };
        for (var id = 1; id <= 2; id++)
        {
            var plan = SchemaFixture.Plan(record); plan.PlanId = id; plan.PlanName = $"Liệu trình {id}"; plan.ConfirmedAt = DateTime.UtcNow;
            var detail = new TreatmentPlanDetail { PlanDetailId = id, Plan = plan, Service = service, PlannedQuantity = 3, UnitPrice = 100, ServiceNameSnapshot = "Châm cứu", UnitSnapshot = "buổi" };
            db.TreatmentPlanDetails.Add(detail);
            invoice.InvoiceDetails.Add(new InvoiceDetail { PlanDetail = detail, ItemType = "SERVICE", ItemName = "Châm cứu", Quantity = 3, UnitPrice = 100 });
        }
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
    }
    private static CreateTreatmentAdmission Request(int secondDoctor = 1) => new()
    {
        RegistrationKey = Guid.NewGuid(), PatientId = 1, ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), Shift = "AFTERNOON",
        Items = [new() { PlanId = 1, AssignedDoctorId = 1, RowVersion = new byte[8] }, new() { PlanId = 2, AssignedDoctorId = secondDoctor, RowVersion = new byte[8] }]
    };
    private static CompleteTreatment Complete(int detail) => new()
    {
        RowVersion = new byte[8], ResultNote = "Đáp ứng tốt", Items = [new() { PlanDetailId = detail, Quantity = 1 }]
    };

    [Fact]
    public async Task OneAcceptanceTwoIndependentStartsAndCompletionsWithSafeRetries()
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var service = new TreatmentWorkflowService(db);
        var request = Request();
        var group = Assert.Single(await service.CreateAdmissionsAsync(Reception, request, default));
        Assert.Equal(2, group.Requests.Count);
        Assert.Equal(1, await db.MedicalRecords.CountAsync());
        var replay = Assert.Single(await service.CreateAdmissionsAsync(Reception, request, default));
        Assert.Equal(group.Group.AdmissionId, replay.Group.AdmissionId);
        Assert.Equal(2, await db.TreatmentSessions.CountAsync());
        var id = group.Group.AdmissionId!.Value;
        var a = group.Requests.Single(s => s.PlanId == 1).SessionId;
        var b = group.Requests.Single(s => s.PlanId == 2).SessionId;
        await Assert.ThrowsAsync<ConflictException>(() => service.StartAsync(Doctor(), a, Version, default));
        var accepted = await service.UpdateAdmissionAsync(Doctor(), id, Version, false, default);
        var retry = await service.UpdateAdmissionAsync(Doctor(), id, Version, false, default);
        Assert.Equal(accepted.Group.AcceptedAt, retry.Group.AcceptedAt);
        Assert.All(retry.Requests, s => Assert.Equal("REQUESTED", s.Status));
        await service.StartAsync(Doctor(), a, Version, default);
        await service.StartAsync(Doctor(), a, Version, default);
        await Assert.ThrowsAsync<ConflictException>(() => service.StartAsync(Doctor(), b, Version, default));
        await service.CompleteAsync(Doctor(), a, Complete(1), default);
        await service.CompleteAsync(Doctor(), a, Complete(1), default);
        var middle = await service.AdmissionAsync(Doctor(), id, default);
        Assert.Equal(1, middle.Group.CompletedCount); Assert.Equal("ACCEPTED", middle.Group.Status);
        Assert.Equal(0, (await service.PlanAsync(Reception, 2, default)).Plan.CompletedSessions);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAdmissionAsync(Doctor(), id, Version, true, default));
        await service.StartAsync(Doctor(), b, Version, default);
        await service.CompleteAsync(Doctor(), b, Complete(2), default);
        var ended = await service.UpdateAdmissionAsync(Doctor(), id, Version, true, default);
        Assert.Equal("COMPLETED", ended.Group.Status); Assert.Equal(2, ended.Group.CompletedCount);
        Assert.Equal(2, await db.UsageHistories.CountAsync());
        Assert.Equal(2, await db.UsageHistories.Select(u => u.PlanDetailId).Distinct().CountAsync());
        Assert.NotNull(db.TreatmentSessions.Single(s => s.SessionId == a).CompletedAt);
    }

    [Fact]
    public async Task DoctorsHaveSeparateGroupsAndCannotActForEachOtherOrOverlapPatient()
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var service = new TreatmentWorkflowService(db);
        var groups = await service.CreateAdmissionsAsync(Reception, Request(2), default);
        Assert.Equal(2, groups.Count);
        var one = groups.Single(g => g.Group.DoctorId == 1); var two = groups.Single(g => g.Group.DoctorId == 2);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateAdmissionAsync(Doctor(), two.Group.AdmissionId!.Value, Version, false, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AdmissionAsync(Doctor(), two.Group.AdmissionId!.Value, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.StartAsync(Doctor(), two.Requests[0].SessionId, Version, default));
        foreach (var g in groups) await service.UpdateAdmissionAsync(Doctor(g.Group.DoctorId!.Value), g.Group.AdmissionId!.Value, Version, false, default);
        await service.StartAsync(Doctor(), one.Requests[0].SessionId, Version, default);
        await Assert.ThrowsAsync<ConflictException>(() => service.StartAsync(Doctor(2), two.Requests[0].SessionId, Version, default));
        var mine = await service.AdmissionsAsync(Doctor(), new SessionQuery(), default);
        Assert.Single(mine.Items); Assert.Equal(1, mine.Items[0].DoctorId);
    }

    [Theory]
    [InlineData("payment")]
    [InlineData("duplicate")]
    [InlineData("capacity")]
    [InlineData("doctor")]
    [InlineData("version")]
    public async Task InvalidItemNeverCreatesPartialRegistration(string problem)
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var request = Request();
        if (problem == "payment") db.InvoiceDetails.Single(d => d.PlanDetailId == 2).Quantity = 1;
        if (problem == "capacity") db.TreatmentPlans.Single(p => p.PlanId == 2).Status = "COMPLETED";
        if (problem == "doctor") request.Items[1].AssignedDoctorId = 999;
        if (problem == "version") request.Items[1].RowVersion = [1,2,3,4,5,6,7,8];
        if (problem == "duplicate") db.TreatmentSessions.Add(new TreatmentSession { PlanId = 2, SessionNumber = 1, Status = "REQUESTED", ScheduledDate = request.ScheduledDate });
        await db.SaveChangesAsync();
        var count = await db.TreatmentSessions.CountAsync();
        await Assert.ThrowsAnyAsync<ApiException>(() => new TreatmentWorkflowService(db).CreateAdmissionsAsync(Reception, request, default));
        Assert.Empty(await db.TreatmentAdmissions.ToListAsync());
        Assert.Equal(count, await db.TreatmentSessions.CountAsync());
    }

    [Fact]
    public async Task CancelledRemainderEndsIncompleteWithoutCountingAsPerformed()
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var service = new TreatmentWorkflowService(db);
        var g = Assert.Single(await service.CreateAdmissionsAsync(Reception, Request(), default));
        var id = g.Group.AdmissionId!.Value;
        await service.UpdateAdmissionAsync(Doctor(), id, Version, false, default);
        var a = g.Requests.Single(s => s.PlanId == 1).SessionId;
        var b = g.Requests.Single(s => s.PlanId == 2).SessionId;
        await service.StartAsync(Doctor(), a, Version, default);
        await service.CompleteAsync(Doctor(), a, Complete(1), default);
        await service.CancelAsync(Doctor(), b, new CancelTreatment { RowVersion = new byte[8], Reason = "Bệnh nhân không thực hiện" }, default);
        var ended = await service.UpdateAdmissionAsync(Doctor(), id, Version, true, default);
        Assert.Equal("ENDED_INCOMPLETE", ended.Group.Status); Assert.Equal(1, ended.Group.CompletedCount);
        Assert.Single(await db.UsageHistories.ToListAsync());
        Assert.Equal(0, (await service.PlanAsync(Reception, 2, default)).Plan.CompletedSessions);
    }

    [Fact]
    public async Task PendingOtherPlanDoesNotBlockAndLaterRegistrationIsNeverMerged()
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var service = new TreatmentWorkflowService(db);
        var first = Request(); first.Items.RemoveAt(1);
        var a = Assert.Single(await service.CreateAdmissionsAsync(Reception, first, default));
        Assert.True((await service.PlanAsync(Reception, 2, default)).CanRequest);
        var second = Request(); second.Items.RemoveAt(0);
        var b = Assert.Single(await service.CreateAdmissionsAsync(Reception, second, default));
        Assert.NotEqual(a.Group.AdmissionId, b.Group.AdmissionId);
        second.Note = "Đã đổi nội dung";
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAdmissionsAsync(Reception, second, default));
        Assert.Equal(2, await db.TreatmentAdmissions.CountAsync());
    }

    [Fact]
    public async Task LegacyRequestsStayIndependentAndCanStillStartAndFinish()
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var service = new TreatmentWorkflowService(db);
        var legacy = await service.RequestAsync(Reception, 1, new RequestTreatment { AssignedDoctorId = 1, RowVersion = new byte[8], ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), Shift = "AFTERNOON" }, default);
        var list = await service.AdmissionsAsync(Doctor(), new SessionQuery(), default);
        var row = Assert.Single(list.Items);
        Assert.Null(row.AdmissionId); Assert.Equal(legacy.Session.SessionId, row.LegacySessionId);
        await service.StartAsync(Doctor(), legacy.Session.SessionId, Version, default);
        await service.CompleteAsync(Doctor(), legacy.Session.SessionId, Complete(1), default);
        Assert.Empty(await db.TreatmentAdmissions.ToListAsync());
    }

    [Fact]
    public async Task DoctorExamAndPatientExamPreventTreatmentStartAndPaymentIsRechecked()
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var service = new TreatmentWorkflowService(db);
        var g = Assert.Single(await service.CreateAdmissionsAsync(Reception, Request(), default));
        await service.UpdateAdmissionAsync(Doctor(), g.Group.AdmissionId!.Value, Version, false, default);
        var record = db.MedicalRecords.Single(); record.Status = "IN_PROGRESS";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => service.StartAsync(Doctor(), g.Requests[0].SessionId, Version, default));
        record.Status = "COMPLETED"; db.Invoices.Single().PaymentStatus = "UNPAID"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => service.StartAsync(Doctor(), g.Requests[0].SessionId, Version, default));
        Assert.All(await db.TreatmentSessions.ToListAsync(), s => Assert.Equal("REQUESTED", s.Status));
    }

    [Fact]
    public async Task TreatmentInProgressBlocksExaminationStartForSamePatientWithAnotherDoctor()
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var service = new TreatmentWorkflowService(db);
        var request = Request(); request.Items.RemoveAt(1);
        var g = Assert.Single(await service.CreateAdmissionsAsync(Reception, request, default));
        await service.UpdateAdmissionAsync(Doctor(), g.Group.AdmissionId!.Value, Version, false, default);
        await service.StartAsync(Doctor(), g.Requests[0].SessionId, Version, default);
        var registration = new ExaminationRegistration { PatientId = 1, DoctorId = 2, RegisteredBy = 1,
            RegistrationCode = "KCB2", Status = "WAITING", BusinessDate = request.ScheduledDate!.Value, RegisteredAt = DateTime.UtcNow };
        db.ExaminationRegistrations.Add(registration); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => new ExaminationService(db).StartAsync(Doctor(2), registration.RegistrationId, default));
        Assert.Equal("WAITING", registration.Status);
        Assert.Equal(1, await db.MedicalRecords.CountAsync());
    }

    [Fact]
    public async Task ListFiltersAndPaginatesGroupsAndLegacyAtTenRows()
    {
        using var db = SchemaFixture.Create(); await Seed(db);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var plan = db.TreatmentPlans.Single(p => p.PlanId == 1);
        for (var i = 0; i < 12; i++)
        {
            var group = new TreatmentAdmission { AdmissionId = Guid.NewGuid(), RegistrationKey = Guid.NewGuid(), PatientId = 1,
                DoctorId = 1, CreatedBy = 1, CreatedAt = DateTime.UtcNow.AddMinutes(i), ScheduledDate = date, Shift = "AFTERNOON" };
            group.Sessions.Add(new TreatmentSession { Plan = plan, SessionNumber = i + 1, AssignedDoctorId = 1, Status = "CANCELLED" });
            db.TreatmentAdmissions.Add(group);
        }
        db.TreatmentSessions.Add(new TreatmentSession { Plan = plan, SessionNumber = 20, AssignedDoctorId = 1, Status = "CANCELLED", ScheduledDate = date });
        await db.SaveChangesAsync();
        var service = new TreatmentWorkflowService(db);
        var query = new SessionQuery { PageSize = 100, Search = "Patient", Status = "ENDED_INCOMPLETE", FromDate = date, ToDate = date };
        var first = await service.AdmissionsAsync(Doctor(), query, default);
        Assert.Equal(13, first.TotalCount); Assert.Equal(10, first.PageSize); Assert.Equal(10, first.Items.Count);
        query.PageNumber = 2;
        var second = await service.AdmissionsAsync(Doctor(), query, default);
        Assert.Equal(3, second.Items.Count);
        Assert.Single(first.Items.Concat(second.Items), g => g.LegacySessionId.HasValue);
        query.Search = "Không tồn tại";
        Assert.Empty((await service.AdmissionsAsync(Doctor(), query, default)).Items);
    }
}
