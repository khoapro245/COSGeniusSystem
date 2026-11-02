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
            // Keep sortable values in a translatable projection before constructing the response record.
            .Select(d => new
            {
                d.DoctorId,
                DoctorName = d.FullName,
                d.Specialization,
                AssignedCount = registrations.Count(r => r.DoctorId == d.DoctorId && r.Status != "CANCELLED"),
                WaitingCount = registrations.Count(r => r.DoctorId == d.DoctorId && r.Status == "WAITING"),
                InProgressCount = registrations.Count(r => r.DoctorId == d.DoctorId && r.Status == "IN_PROGRESS"),
                CompletedCount = registrations.Count(r => r.DoctorId == d.DoctorId && r.Status == "COMPLETED")
            })
            .OrderBy(d => d.AssignedCount).ThenBy(d => d.DoctorName).ThenBy(d => d.DoctorId)
            .Select(d => new DoctorWorkloadResponse(d.DoctorId, d.DoctorName, d.Specialization,
                date, query.Session, d.AssignedCount, d.WaitingCount, d.InProgressCount, d.CompletedCount))
            .ToListAsync(ct);
    }
}
