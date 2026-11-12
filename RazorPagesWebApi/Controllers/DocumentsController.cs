using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using RazorPagesWebApi.Validation;

namespace RazorPagesWebApi.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(
    DocumentStore documentStore,
    ScannedDocumentStore scannedDocumentStore,
    IValidator<CreateDocumentRequest> validator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        return Ok(await documentStore.ListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request)
    {
        var validation = await validator.ValidateAsync(request, HttpContext.RequestAborted);
        
        if (!validation.IsValid)
            return this.ToValidationProblem(validation);

        var documentId = await documentStore.CreateAsync(request.FileName, request.DocumentType.Trim(),
            request.ThirdPartyDocumentId);
        var createdDocument = new
        {
            documentId,
            request.ThirdPartyDocumentId,
            request.FileName,
            request.DocumentType
        };
        return CreatedAtAction(nameof(Get), new { id = documentId }, createdDocument);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var document = await documentStore.GetAsync(id);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpGet("{id:long}/file")]
    public async Task<IActionResult> Download(long id)
    {
        var thirdPartyDocumentId = await documentStore.GetThirdPartyDocumentIdAsync(id);
        if (thirdPartyDocumentId is null)
            return NotFound();

        var scannedFile = await scannedDocumentStore.GetAsync(thirdPartyDocumentId.Value);
        if (scannedFile is null)
            return NotFound();
        if (scannedFile.Status != "Approved")
            return Conflict(new { error = "Dokument można pobrać dopiero po zatwierdzeniu.", status = scannedFile.Status });

        return File(scannedFile.Document.Data, scannedFile.Document.ContentType, scannedFile.Document.FileName);
    }
}

public sealed record CreateDocumentRequest(string FileName, string DocumentType, long ThirdPartyDocumentId);
