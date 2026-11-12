using FluentValidation;
using RazorPagesWebApp.Controllers;

namespace RazorPagesWebApp.Validation;

public sealed class CreateDocumentRequestValidator : AbstractValidator<CreateDocumentRequest>
{
    public CreateDocumentRequestValidator(ScannedDocumentStore scannedDocumentStore)
    {
        RuleFor(request => request.FileName)
            .NotEmpty().WithMessage("Nazwa pliku jest wymagana.")
            .MaximumLength(255).WithMessage("Nazwa pliku może mieć maksymalnie 255 znaków.");

        RuleFor(request => request.DocumentType)
            .NotEmpty().WithMessage("Typ dokumentu jest wymagany.")
            .MaximumLength(100).WithMessage("Typ dokumentu może mieć maksymalnie 100 znaków.");

        RuleFor(request => request.ThirdPartyDocumentId)
            .GreaterThan(0).WithMessage("Identyfikator zewnętrzny musi być większy od zera.");

        RuleFor(request => request.ThirdPartyDocumentId)
            .MustAsync(async (id, _) => await scannedDocumentStore.ExistsAsync(id))
            .When(request => request.ThirdPartyDocumentId > 0)
            .WithMessage("Podany identyfikator zewnętrzny nie istnieje.");
    }
}
