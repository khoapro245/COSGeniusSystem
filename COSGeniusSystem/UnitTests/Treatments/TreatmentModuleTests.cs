using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace COSGeniusSystem.UnitTests;

public class TreatmentModuleTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private static ClaimsPrincipal Actor(int id, string role) => new(new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)
    }, "test"));
    // Moq compares the actor by reference: use the SAME principal for setup and invocation.
    private static ClaimsPrincipal Reception { get; } = Actor(1, RoleConstants.ReceptionStaff);
    private static ClaimsPrincipal Doctor(int id = 1) => Actor(id, RoleConstants.Doctor);
    private static VersionRequest Version => new() { RowVersion = new byte[8] };
    private static byte[] Stale => new byte[] { 255, 0, 0, 0, 0, 0, 0, 0 };
    private static RequestTreatment Request() => new()
    {
        AssignedDoctorId = 1,
        ScheduledDate = Today,
        Shift = "AFTERNOON",
        RowVersion = new byte[8]
    };
    private static CompleteTreatment Outcome(int quantity = 1) => new()
    {
        RowVersion = new byte[8],
        ResultNote = "Improved",
        Items = [new() { PlanDetailId = 1, Quantity = quantity }]
    };
    private static CreateTreatmentServiceRequest NewService() => new()
    {
        ServiceCode = "DV002",
        ServiceName = "New service",
        Price = 200m,
        Unit = "session",
        IsActive = true
    };
    private static UpdateTreatmentServiceRequest EditService(decimal price = 200m) => new()
    {
        ServiceName = "Edited service",
        Price = price,
        Unit = "session",
        IsActive = true
    };

    private sealed class Scope : IDisposable
    {
        public ApplicationDbContext Db { get; }
        public TreatmentWorkflowService Work { get; }
        public TreatmentService Catalog { get; }
        public TreatmentPlan Plan { get; }
        public MedicalRecord Record { get; }
        public Scope()
        {
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"TreatmentTests_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            // Test-provider initialization only. No automatic version advancement on UPDATE.
            Db.SavingChanges += (_, _) =>
            {
                Db.ChangeTracker.DetectChanges();
                foreach (var e in Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
                    if (e.Metadata.FindProperty("RowVersion")?.ClrType == typeof(byte[])
                        && (e.Property("RowVersion").CurrentValue is not byte[] b || b.Length == 0))
                        e.Property("RowVersion").CurrentValue = new byte[8];
            };
            Record = SchemaFixture.Record();
            // Set scalar keys explicitly: do not read unset FKs from a detached fixture graph.
            Record.RecordId = 1;
            Record.Patient.PatientId = 1;
            Record.PatientId = 1;
            Record.Doctor.DoctorId = 1;
            Record.Doctor.User!.UserId = 1;
            Record.Doctor.UserId = 1;
            Record.DoctorId = 1;
            Record.Registration.RegistrationId = 1;
            Record.Registration.Patient = Record.Patient;
            Record.Registration.PatientId = 1;
            Record.Registration.Doctor = Record.Doctor;
            Record.Registration.DoctorId = 1;
            Record.Registration.RegisteredByNavigation = Record.Doctor.User;
            Record.Registration.RegisteredBy = 1;
            Record.RegistrationId = 1;
            Record.RowVersion = new byte[8];
            Record.Registration.RowVersion = new byte[8];
            Record.Status = Record.Registration.Status = "COMPLETED";
            Record.CompletedAt = Record.Registration.CompletedAt = DateTime.UtcNow;
            Record.Registration.BusinessDate = Today;
            Record.Registration.Session = "AFTERNOON";
            Record.Doctor.IsActive = true; Record.Doctor.User!.IsActive = true;
            Record.Doctor.User.UserRoles.Add(new UserRole
            {
                Role = new Role { RoleCode = RoleConstants.Doctor, RoleName = "Doctor" }
            });
            Plan = new TreatmentPlan
            {
                PlanId = 1,
                Record = Record,
                RecordId = 1,
                Doctor = Record.Doctor,
                DoctorId = 1,
                PlanCode = "LT001",
                PlanName = "Course",
                TreatmentType = "COURSE",
                PlannedSessionCount = 3,
                Status = "CONFIRMED",
                ConfirmedAt = DateTime.UtcNow,
                RowVersion = new byte[8]
            };
            var service = new COSGeniusSystem.Models.Service
            {
                ServiceId = 1,
                ServiceCode = "DV001",
                ServiceName = "Acupuncture",
                Unit = "session",
                Price = 100m,
                IsActive = true
            };
            var detail = new TreatmentPlanDetail
            {
                PlanDetailId = 1,
                Plan = Plan,
                PlanId = 1,
                Service = service,
                ServiceId = 1,
                ServiceNameSnapshot = "Acupuncture",
                UnitSnapshot = "session",
                UnitPrice = 100m,
                PlannedQuantity = 3
            };
            var invoice = new Invoice
            {
                InvoiceId = 1,
                Record = Record,
                RecordId = 1,
                PaymentStatus = "PAID",
                PaymentMethod = "CASH",
                RowVersion = new byte[8]
            };
            invoice.InvoiceDetails.Add(new InvoiceDetail
            {
                PlanDetail = detail,
                PlanDetailId = 1,
                InvoiceId = 1,
                ItemType = "SERVICE",
                ItemName = "Acupuncture",
                UnitPrice = 100m,
                Quantity = 3,
                SubTotal = 300m
            });
            Db.MedicalRecords.Add(Record);
            Db.Services.Add(service);
            Db.TreatmentPlans.Add(Plan);
            Db.TreatmentPlanDetails.Add(detail);
            Db.Invoices.Add(invoice);
            Work = new TreatmentWorkflowService(Db, new PatientService(Db));
            Catalog = new TreatmentService(Db);
        }
        public static async Task<Scope> CreateAsync()
        {
            var s = new Scope();
            await s.Db.SaveChangesAsync();
            // Fail at setup with a useful assertion instead of a later SingleAsync error.
            var savedPlan = await s.Db.TreatmentPlans.AsNoTracking().SingleAsync(p => p.PlanId == 1);
            var savedRecord = await s.Db.MedicalRecords.AsNoTracking().SingleAsync(r => r.RecordId == 1);
            Assert.Equal(1, savedPlan.RecordId); Assert.Equal(1, savedPlan.DoctorId);
            Assert.Equal(1, savedRecord.PatientId); Assert.Equal(1, savedRecord.DoctorId);
            Assert.Equal(1, savedRecord.RegistrationId);
            return s;
        }
        public async Task<TreatmentSession> SessionAsync(string status = "REQUESTED", int number = 1)
        {
            var session = new TreatmentSession
            {
                Plan = Plan,
                PlanId = 1,
                SessionNumber = number,
                AssignedDoctorId = 1,
                RequestedBy = 1,
                RequestedAt = DateTime.UtcNow,
                ScheduledDate = Today,
                Shift = "AFTERNOON",
                Status = status,
                RowVersion = new byte[8]
            };
            if (status is "IN_PROGRESS" or "COMPLETED") { session.PerformedBy = 1; session.StartedAt = DateTime.UtcNow; }
            if (status == "COMPLETED") session.CompletedAt = DateTime.UtcNow;
            Db.TreatmentSessions.Add(session); await Db.SaveChangesAsync(); return session;
        }
        public async Task RemoveInvoicesAsync()
        {
            Db.InvoiceDetails.RemoveRange(await Db.InvoiceDetails.ToListAsync());
            Db.Invoices.RemoveRange(await Db.Invoices.ToListAsync());
            await Db.SaveChangesAsync();
        }
        public BatchTreatmentRequest Batch(int patientId = 1) => new()
        {
            PatientId = patientId,
            ScheduledDate = Today,
            Shift = "AFTERNOON",
            Items = [new() { PlanId = 1, AssignedDoctorId = 1, RowVersion = new byte[8] }]
        };
        public CreateTreatmentAdmission LegacyBatch() => new()
        {
            PatientId = 1,
            ScheduledDate = Today,
            Shift = "AFTERNOON",
            RegistrationKey = Guid.NewGuid(),
            Items = [new() { PlanId = 1, AssignedDoctorId = 1, RowVersion = new byte[8] }]
        };
        public void Dispose() => Db.Dispose();
    }

    // UTC-TRT-001
    [Fact]
    public async Task GetActiveServicesAsync_FiltersInactiveAndOrdersCode()
    {
        using var s = await Scope.CreateAsync();
        s.Db.Services.Add(new() { ServiceId = 2, ServiceCode = "DV002", ServiceName = "Inactive", Unit = "session", Price = 200m, IsActive = false });
        await s.Db.SaveChangesAsync();
        var result = await s.Catalog.GetActiveServicesAsync();
        Assert.Equal(1, Assert.Single(result).ServiceId);
    }

    // UTC-TRT-002
    [Fact]
    public async Task GetActiveServicesAsync_NoActiveServices_ReturnsEmpty()
    {
        using var s = await Scope.CreateAsync();
        (await s.Db.Services.SingleAsync()).IsActive = false; await s.Db.SaveChangesAsync();
        Assert.Empty(await s.Catalog.GetActiveServicesAsync());
    }

    // UTC-TRT-003
    [Fact]
    public async Task GetActiveServicesAsync_MapsPriceAndUnit()
    {
        using var s = await Scope.CreateAsync();
        var result = Assert.Single(await s.Catalog.GetActiveServicesAsync());
        Assert.Equal("DV001", result.ServiceCode); Assert.Equal("Acupuncture", result.ServiceName);
        Assert.Equal(100m, result.Price); Assert.Equal("session", result.Unit); Assert.True(result.IsActive);
    }

    // UTC-TRT-004
    [Fact]
    public async Task GetServicesAsync_SearchMatchesName()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Catalog.GetServicesAsync(new() { Keyword = " acupuncture " });
        Assert.Equal(1, Assert.Single(result).ServiceId);
    }

    // UTC-TRT-005
    [Fact]
    public async Task GetServicesAsync_IncludeInactiveReturnsBothStatuses()
    {
        using var s = await Scope.CreateAsync();
        s.Db.Services.Add(new() { ServiceId = 2, ServiceCode = "DV002", ServiceName = "Inactive", Unit = "session", Price = 200m, IsActive = false });
        await s.Db.SaveChangesAsync();
        Assert.Equal(2, (await s.Catalog.GetServicesAsync(new() { IncludeInactive = true })).Count);
        Assert.False(Assert.Single(await s.Catalog.GetServicesAsync(new() { IsActive = false })).IsActive);
    }

    // UTC-TRT-006
    [Fact]
    public async Task GetServicesAsync_InvalidPagingUsesDefaults()
    {
        using var s = await Scope.CreateAsync();
        Assert.Single(await s.Catalog.GetServicesAsync(new() { Page = 0, PageSize = 0 }));
    }

    // UTC-TRT-007
    [Fact]
    public async Task GetServiceByIdAsync_ExistingServiceReturnsDto()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Catalog.GetServiceByIdAsync(1);
        Assert.NotNull(result); Assert.Equal(1, result.ServiceId); Assert.Equal(100m, result.Price);
    }

    // UTC-TRT-008
    [Fact]
    public async Task GetServiceByIdAsync_MissingServiceReturnsNull()
    {
        using var s = await Scope.CreateAsync();
        Assert.Null(await s.Catalog.GetServiceByIdAsync(999));
    }

    // UTC-TRT-009
    [Fact]
    public async Task GetServiceByIdAsync_InvalidIdOrNormalizedCode()
    {
        using var s = await Scope.CreateAsync();
        Assert.Null(await s.Catalog.GetServiceByIdAsync(0));
    }

    // UTC-TRT-010
    [Fact]
    public async Task GetServiceByCodeAsync_ExistingServiceReturnsDto()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Catalog.GetServiceByCodeAsync("DV001");
        Assert.NotNull(result); Assert.Equal(1, result.ServiceId); Assert.Equal(100m, result.Price);
    }

    // UTC-TRT-011
    [Fact]
    public async Task GetServiceByCodeAsync_MissingServiceReturnsNull()
    {
        using var s = await Scope.CreateAsync();
        Assert.Null(await s.Catalog.GetServiceByCodeAsync("UNKNOWN"));
    }

    // UTC-TRT-012
    [Fact]
    public async Task GetServiceByCodeAsync_InvalidIdOrNormalizedCode()
    {
        using var s = await Scope.CreateAsync();
        Assert.Equal(1, (await s.Catalog.GetServiceByCodeAsync(" dv001 "))!.ServiceId);
        Assert.Null(await s.Catalog.GetServiceByCodeAsync(" "));
    }

    // UTC-TRT-013
    [Fact]
    public async Task CreateServiceAsync_ValidRequestCreatesService()
    {
        using var s = await Scope.CreateAsync(); var request = NewService(); request.ServiceCode = null;
        var (dto, error) = await s.Catalog.CreateServiceAsync(request);
        Assert.Null(error); Assert.NotNull(dto); Assert.True(dto.IsActive); Assert.Equal(200m, dto.Price);
        Assert.False(string.IsNullOrWhiteSpace(dto.ServiceCode));
        s.Db.ChangeTracker.Clear(); Assert.Equal(2, await s.Db.Services.CountAsync());
    }

    // UTC-TRT-014
    [Fact]
    public async Task CreateServiceAsync_ZeroPriceReturnsError()
    {
        using var s = await Scope.CreateAsync(); var request = NewService(); request.Price = 0;
        var (dto, error) = await s.Catalog.CreateServiceAsync(request);
        Assert.Null(dto); Assert.Equal("Price must be greater than 0.", error);
        Assert.Equal(1, await s.Db.Services.CountAsync());
    }

    // UTC-TRT-015
    [Fact]
    public async Task CreateServiceAsync_DuplicateNameAndCodeReturnErrors()
    {
        using var s = await Scope.CreateAsync(); var request = NewService(); request.ServiceName = "acupuncture";
        var name = await s.Catalog.CreateServiceAsync(request);
        Assert.Null(name.Dto); Assert.Equal("Service name already exists.", name.ErrorMessage);
        request.ServiceName = "Unique name"; request.ServiceCode = " dv001 ";
        var code = await s.Catalog.CreateServiceAsync(request);
        Assert.Null(code.Dto); Assert.Equal("Service code already exists.", code.ErrorMessage);
        Assert.Equal(1, await s.Db.Services.CountAsync());
    }

    // UTC-TRT-016
    [Fact]
    public async Task UpdateServiceAsync_UpdatesPricePreservesHistoricalSnapshot()
    {
        using var s = await Scope.CreateAsync();
        var (dto, error) = await s.Catalog.UpdateServiceAsync(1, EditService());
        Assert.Null(error); Assert.Equal(200m, dto!.Price);
        s.Db.ChangeTracker.Clear(); Assert.Equal(200m, (await s.Db.Services.SingleAsync()).Price);
        Assert.Equal(100m, (await s.Db.InvoiceDetails.SingleAsync()).UnitPrice);
        Assert.Equal(100m, (await s.Db.TreatmentPlanDetails.SingleAsync()).UnitPrice);
    }

    // UTC-TRT-017
    [Fact]
    public async Task UpdateServiceAsync_MissingServiceReturnsNullTuple()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Catalog.UpdateServiceAsync(999, EditService());
        Assert.Null(result.Dto); Assert.Null(result.ErrorMessage);
    }

    // UTC-TRT-018
    [Fact]
    public async Task UpdateServiceAsync_InvalidPriceDoesNotChangeStoredPrice()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Catalog.UpdateServiceAsync(1, EditService(0m));
        Assert.Null(result.Dto); Assert.Equal("Price must be greater than 0.", result.ErrorMessage);
        s.Db.ChangeTracker.Clear(); Assert.Equal(100m, (await s.Db.Services.SingleAsync()).Price);
    }

    // UTC-TRT-019
    [Fact]
    public async Task DeleteServiceAsync_UnusedServiceIsDeleted()
    {
        using var s = await Scope.CreateAsync(); var created = await s.Catalog.CreateServiceAsync(NewService());
        var result = await s.Catalog.DeleteServiceAsync(created.Dto!.ServiceId);
        Assert.True(result.Success); Assert.False(result.IsDeactivated); Assert.Null(result.ErrorMessage);
        s.Db.ChangeTracker.Clear(); Assert.False(await s.Db.Services.AnyAsync(x => x.ServiceId == created.Dto.ServiceId));
    }

    // UTC-TRT-020
    [Fact]
    public async Task DeleteServiceAsync_ReferencedServiceIsDeactivated()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Catalog.DeleteServiceAsync(1);
        Assert.True(result.Success); Assert.True(result.IsDeactivated);
        s.Db.ChangeTracker.Clear(); Assert.False((await s.Db.Services.SingleAsync()).IsActive);
        Assert.Single(await s.Db.TreatmentPlanDetails.ToListAsync());
    }

    // UTC-TRT-021
    [Fact]
    public async Task DeleteServiceAsync_MissingServiceReturnsFailure()
    {
        using var s = await Scope.CreateAsync(); var result = await s.Catalog.DeleteServiceAsync(999);
        Assert.False(result.Success); Assert.False(result.IsDeactivated); Assert.NotNull(result.ErrorMessage);
    }

    // UTC-TRT-022
    [Fact]
    public async Task PlansAsync_FilterPatientReturnsPlan()
    {
        using var s = await Scope.CreateAsync();
        var result = await s.Work.PlansAsync(Reception, new() { PatientId = 1, Search = "Course" }, default);
        Assert.Equal(1, result.TotalCount); Assert.Equal(1, Assert.Single(result.Items).PlanId);
    }

    // UTC-TRT-023
    [Fact]
    public async Task PlansAsync_NoSearchMatchReturnsEmpty()
    {
        using var s = await Scope.CreateAsync();
        Assert.Empty((await s.Work.PlansAsync(Reception, new() { Search = "UNKNOWN" }, default)).Items);
    }

    // UTC-TRT-024
    [Fact]
    public async Task PlansAsync_InvalidDateRangeIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.PlansAsync(Reception,
            new() { FromDate = Today.AddDays(1), ToDate = Today }, default));
    }

    // UTC-TRT-025
    [Fact]
    public async Task PlanAsync_PaidPlanCanBeRequested()
    {
        using var s = await Scope.CreateAsync(); var result = await s.Work.PlanAsync(Reception, 1, default);
        Assert.True(result.FullyPaid); Assert.True(result.CanRequest); Assert.Equal(3, result.RemainingSessions);
        Assert.Equal(3, Assert.Single(result.Services).RemainingQuantity);
    }

    // UTC-TRT-026
    [Fact]
    public async Task PlanAsync_UnpaidPlanCannotBeRequested()
    {
        using var s = await Scope.CreateAsync(); (await s.Db.Invoices.SingleAsync()).PaymentStatus = "UNPAID";
        await s.Db.SaveChangesAsync(); var result = await s.Work.PlanAsync(Reception, 1, default);
        Assert.False(result.FullyPaid); Assert.False(result.CanRequest); Assert.Equal("PAYMENT_REQUIRED", result.RequestBlockedReason);
    }

    // UTC-TRT-027
    [Fact]
    public async Task PlanAsync_MissingPlanIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => s.Work.PlanAsync(Reception, 999, default));
    }

    // UTC-TRT-028
    [Fact]
    public async Task SessionsAsync_FiltersStatusAndPatient()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync(); await s.SessionAsync("CANCELLED", 2);
        var result = await s.Work.SessionsAsync(Reception, new() { PatientId = 1, Status = "REQUESTED" }, false, default);
        Assert.Equal(ticket.SessionId, Assert.Single(result.Items).SessionId); Assert.Equal(1, result.TotalCount);
    }

    // UTC-TRT-029
    [Fact]
    public async Task SessionsAsync_MineExcludesOtherDoctor()
    {
        using var s = await Scope.CreateAsync(); await s.SessionAsync();
        var other = await s.SessionAsync("CANCELLED", 2); other.AssignedDoctorId = 2; await s.Db.SaveChangesAsync();
        var result = await s.Work.SessionsAsync(Doctor(), new(), true, default);
        Assert.Equal(1, Assert.Single(result.Items).AssignedDoctorId);
    }

    // UTC-TRT-030
    [Fact]
    public async Task SessionsAsync_InvalidPageIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.SessionsAsync(Reception, new() { PageNumber = 0 }, false, default));
    }

    // UTC-TRT-031
    [Fact]
    public async Task SessionAsync_ReturnsSessionAndAllowedActions()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        var result = await s.Work.SessionAsync(Doctor(), ticket.SessionId, default);
        Assert.Equal(ticket.SessionId, result.Session.SessionId); Assert.True(result.CanStart);
        Assert.False(result.CanComplete); Assert.True(result.CanCancel); Assert.Empty(result.Usage);
    }

    // UTC-TRT-032
    [Fact]
    public async Task SessionAsync_MissingSessionIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => s.Work.SessionAsync(Reception, 999, default));
    }

    // UTC-TRT-033
    [Fact]
    public async Task SessionAsync_UnauthorizedDoctorIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Work.SessionAsync(Doctor(99), ticket.SessionId, default));
    }

    // UTC-TRT-034
    [Fact]
    public async Task WorkloadAsync_CountsStatesWithinDateAndShift()
    {
        using var s = await Scope.CreateAsync();
        await s.SessionAsync(); await s.SessionAsync("IN_PROGRESS", 2); await s.SessionAsync("COMPLETED", 3);
        await s.SessionAsync("CANCELLED", 4);
        var other = await s.SessionAsync("REQUESTED", 5); other.Shift = "MORNING";
        var future = await s.SessionAsync("REQUESTED", 6); future.ScheduledDate = Today.AddDays(1); await s.Db.SaveChangesAsync();
        var result = Assert.Single(await s.Work.WorkloadAsync(Reception, new() { ScheduledDate = Today, Shift = "AFTERNOON" }, default));
        Assert.Equal(3, result.AssignedCount); Assert.Equal(1, result.WaitingCount);
        Assert.Equal(1, result.InProgressCount); Assert.Equal(1, result.CompletedCount);
    }

    // UTC-TRT-035
    [Fact]
    public async Task WorkloadAsync_UnknownDoctorReturnsEmpty()
    {
        using var s = await Scope.CreateAsync();
        Assert.Empty(await s.Work.WorkloadAsync(Reception, new() { ScheduledDate = Today, Shift = "AFTERNOON", DoctorId = 999 }, default));
    }

    // UTC-TRT-036
    [Fact]
    public async Task WorkloadAsync_UnsupportedShiftIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.WorkloadAsync(Reception, new() { ScheduledDate = Today, Shift = "EVENING" }, default));
    }

    // UTC-TRT-037
    [Fact]
    public async Task RequestAsync_PaidPlanCreatesIndependentRequest()
    {
        using var s = await Scope.CreateAsync(); var result = await s.Work.RequestAsync(Reception, 1, Request(), default);
        Assert.Equal("REQUESTED", result.Session.Status); Assert.Equal(1, result.Session.SessionNumber);
        Assert.Equal("AFTERNOON", result.Session.Shift); Assert.NotNull(result.Session.QueueNumber);
        Assert.False(string.IsNullOrWhiteSpace(result.Session.RequestCode));
        Assert.Empty(result.Usage); Assert.Equal(3, (await s.Db.TreatmentPlanDetails.SingleAsync()).PlannedQuantity);
    }

    // UTC-TRT-038
    [Fact]
    public async Task RequestAsync_UnpaidPlanIsRejected()
    {
        using var s = await Scope.CreateAsync(); (await s.Db.Invoices.SingleAsync()).PaymentStatus = "UNPAID"; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.RequestAsync(Reception, 1, Request(), default));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-TRT-039
    [Fact]
    public async Task RequestAsync_DuplicateActiveRequestIsRejected()
    {
        using var s = await Scope.CreateAsync(); await s.Work.RequestAsync(Reception, 1, Request(), default);
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.RequestAsync(Reception, 1, Request(), default));
        Assert.Equal(1, await s.Db.TreatmentSessions.CountAsync());
    }

    // UTC-TRT-040
    [Fact]
    public async Task RequestBatchAsync_BatchTreatmentRequest_CreatesIndependentRequests()
    {
        using var s = await Scope.CreateAsync();
        var result = Assert.Single(await s.Work.RequestBatchAsync(Reception, s.Batch(), default));
        Assert.Equal(1, result.Session.PlanId); Assert.Equal("REQUESTED", result.Session.Status);
        Assert.Equal(1, await s.Db.TreatmentSessions.CountAsync());
    }

    // UTC-TRT-041
    [Fact]
    public async Task RequestBatchAsync_BatchTreatmentRequest_DuplicatePlansAreRejected()
    {
        using var s = await Scope.CreateAsync(); var request = s.Batch(); request.Items.Add(request.Items[0]);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.RequestBatchAsync(Reception, request, default));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-TRT-042
    [Fact]
    public async Task RequestBatchAsync_BatchTreatmentRequest_UnpaidPlanIsRejected()
    {
        using var s = await Scope.CreateAsync(); (await s.Db.Invoices.SingleAsync()).PaymentStatus = "UNPAID";
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.RequestBatchAsync(Reception, s.Batch(), default));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-TRT-043
    [Fact]
    public async Task RequestBatchAsync_CreateTreatmentAdmission_CreatesIndependentRequests()
    {
        using var s = await Scope.CreateAsync();
        var result = Assert.Single(await s.Work.RequestBatchAsync(Reception, s.LegacyBatch(), default));
        Assert.Equal(1, result.Session.PlanId); Assert.Equal("REQUESTED", result.Session.Status);
        Assert.Equal(1, await s.Db.TreatmentSessions.CountAsync());
    }

    // UTC-TRT-044
    [Fact]
    public async Task RequestBatchAsync_CreateTreatmentAdmission_DuplicatePlansAreRejected()
    {
        using var s = await Scope.CreateAsync(); var request = s.LegacyBatch(); request.Items.Add(request.Items[0]);
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.RequestBatchAsync(Reception, request, default));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-TRT-045
    [Fact]
    public async Task RequestBatchAsync_CreateTreatmentAdmission_UnpaidPlanIsRejected()
    {
        using var s = await Scope.CreateAsync(); (await s.Db.Invoices.SingleAsync()).PaymentStatus = "UNPAID";
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.RequestBatchAsync(Reception, s.LegacyBatch(), default));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-TRT-046
    [Fact]
    public async Task StartAsync_StartsAssignedRequestAndRetryIsSafe()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        var first = await s.Work.StartAsync(Doctor(), ticket.SessionId, Version, default);
        var second = await s.Work.StartAsync(Doctor(), ticket.SessionId, Version, default);
        Assert.Equal("IN_PROGRESS", first.Session.Status); Assert.NotNull(first.Session.StartedAt);
        Assert.Equal(1, first.Session.PerformedBy); Assert.Equal(first.Session.StartedAt, second.Session.StartedAt);
        Assert.False(await s.Db.UsageHistories.AnyAsync());
    }

    // UTC-TRT-047
    [Fact]
    public async Task StartAsync_UnauthorizedDoctorIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Work.StartAsync(Doctor(99), ticket.SessionId, Version, default));
    }

    // UTC-TRT-048
    [Fact]
    public async Task StartAsync_PaymentIsRecheckedBeforeStart()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        (await s.Db.Invoices.SingleAsync()).PaymentStatus = "UNPAID"; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.StartAsync(Doctor(), ticket.SessionId, Version, default));
        s.Db.ChangeTracker.Clear(); Assert.Equal("REQUESTED", (await s.Db.TreatmentSessions.SingleAsync()).Status);
    }

    // UTC-TRT-049
    [Fact]
    public async Task CompleteAsync_RecordsUsageAndRetryDoesNotDuplicate()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync("IN_PROGRESS");
        var result = await s.Work.CompleteAsync(Doctor(), ticket.SessionId, Outcome(), default);
        Assert.Equal("COMPLETED", result.Session.Status); Assert.Equal("Improved", result.Session.ResultNote);
        Assert.Equal(1, Assert.Single(result.Usage).Quantity); Assert.Equal(1, result.TreatmentPlan.Plan.CompletedSessions);
        Assert.Equal("IN_PROGRESS", result.TreatmentPlan.Plan.Status);
        await s.Work.CompleteAsync(Doctor(), ticket.SessionId, Outcome(), default);
        s.Db.ChangeTracker.Clear(); Assert.Equal(1, await s.Db.UsageHistories.CountAsync());
        Assert.Equal(1, await s.Db.Invoices.CountAsync());
    }

    // UTC-TRT-050
    [Fact]
    public async Task CompleteAsync_UnstartedRequestIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.CompleteAsync(Doctor(), ticket.SessionId, Outcome(), default));
        Assert.False(await s.Db.UsageHistories.AnyAsync());
    }

    // UTC-TRT-051
    [Fact]
    public async Task CompleteAsync_ExceededServiceQuantityIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync("IN_PROGRESS");
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.CompleteAsync(Doctor(), ticket.SessionId, Outcome(4), default));
        Assert.False(await s.Db.UsageHistories.AnyAsync());
    }

    // UTC-TRT-052
    [Fact]
    public async Task CompleteAsync_MissingResultIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync("IN_PROGRESS");
        var request = Outcome(); request.ResultNote = "";
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.CompleteAsync(Doctor(), ticket.SessionId, request, default));
    }

    // UTC-TRT-053
    [Fact]
    public async Task CompleteAsync_FinalSessionAndAllServicesCompletePlan()
    {
        using var s = await Scope.CreateAsync(); s.Plan.PlannedSessionCount = 1;
        (await s.Db.TreatmentPlanDetails.SingleAsync()).PlannedQuantity = 1; await s.Db.SaveChangesAsync();
        var ticket = await s.SessionAsync("IN_PROGRESS");
        var result = await s.Work.CompleteAsync(Doctor(), ticket.SessionId, Outcome(), default);
        Assert.Equal("COMPLETED", result.TreatmentPlan.Plan.Status); Assert.NotNull(result.TreatmentPlan.EndDate);
        Assert.Equal(1, result.TreatmentPlan.Plan.CompletedSessions);
        Assert.Equal(0, Assert.Single(result.TreatmentPlan.Services).RemainingQuantity);
    }

    // UTC-TRT-054
    [Fact]
    public async Task CancelAsync_CancellationPreservesPaymentAndAllowsRegistrationAgain()
    {
        using var s = await Scope.CreateAsync();
        var original = await s.Work.RequestAsync(Reception, 1, Request(), default);
        await s.Work.CancelAsync(Reception, original.Session.SessionId,
            new() { RowVersion = new byte[8], Reason = "Patient unavailable" }, default);
        var replacement = await s.Work.RequestAsync(Reception, 1, Request(), default);
        Assert.NotEqual(original.Session.SessionId, replacement.Session.SessionId);
        Assert.Equal(1, replacement.Session.SessionNumber); Assert.True(replacement.TreatmentPlan.FullyPaid);
        s.Db.ChangeTracker.Clear(); Assert.Equal("PAID", (await s.Db.Invoices.SingleAsync()).PaymentStatus);
        Assert.Equal(3, (await s.Db.TreatmentPlanDetails.SingleAsync()).PlannedQuantity);
        Assert.False(await s.Db.UsageHistories.AnyAsync());
        var cancelled = await s.Db.TreatmentSessions.SingleAsync(x => x.SessionId == original.Session.SessionId);
        Assert.Equal("CANCELLED", cancelled.Status); Assert.Contains("Patient unavailable", cancelled.ResultNote);
        Assert.Contains("user 1", cancelled.ResultNote);
    }

    // UTC-TRT-055
    [Fact]
    public async Task CancelAsync_MissingReasonIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.CancelAsync(Reception, ticket.SessionId,
            new() { RowVersion = new byte[8], Reason = "" }, default));
    }

    // UTC-TRT-056
    [Fact]
    public async Task CancelAsync_StartedSessionIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync("IN_PROGRESS");
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.CancelAsync(Reception, ticket.SessionId,
            new() { RowVersion = new byte[8], Reason = "Cancel" }, default));
    }

    // UTC-TRT-057
    [Fact]
    public async Task CancelSessionTicketAsync_CancelsRequestAndPreservesNote()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync(); ticket.ResultNote = "Original note";
        await s.Db.SaveChangesAsync();
        var result = await s.Work.CancelSessionTicketAsync(Reception, ticket.SessionId,
            new() { RowVersion = new byte[8], CancelReason = "Patient unavailable" }, default);
        Assert.Equal("CANCELLED", result.Session.Status); Assert.Contains("Original note", result.Session.ResultNote);
        Assert.Contains("Patient unavailable", result.Session.ResultNote);
        Assert.Equal("PAID", (await s.Db.Invoices.SingleAsync()).PaymentStatus);
    }

    // UTC-TRT-058
    [Fact]
    public async Task CancelSessionTicketAsync_MissingReasonIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.CancelSessionTicketAsync(Reception, ticket.SessionId,
            new() { CancelReason = "" }, default));
    }

    // UTC-TRT-059
    [Fact]
    public async Task CancelSessionTicketAsync_StartedSessionIsRejected()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync("IN_PROGRESS");
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.CancelSessionTicketAsync(Reception, ticket.SessionId,
            new() { RowVersion = new byte[8], CancelReason = "Cancel" }, default));
    }

    // UTC-TRT-060
    [Fact]
    public async Task CancelPlanAsync_UninvoicedUnusedPlanCanBeCancelled()
    {
        using var s = await Scope.CreateAsync(); await s.RemoveInvoicesAsync();
        var result = await s.Work.CancelPlanAsync(Doctor(), 1,
            new() { RowVersion = new byte[8], Reason = "Indication cancelled" }, default);
        Assert.Equal("CANCELLED", result.Plan.Status); Assert.Contains("Indication cancelled", result.Description);
        Assert.Equal(3, (await s.Db.TreatmentPlanDetails.SingleAsync()).PlannedQuantity);
    }

    // UTC-TRT-061
    [Fact]
    public async Task CancelPlanAsync_InvoicedPlanIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.CancelPlanAsync(Doctor(), 1,
            new() { RowVersion = new byte[8], Reason = "Cancel" }, default));
        Assert.Equal("PAID", (await s.Db.Invoices.SingleAsync()).PaymentStatus);
    }

    // UTC-TRT-062
    [Fact]
    public async Task CancelPlanAsync_UnauthorizedDoctorIsRejected()
    {
        using var s = await Scope.CreateAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.Work.CancelPlanAsync(Doctor(99), 1,
            new() { RowVersion = new byte[8], Reason = "Cancel" }, default));
    }

    // UTC-TRT-063
    [Fact]
    public async Task GetQueueAsync_MapsActualPlanSessionCount()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        var overview = await s.Work.SessionAsync(Reception, ticket.SessionId, default);
        var workflow = new Mock<ITreatmentWorkflowService>(MockBehavior.Strict);
        workflow.Setup(w => w.SessionsAsync(Reception, It.Is<SessionQuery>(q => q.Status == "REQUESTED" && q.PageSize == 100), false, default))
            .ReturnsAsync(new PagedResult<SessionSummary>([overview.Session], 1, 100, 1));
        var result = Assert.Single(await new TreatmentProtocolService(s.Db, workflow.Object).GetQueueAsync(Reception, new()));
        Assert.Equal("01 / 03", result.SessionInfo); Assert.Equal(ticket.SessionId, result.SessionId); workflow.VerifyAll();
    }

    // UTC-TRT-064
    [Fact]
    public async Task GetQueueAsync_NoSessionsReturnsEmpty()
    {
        using var s = await Scope.CreateAsync(); var workflow = new Mock<ITreatmentWorkflowService>(MockBehavior.Strict);
        workflow.Setup(w => w.SessionsAsync(Reception, It.IsAny<SessionQuery>(), false, default))
            .ReturnsAsync(new PagedResult<SessionSummary>([], 1, 100, 0));
        Assert.Empty(await new TreatmentProtocolService(s.Db, workflow.Object).GetQueueAsync(Reception, new())); workflow.VerifyAll();
    }

    // UTC-TRT-065
    [Fact]
    public async Task GetQueueAsync_ReadsNextPageAfterFullPage()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        var row = (await s.Work.SessionAsync(Reception, ticket.SessionId, default)).Session;
        var rows = Enumerable.Range(1, 100).Select(i => row with { SessionId = i }).ToArray();
        var workflow = new Mock<ITreatmentWorkflowService>(MockBehavior.Strict);
        workflow.Setup(w => w.SessionsAsync(Reception, It.Is<SessionQuery>(q => q.PageNumber == 1), false, default))
            .ReturnsAsync(new PagedResult<SessionSummary>(rows, 1, 100, 100));
        workflow.Setup(w => w.SessionsAsync(Reception, It.Is<SessionQuery>(q => q.PageNumber == 2), false, default))
            .ReturnsAsync(new PagedResult<SessionSummary>([], 2, 100, 100));
        Assert.Equal(100, (await new TreatmentProtocolService(s.Db, workflow.Object).GetQueueAsync(Reception, new())).Count);
        workflow.VerifyAll();
    }

    // UTC-TRT-066
    [Fact]
    public async Task CreateTreatmentRequestAsync_ForwardsRequestAndMapsResult()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        var overview = await s.Work.SessionAsync(Reception, ticket.SessionId, default);
        var workflow = new Mock<ITreatmentWorkflowService>(MockBehavior.Strict);
        workflow.Setup(w => w.RequestAsync(Reception, 1, It.Is<RequestTreatment>(r =>
            r.AssignedDoctorId == 1 && r.ScheduledDate == Today && r.Shift == "AFTERNOON" && r.RowVersion.Length == 8), default)).ReturnsAsync(overview);
        var result = await new TreatmentProtocolService(s.Db, workflow.Object).CreateTreatmentRequestAsync(Reception,
            new() { PlanId = 1, AssignedDoctorId = 1, ScheduledDate = Today, Shift = "AFTERNOON", RowVersion = new byte[8] });
        Assert.Equal(ticket.SessionId, result.SessionId); Assert.Equal("01 / 03", result.SessionInfo); workflow.VerifyAll();
    }

    // UTC-TRT-067
    [Fact]
    public async Task CreateTreatmentRequestAsync_PropagatesWorkflowPaymentError()
    {
        using var s = await Scope.CreateAsync(); var workflow = new Mock<ITreatmentWorkflowService>(MockBehavior.Strict);
        workflow.Setup(w => w.RequestAsync(Reception, 1, It.IsAny<RequestTreatment>(), default)).ThrowsAsync(new ConflictException("Workflow rejected request"));
        await Assert.ThrowsAsync<ConflictException>(() => new TreatmentProtocolService(s.Db, workflow.Object)
            .CreateTreatmentRequestAsync(Reception, new() { PlanId = 1, AssignedDoctorId = 1, ScheduledDate = Today, RowVersion = new byte[8] }));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync()); workflow.VerifyAll();
    }

    // UTC-TRT-068
    [Fact]
    public async Task CreateTreatmentRequestAsync_PropagatesWorkflowAuthorizationError()
    {
        using var s = await Scope.CreateAsync(); var workflow = new Mock<ITreatmentWorkflowService>(MockBehavior.Strict);
        workflow.Setup(w => w.RequestAsync(Reception, 1, It.IsAny<RequestTreatment>(), default)).ThrowsAsync(new ForbiddenException("Workflow rejected request"));
        await Assert.ThrowsAsync<ForbiddenException>(() => new TreatmentProtocolService(s.Db, workflow.Object)
            .CreateTreatmentRequestAsync(Reception, new() { PlanId = 1, AssignedDoctorId = 1, ScheduledDate = Today, RowVersion = new byte[8] }));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync()); workflow.VerifyAll();
    }

    // UTC-TRT-069
    [Fact]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("Defect", "DEF-TRT-001")]
    public async Task CompleteAsync_EnoughSessionsButServicesRemainMustKeepPlanInProgress()
    {
        using var s = await Scope.CreateAsync();
        var extraService = new COSGeniusSystem.Models.Service
        {
            ServiceId = 2,
            ServiceCode = "DV002",
            ServiceName = "Massage",
            Price = 100m,
            Unit = "session",
            IsActive = true
        };
        var extraDetail = new TreatmentPlanDetail
        {
            PlanDetailId = 2,
            Plan = s.Plan,
            Service = extraService,
            ServiceNameSnapshot = "Massage",
            UnitSnapshot = "session",
            UnitPrice = 100m,
            PlannedQuantity = 3
        };
        s.Db.TreatmentPlanDetails.Add(extraDetail);
        (await s.Db.Invoices.SingleAsync()).InvoiceDetails.Add(new InvoiceDetail
        {
            PlanDetail = extraDetail,
            ItemType = "SERVICE",
            ItemName = "Massage",
            UnitPrice = 100m,
            Quantity = 3,
            SubTotal = 300m
        });
        for (var number = 1; number <= 2; number++)
        {
            var previous = await s.SessionAsync("COMPLETED", number);
            s.Db.UsageHistories.Add(new UsageHistory
            {
                Session = previous,
                PlanDetail = extraDetail,
                PerformedBy = 1,
                Quantity = 1,
                ExecutionTime = DateTime.UtcNow
            });
        }
        await s.Db.SaveChangesAsync();
        var ticket = await s.SessionAsync("IN_PROGRESS", 3);
        var result = await s.Work.CompleteAsync(Doctor(), ticket.SessionId, Outcome(), default);
        Assert.Equal(2, result.TreatmentPlan.Services.Single(x => x.PlanDetailId == 1).RemainingQuantity);
        Assert.Equal("IN_PROGRESS", result.TreatmentPlan.Plan.Status); Assert.Null(result.TreatmentPlan.EndDate);
    }

    // UTC-TRT-070
    [Fact]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("Defect", "DEF-TRT-001")]
    public async Task CompleteAsync_EnoughServicesButSessionsRemainMustKeepPlanInProgress()
    {
        using var s = await Scope.CreateAsync();
        var ticket = await s.SessionAsync("IN_PROGRESS");
        var result = await s.Work.CompleteAsync(Doctor(), ticket.SessionId, Outcome(3), default);
        Assert.Equal(2, result.TreatmentPlan.RemainingSessions);
        Assert.Equal("IN_PROGRESS", result.TreatmentPlan.Plan.Status); Assert.Null(result.TreatmentPlan.EndDate);
    }

    // UTC-TRT-071
    [Fact]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("Defect", "DEF-TRT-002")]
    public async Task RequestBatchAsync_BatchTreatmentRequest_AnotherPatientsPlanMustBeRejected()
    {
        using var s = await Scope.CreateAsync(); var other = SchemaFixture.Record(2);
        other.Status = other.Registration.Status = "COMPLETED"; other.Registration.BusinessDate = Today;
        s.Db.MedicalRecords.Add(other); await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => s.Work.RequestBatchAsync(Reception, s.Batch(2), default));
        Assert.False(await s.Db.TreatmentSessions.AnyAsync());
    }

    // UTC-TRT-072
    [Fact]
    [Trait("Category", "BusinessRuleRegression")]
    [Trait("Defect", "DEF-TRT-003")]
    public async Task CreateTreatmentRequestAsync_ValidCompatibilityRequestMustReachWorkflowSuccessfully()
    {
        using var s = await Scope.CreateAsync();
        var result = await new TreatmentProtocolService(s.Db, s.Work).CreateTreatmentRequestAsync(Reception,
            new() { PlanId = 1, AssignedDoctorId = 1, ScheduledDate = Today, Shift = "AFTERNOON", RowVersion = new byte[8] });
        Assert.Equal("REQUESTED", result.Status); Assert.Equal(1, await s.Db.TreatmentSessions.CountAsync());
    }

    // UTC-TRT-073
    [Fact]
    public async Task StartAsync_DoctorWithOngoingExaminationIsBlocked()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        s.Record.Status = s.Record.Registration.Status = "IN_PROGRESS"; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.StartAsync(Doctor(), ticket.SessionId, Version, default));
        s.Db.ChangeTracker.Clear(); Assert.Equal("REQUESTED", (await s.Db.TreatmentSessions.SingleAsync()).Status);
    }

    // UTC-TRT-074
    [Fact]
    public async Task StartAsync_PatientWithAnotherDoctorsOngoingExaminationIsBlocked()
    {
        using var s = await Scope.CreateAsync(); var ticket = await s.SessionAsync();
        var otherDoctor = SchemaFixture.Record(2).Doctor; otherDoctor.IsActive = true; otherDoctor.User!.IsActive = true;
        s.Db.Doctors.Add(otherDoctor);
        s.Record.Doctor = otherDoctor; s.Record.Registration.Doctor = otherDoctor;
        s.Record.Status = s.Record.Registration.Status = "IN_PROGRESS"; await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => s.Work.StartAsync(Doctor(), ticket.SessionId, Version, default));
        s.Db.ChangeTracker.Clear(); Assert.Equal("REQUESTED", (await s.Db.TreatmentSessions.SingleAsync()).Status);
    }

    // UTC-TRT-075
    [Fact]
    public async Task TreatmentDtoValidation_QuantityAndShiftRespectAllowedValues()
    {
        foreach (var (quantity, expected) in new[] { (0, false), (1, true), (10000, true), (10001, false) })
        {
            var input = new UsageInput { PlanDetailId = 1, Quantity = quantity };
            var errors = new System.Collections.Generic.List<ValidationResult>();
            Assert.Equal(expected, Validator.TryValidateObject(input, new ValidationContext(input), errors, true));
            if (!expected) Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UsageInput.Quantity)));
        }
        foreach (var (shift, expected) in new[] { ("MORNING", true), ("AFTERNOON", true), ("", false), ("EVENING", false) })
        {
            var input = Request(); input.ScheduledDate = Today.AddDays(10); input.Shift = shift;
            Assert.Equal(expected, Validator.TryValidateObject(input, new ValidationContext(input),
                new System.Collections.Generic.List<ValidationResult>(), true));
        }
        await Task.CompletedTask;
    }

    // UTC-TRT-076
    [Fact]
    public async Task RequestAsync_ExpiredAttemptDoesNotConsumePrescribedSession()
    {
        using var s = await Scope.CreateAsync(); var abandoned = await s.SessionAsync();
        abandoned.ScheduledDate = Today.AddDays(-1); await s.Db.SaveChangesAsync();
        var replacement = await s.Work.RequestAsync(Reception, 1, Request(), default);
        Assert.Equal(1, replacement.Session.SessionNumber);
        s.Db.ChangeTracker.Clear();
        Assert.Equal("EXPIRED", (await s.Db.TreatmentSessions.SingleAsync(x => x.SessionId == abandoned.SessionId)).Status);
        Assert.Equal(2, (await s.Db.TreatmentSessions.SingleAsync(x => x.SessionId == replacement.Session.SessionId)).SessionNumber);
        Assert.Equal(0, replacement.TreatmentPlan.Plan.CompletedSessions);
    }

    // UTC-TRT-077
    [Fact]
    public async Task CrossWorkflowAvailability_OngoingTreatmentBlocksExaminationStartForSamePatient()
    {
        using var s = await Scope.CreateAsync(); await s.SessionAsync("IN_PROGRESS");
        var otherDoctor = SchemaFixture.Record(2).Doctor; otherDoctor.IsActive = true; otherDoctor.User!.IsActive = true;
        var registration = new ExaminationRegistration
        {
            RegistrationId = 2,
            Patient = s.Record.Patient,
            Doctor = otherDoctor,
            RegisteredBy = 1,
            BusinessDate = Today,
            Session = "AFTERNOON",
            RegisteredAt = DateTime.UtcNow,
            Status = "WAITING",
            RowVersion = new byte[8]
        };
        s.Db.ExaminationRegistrations.Add(registration); await s.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => new ExaminationService(s.Db, new PatientService(s.Db))
            .StartAsync(Doctor(2), registration.RegistrationId, default));
        s.Db.ChangeTracker.Clear();
        Assert.Equal("WAITING", (await s.Db.ExaminationRegistrations.SingleAsync(x => x.RegistrationId == 2)).Status);
        Assert.Equal(1, await s.Db.MedicalRecords.CountAsync());
    }

}
