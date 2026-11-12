using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace RazorPagesWebApp.Validation;

public static class ApiValidationExtensions
{
    public static IActionResult ToValidationProblem(this ControllerBase controller, ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        return controller.ValidationProblem(new ValidationProblemDetails(errors));
    }
}
