using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;
using COSGeniusSystem.Models;

namespace COSGeniusSystem.Services;

internal static class PatientMapping
{
    internal static void Apply(PatientRequest request, Patient entity)
    {
        entity.FullName = request.FullName.Trim();
        entity.Gender = request.Gender;
        entity.DateOfBirth = request.DateOfBirth;
        entity.Phone = request.Phone;
        entity.Address = request.Address;
        entity.IdentityCard = request.IdentityCard;
        entity.Email = request.Email;
        entity.IdentityCardIssuedDate = request.IdentityCardIssuedDate;
        entity.IdentityCardIssuedPlace = request.IdentityCardIssuedPlace;
        entity.PhotoUrl = request.PhotoUrl;
        entity.Province = request.Province;
        entity.Ward = request.Ward;
        entity.Occupation = request.Occupation;
        entity.Workplace = request.Workplace;
        entity.Nationality = request.Nationality;
        entity.Ethnicity = request.Ethnicity;
    }

    internal static PatientResponse ToResponse(Patient entity) => new()
    {
        PatientId = entity.PatientId,
        PatientCode = entity.PatientCode,
        CreatedAt = entity.CreatedAt,
        FullName = entity.FullName,
        Gender = entity.Gender,
        DateOfBirth = entity.DateOfBirth,
        Phone = entity.Phone,
        Address = entity.Address,
        IdentityCard = entity.IdentityCard,
        Email = entity.Email,
        IdentityCardIssuedDate = entity.IdentityCardIssuedDate,
        IdentityCardIssuedPlace = entity.IdentityCardIssuedPlace,
        PhotoUrl = entity.PhotoUrl,
        Province = entity.Province,
        Ward = entity.Ward,
        Occupation = entity.Occupation,
        Workplace = entity.Workplace,
        Nationality = entity.Nationality,
        Ethnicity = entity.Ethnicity,
    };
}
