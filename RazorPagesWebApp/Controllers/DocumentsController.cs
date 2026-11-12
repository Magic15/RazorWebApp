using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using RazorPagesWebApp.Validation;

namespace RazorPagesWebApp.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(DocumentStore documentStore, IValidator<CreateDocumentRequest> validator) : ControllerBase
{
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
}

public sealed record CreateDocumentRequest(string FileName, string DocumentType, long ThirdPartyDocumentId);
