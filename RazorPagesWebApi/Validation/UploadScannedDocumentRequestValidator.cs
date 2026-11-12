using FluentValidation;
using RazorPagesWebApi.Controllers;

namespace RazorPagesWebApi.Validation;

public sealed class UploadScannedDocumentRequestValidator : AbstractValidator<UploadScannedDocumentRequest>
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    public UploadScannedDocumentRequestValidator()
    {
        RuleFor(request => request.File)
            .NotNull().WithMessage("Wybierz plik do przesłania.");

        RuleFor(request => request.File)
            .Must(file => file is { Length: > 0 })
            .When(request => request.File is not null)
            .WithMessage("Wybrany plik jest pusty.");

        RuleFor(request => request.File)
            .Must(file => file is null || file.Length <= MaxFileSize)
            .WithMessage("Maksymalny rozmiar pliku to 10 MB.");

        RuleFor(request => request.File!.FileName)
            .NotEmpty().WithMessage("Plik musi mieć nazwę.")
            .MaximumLength(255).WithMessage("Nazwa pliku może mieć maksymalnie 255 znaków.")
            .When(request => request.File is not null);
    }
}
