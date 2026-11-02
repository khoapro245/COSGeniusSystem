using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public sealed partial class ExaminationService
{
    private static void ValidateBusinessDate(DateOnly date)
    {
        if (date < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            throw new BadRequestException("BusinessDate cannot be in the past.");
    }

    public async Task<IReadOnlyList<DoctorWorkloadResponse>> DoctorWorkloadAsync(
        ClaimsPrincipal actor, DoctorWorkloadQuery query, CancellationToken ct)
    {
        Reception(actor);
        Validate(query);
        var date = query.BusinessDate!.Value;
        var registrations = db.ExaminationRegistrations.AsNoTracking()
            .Where(r => r.BusinessDate == date && r.Session == query.Session);
        return await db.Doctors.AsNoTracking()
            .Where(d => d.IsActive && d.User != null && d.User.IsActive
                && d.User.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Doctor))
            .Select(d => new DoctorWorkloadResponse(d.DoctorId, d.FullName, d.Specialization,
                date, query.Session,
                registrations.Count(r => r.DoctorId == d.DoctorId && r.Status != "CANCELLED"),
                registrations.Count(r => r.DoctorId == d.DoctorId && r.Status == "WAITING"),
                registrations.Count(r => r.DoctorId == d.DoctorId && r.Status == "IN_PROGRESS"),
                registrations.Count(r => r.DoctorId == d.DoctorId && r.Status == "COMPLETED")))
            .OrderBy(d => d.AssignedCount).ThenBy(d => d.DoctorName).ThenBy(d => d.DoctorId)
            .ToListAsync(ct);
    }
}
