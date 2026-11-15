using COSGeniusSystem.Commons;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace COSGeniusSystem.UnitTests;

public class ValidationErrorDataTests
{
    [Fact]
    public void FromModelState_PreservesSpecificEnglishValidationMessage()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Username", "Username is required.");

        var result = ValidationErrorData.FromModelState(modelState);

        Assert.Equal("Username is required.", Assert.Single(result.Errors["Username"]));
    }

    [Fact]
    public void FromModelState_UsesSafeEnglishFallbackForConversionException()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Quantity", new FormatException("Private parser details"), new EmptyModelMetadataProvider().GetMetadataForType(typeof(int)));

        var result = ValidationErrorData.FromModelState(modelState);

        var message = Assert.Single(result.Errors["Quantity"]);
        Assert.DoesNotContain("Private parser details", message);
        Assert.All(message, character => Assert.InRange((int)character, 0, 127));
    }
}
