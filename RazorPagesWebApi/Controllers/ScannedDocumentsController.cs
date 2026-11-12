using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using RazorPagesWebApi.Validation;

namespace RazorPagesWebApi.Controllers;

[ApiController]
[Route("api/scanned-documents")]
public sealed class ScannedDocumentsController(
    ScannedDocumentStore scannedDocumentStore,
    ScanStatusEvents statusEvents,
    IValidator<UploadScannedDocumentRequest> uploadValidator,
    IValidator<UpdateScanStatusRequest> statusValidator) : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadScannedDocumentRequest request)
    {
        var validation = await uploadValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
            return this.ToValidationProblem(validation);

        var file = request.File!;
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var fileName = Path.GetFileName(file.FileName);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        var externalId = await scannedDocumentStore.UploadAsync(fileName, contentType, stream.ToArray());

        return CreatedAtAction(nameof(Get), new { thirdPartyDocumentId = externalId }, new
        {
            thirdPartyDocumentId = externalId,
            fileName,
            status = "InProgress"
        });
    }

    [HttpGet("{thirdPartyDocumentId:long}")]
    public async Task<IActionResult> Get(long thirdPartyDocumentId)
    {
        var scannedFile = await scannedDocumentStore.GetAsync(thirdPartyDocumentId);
        if (scannedFile is null)
            return NotFound();

        return Ok(new
        {
            thirdPartyDocumentId,
            fileName = scannedFile.Document.FileName,
            status = scannedFile.Status
        });
    }

    [HttpGet("{thirdPartyDocumentId:long}/file")]
    public async Task<IActionResult> Download(long thirdPartyDocumentId)
    {
        var scannedFile = await scannedDocumentStore.GetAsync(thirdPartyDocumentId);
        if (scannedFile is null)
            return NotFound();
        if (scannedFile.Status != "Approved")
            return Conflict(new { error = "Dokument można pobrać dopiero po zatwierdzeniu.", status = scannedFile.Status });

        return File(scannedFile.Document.Data, scannedFile.Document.ContentType, scannedFile.Document.FileName);
    }

    [HttpPut("{thirdPartyDocumentId:long}/status")]
    public async Task<IActionResult> UpdateStatus(long thirdPartyDocumentId, [FromBody] UpdateScanStatusRequest request)
    {
        var validation = await statusValidator.ValidateAsync(request, HttpContext.RequestAborted);
        if (!validation.IsValid)
            return this.ToValidationProblem(validation);

        var updated = await scannedDocumentStore.UpdateStatusAsync(thirdPartyDocumentId, request.Status);
        
        if (!updated)
            return NotFound(new { error = "Nie znaleziono dokumentu lub jego rekordu skanowania." });

        statusEvents.Publish(new ScanStatusChanged(thirdPartyDocumentId, request.Status));
        return Ok(new { thirdPartyDocumentId, status = request.Status });
    }
}

public sealed record UpdateScanStatusRequest(string Status);
public sealed record UploadScannedDocumentRequest(IFormFile? File);
