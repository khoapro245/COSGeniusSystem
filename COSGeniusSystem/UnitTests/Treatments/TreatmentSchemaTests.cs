using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests;

public class TreatmentSchemaTests
{
    // A query-translation check only: never opens a connection or runs a migration.
    [Fact]
    public void SessionNumberProjection_TranslatesCancelledAndExpiredAttemptsToSql()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaChecks;Integrated Security=true;TrustServerCertificate=true").Options);
        var method = typeof(TreatmentWorkflowService).GetMethod("SessionProjection",
            BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(DateOnly) }, null);
        Assert.NotNull(method);
        var projection = (Expression<Func<TreatmentSession, SessionSummary>>)method.Invoke(null,
            new object[] { DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) })!;
        var sql = db.TreatmentSessions.Select(projection).ToQueryString();
        Assert.Contains("COUNT", sql);
        Assert.Contains("EXPIRED", sql);
        Assert.Contains("CANCELLED", sql);
    }
}
