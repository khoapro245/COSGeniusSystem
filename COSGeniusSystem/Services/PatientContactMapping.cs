using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;
using COSGeniusSystem.Models;

namespace COSGeniusSystem.Services;

internal static class PatientContactMapping
{
    internal static void Apply(PatientContactRequest request, PatientContact entity)
    {
        entity.FullName = request.FullName.Trim();
        entity.Relationship = request.Relationship;
        entity.Phone = request.Phone;
    }

    internal static PatientContactResponse ToResponse(PatientContact entity) => new()
    {
        ContactId = entity.ContactId,
        PatientId = entity.PatientId,
        FullName = entity.FullName,
        Relationship = entity.Relationship,
        Phone = entity.Phone,
    };
}
