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

    [HttpPost("{thirdPartyDocumentId:long}/status")]
    public async Task<IActionResult> UpdateStatus(long thirdPartyDocumentId, [FromBody] UpdateScanStatusRequest request)
    {
        if (!AllowedStatuses.Contains(request.Status))
        {
            return BadRequest(new
            {
                error = "Nieprawidłowy status.",
                allowedStatuses = AllowedStatuses
            });
        }

        var updated = await documentStore.UpdateScanStatusAsync(thirdPartyDocumentId, request.Status);
        
        if (!updated)
            return NotFound(new { error = "Nie znaleziono dokumentu lub jego rekordu skanowania." });

        statusEvents.Publish(new ScanStatusChanged(thirdPartyDocumentId, request.Status));
        return Ok(new { thirdPartyDocumentId, status = request.Status });
    }
}

public sealed record UpdateScanStatusRequest(string Status);
