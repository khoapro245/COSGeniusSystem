using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Moq;

namespace COSGeniusSystem.UnitTests;

public class TreatmentProtocolServiceTests
{
    [Fact]
    public async Task CreateQueue_DelegatesToWorkflow_PreservingPaymentAndVersionChecks()
    {
        using var db = SchemaFixture.Create();
        var actor = new ClaimsPrincipal();
        var request = new CreateTreatmentRequest
        {
            PlanId = 5, AssignedDoctorId = 7, ScheduledDate = new DateOnly(2026, 11, 2), RowVersion = new byte[8]
        };
        var workflow = new Mock<ITreatmentWorkflowService>(MockBehavior.Strict);
        workflow.Setup(w => w.RequestAsync(actor, 5, It.Is<RequestTreatment>(r =>
            r.AssignedDoctorId == 7 && r.ScheduledDate == request.ScheduledDate && r.RowVersion == request.RowVersion), default))
            .ThrowsAsync(new ConflictException("Payment required"));
        var service = new TreatmentProtocolService(db, workflow.Object);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateTreatmentRequestAsync(actor, request));
        Assert.Empty(db.UsageHistories);
        Assert.Empty(db.MedicalRecords);
        workflow.VerifyAll();
    }

    [Fact]
    public async Task Queue_UsesAuthorizedSessionQuery_AndRealPlanSessionCount()
    {
        using var db = SchemaFixture.Create();
        var plan = SchemaFixture.Plan(SchemaFixture.Record());
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();
        var actor = new ClaimsPrincipal();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var session = new SessionSummary(8, 1, "LT1", "Plan", 2, 1, "BN1", "Patient", null,
            1, "Doctor", null, null, 1, DateTime.UtcNow, date, null, null, "REQUESTED", null, new byte[8]);
        var workflow = new Mock<ITreatmentWorkflowService>(MockBehavior.Strict);
        workflow.Setup(w => w.SessionsAsync(actor, It.Is<SessionQuery>(q =>
            q.Status == "REQUESTED" && q.FromDate == date && q.ToDate == date && q.Search == "Patient"), false, default))
            .ReturnsAsync(new PagedResult<SessionSummary>([session], 1, 100, 1));
        var result = Assert.Single(await new TreatmentProtocolService(db, workflow.Object)
            .GetQueueAsync(actor, new TreatmentQueueQuery { Keyword = "Patient" }));
        Assert.Equal(8, result.SessionId);
        Assert.Equal("02 / 03", result.SessionInfo);
        workflow.VerifyAll();
    }
}
