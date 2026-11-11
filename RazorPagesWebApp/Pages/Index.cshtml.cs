using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorPagesWebApp;
using System.Text.Json;

namespace RazorPagesWebApp.Pages;

public class IndexModel : PageModel
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private readonly DocumentStore _documentStore;
    private readonly ScanStatusEvents _statusEvents;

    public IndexModel(DocumentStore documentStore, ScanStatusEvents statusEvents)
    {
        _documentStore = documentStore;
        _statusEvents = statusEvents;
    }

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
        var download = await _documentStore.GetDownloadAsync(id);
        if (download is null)
            return NotFound();
        if (download.Status != "Approved")
            return Forbid();

        return File(download.Document.Data, download.Document.ContentType, download.Document.FileName);
    }

    public async Task OnGetStatusEventsAsync()
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        var (subscriberId, reader) = _statusEvents.Subscribe();
        try
        {
            await Response.WriteAsync(": connected\n\n", HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);

            await foreach (var update in reader.ReadAllAsync(HttpContext.RequestAborted))
            {
                // Keep the event payload property names consistent with the JavaScript client.
                var json = JsonSerializer.Serialize(update, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                await Response.WriteAsync($"event: scan-status\ndata: {json}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // The browser closed the event stream.
        }
        finally
        {
            _statusEvents.Unsubscribe(subscriberId);
        }
    }
}
