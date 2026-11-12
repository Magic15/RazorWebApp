using FluentValidation;
using RazorPagesWebApp.Controllers;

namespace RazorPagesWebApp.Validation;

public sealed class UpdateScanStatusRequestValidator : AbstractValidator<UpdateScanStatusRequest>
{
    private static readonly string[] AllowedStatuses = ["InProgress", "Approved", "Rejected"];

    public UpdateScanStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => !string.IsNullOrWhiteSpace(status) && AllowedStatuses.Contains(status, StringComparer.Ordinal))
            .WithMessage($"Status musi mieć jedną z wartości: {string.Join(", ", AllowedStatuses)}.");
    }
}
