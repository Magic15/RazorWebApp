using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorPagesWebApp;
using System.Text.Json;

namespace RazorPagesWebApp.Pages;

public class IndexModel : PageModel
{
    private readonly DocumentStore _documentStore;
    private readonly ScanStatusEvents _statusEvents;

    public IndexModel(DocumentStore documentStore, ScanStatusEvents statusEvents)
    {
        _documentStore = documentStore;
        _statusEvents = statusEvents;
    }

    public IReadOnlyList<DocumentSummary> Documents { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Documents = await _documentStore.ListAsync();
    }

    public async Task<IActionResult> OnGetDownloadAsync(long id)
    {
        var thirdPartyDocumentId = await _documentStore.GetThirdPartyDocumentIdAsync(id);
        if (thirdPartyDocumentId is null)
            return NotFound();
        return Redirect($"/api/scanned-documents/{thirdPartyDocumentId.Value}/file");
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
