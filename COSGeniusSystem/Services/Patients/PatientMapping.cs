using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;

namespace COSGeniusSystem.Services;

internal static class PatientMapping
{
    internal static readonly System.Linq.Expressions.Expression<Func<Patient, PatientResponse>> Projection = entity => new()
    {
        PatientId = entity.PatientId, PatientCode = entity.PatientCode, CreatedAt = entity.CreatedAt,
        FullName = entity.FullName, Gender = entity.Gender, DateOfBirth = entity.DateOfBirth,
        Phone = entity.Phone, Address = entity.Address, IdentityCard = entity.IdentityCard, Email = entity.Email,
        IdentityCardIssuedDate = entity.IdentityCardIssuedDate, IdentityCardIssuedPlace = entity.IdentityCardIssuedPlace,
        PhotoUrl = entity.PhotoUrl, Province = entity.Province, Ward = entity.Ward, Occupation = entity.Occupation,
        Workplace = entity.Workplace, Nationality = entity.Nationality, Ethnicity = entity.Ethnicity
    };

    internal static void Apply(PatientRequest request, Patient entity)
    {
        entity.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Gender))
        {
            var g = request.Gender.Trim().ToLowerInvariant();
            entity.Gender = (g is "nam" or "male") ? "Nam" : (g is "nữ" or "nu" or "female") ? "Nữ" : "Khác";
        }
        else
        {
            entity.Gender = null;
        }
        entity.DateOfBirth = request.DateOfBirth;
        entity.Phone = request.Phone;

        var address = request.Address?.Trim();
        var ward = request.Ward?.Trim();
        var province = request.Province?.Trim();

        // Xử lý mapping an toàn cho các trường địa chỉ mở rộng (Tỉnh/Thành, Phường/Xã) vào chuỗi Address của bảng Patient
        if (!string.IsNullOrWhiteSpace(ward) || !string.IsNullOrWhiteSpace(province))
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(address)) parts.Add(address);
            if (!string.IsNullOrWhiteSpace(ward) && (string.IsNullOrWhiteSpace(address) || !address.Contains(ward, StringComparison.OrdinalIgnoreCase)))
                parts.Add(ward);
            if (!string.IsNullOrWhiteSpace(province) && (string.IsNullOrWhiteSpace(address) || !address.Contains(province, StringComparison.OrdinalIgnoreCase)))
                parts.Add(province);
            entity.Address = parts.Count > 0 ? string.Join(", ", parts) : address;
        }
        else
        {
            entity.Address = address;
        }

        entity.IdentityCard = request.IdentityCard;
        entity.Email = request.Email;
        entity.IdentityCardIssuedDate = request.IdentityCardIssuedDate;
        entity.IdentityCardIssuedPlace = request.IdentityCardIssuedPlace;
        entity.PhotoUrl = request.PhotoUrl;
        entity.Province = province;
        entity.Ward = ward;
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
