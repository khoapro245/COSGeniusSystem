using System.ComponentModel.DataAnnotations;
using COSGeniusSystem.DTOs;

namespace COSGeniusSystem.UnitTests;

public class TreatmentValidationBoundaryTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10000, true)]
    [InlineData(10001, false)]
    public void UsageQuantity_ValidatesInclusiveBounds(int quantity, bool expected)
    {
        var input = new UsageInput { PlanDetailId = 1, Quantity = quantity };
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(input, new ValidationContext(input), errors, true);

        Assert.Equal(expected, valid);
        if (!expected) Assert.Contains(errors, e => e.MemberNames.Contains(nameof(UsageInput.Quantity)));
    }
}
