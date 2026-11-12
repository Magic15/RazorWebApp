using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace RazorPagesWebApi.Controllers;

[ApiController]
[Route("api/scanned-documents/status-events")]
public sealed class ScanStatusEventsController(ScanStatusEvents statusEvents) : ControllerBase
{
    [HttpGet]
    public async Task StreamAsync()
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        var (subscriberId, reader) = statusEvents.Subscribe();
        try
        {
            await Response.WriteAsync(": connected\n\n", HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);

            await foreach (var update in reader.ReadAllAsync(HttpContext.RequestAborted))
            {
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
            statusEvents.Unsubscribe(subscriberId);
        }
    }
}
