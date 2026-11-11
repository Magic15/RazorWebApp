using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorPagesWebApp;

namespace RazorPagesWebApp.Pages;

public class IndexModel : PageModel
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private readonly DocumentStore _documentStore;

    public IndexModel(DocumentStore documentStore) => _documentStore = documentStore;

    public IReadOnlyList<DocumentSummary> Documents { get; private set; } = [];

    [BindProperty]
    public string DocumentType { get; set; } = string.Empty;

    [BindProperty]
    public IFormFile? Upload { get; set; }

    public async Task OnGetAsync()
    {
        Documents = await _documentStore.ListAsync();
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (string.IsNullOrWhiteSpace(DocumentType) || DocumentType.Length > 100)
            ModelState.AddModelError(nameof(DocumentType), "Wybierz typ dokumentu (maksymalnie 100 znaków).");
        if (Upload is null || Upload.Length == 0)
            ModelState.AddModelError(nameof(Upload), "Wybierz plik do zapisania.");
        else if (Upload.Length > MaxFileSize)
            ModelState.AddModelError(nameof(Upload), "Maksymalny rozmiar pliku to 10 MB.");

        if (!ModelState.IsValid)
        {
            Documents = await _documentStore.ListAsync();
            return Page();
        }

        await using var stream = new MemoryStream();
        await Upload!.CopyToAsync(stream);
        await _documentStore.SaveAsync(Path.GetFileName(Upload.FileName), DocumentType.Trim(),
            string.IsNullOrWhiteSpace(Upload.ContentType) ? "application/octet-stream" : Upload.ContentType,
            stream.ToArray());
        TempData["SuccessMessage"] = "Dokument został zapisany.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadAsync(long id)
    {
        var document = await _documentStore.GetAsync(id);
        if (document is null)
            return NotFound();

        return File(document.Data, document.ContentType, document.FileName);
    }
}
