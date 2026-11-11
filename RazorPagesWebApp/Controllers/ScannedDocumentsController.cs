using Microsoft.AspNetCore.Mvc;

namespace RazorPagesWebApp.Controllers;

[ApiController]
[Route("api/scanned-documents")]
public sealed class ScannedDocumentsController(DocumentStore documentStore, ScanStatusEvents statusEvents) : ControllerBase
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "InProgress",
        "Approved",
        "Rejected"
    };

    [HttpPost("{documentId:long}/status")]
    public async Task<IActionResult> UpdateStatus(long documentId, [FromBody] UpdateScanStatusRequest request)
    {
        if (!AllowedStatuses.Contains(request.Status))
        {
            return BadRequest(new
            {
                error = "Nieprawidłowy status.",
                allowedStatuses = AllowedStatuses
            });
        }

        var updated = await documentStore.UpdateScanStatusAsync(documentId, request.Status);
        
        if (!updated)
            return NotFound(new { error = "Nie znaleziono dokumentu lub jego rekordu skanowania." });

        statusEvents.Publish(new ScanStatusChanged(documentId, request.Status));
        return Ok(new { documentId, status = request.Status });
    }
}

public sealed record UpdateScanStatusRequest(string Status);
