using System.Net.Http.Json;

namespace RazorPagesWebApp;

public sealed class BackendApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<DocumentListItem>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<DocumentListItem>>("api/documents", cancellationToken) ?? [];
    }
}

public sealed record DocumentListItem(long Id, long ThirdPartyDocumentId, string FileName, string DocumentType,
    DateTimeOffset UploadedAt, string ScanStatus);
