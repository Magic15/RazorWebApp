using Dapper;
using Microsoft.Data.Sqlite;

namespace RazorPagesWebApp;

public sealed class DocumentStore(string connectionString)
{
    public async Task<IReadOnlyList<DocumentSummary>> ListAsync()
    {
        const string sql = """
            SELECT d.Id, d.ThirdPartyDocumentId, d.FileName, d.DocumentType, d.UploadedAt, s.Status
            FROM Documents d JOIN ScannedDocuments s ON s.Id = d.ThirdPartyDocumentId
            ORDER BY d.Id DESC
            """;

        await using var connection = new SqliteConnection(connectionString);
        var rows = await connection.QueryAsync<DocumentListRow>(sql);
        return rows.Select(row => new DocumentSummary(row.Id, row.ThirdPartyDocumentId, row.FileName,
            row.DocumentType, DateTimeOffset.Parse(row.UploadedAt), row.Status)).ToList();
    }

    public async Task<long> CreateAsync(string fileName, string documentType, long thirdPartyDocumentId)
    {
        const string sql = """
            INSERT INTO Documents (FileName, DocumentType, ThirdPartyDocumentId, UploadedAt)
            VALUES (@FileName, @DocumentType, @ThirdPartyDocumentId, @UploadedAt);
            SELECT last_insert_rowid();
            """;

        await using var connection = new SqliteConnection(connectionString);
        return await connection.ExecuteScalarAsync<long>(sql, new
        {
            FileName = fileName,
            DocumentType = documentType,
            ThirdPartyDocumentId = thirdPartyDocumentId,
            UploadedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    public async Task<long?> GetThirdPartyDocumentIdAsync(long documentId)
    {
        const string sql = "SELECT ThirdPartyDocumentId FROM Documents WHERE Id = @Id";
        await using var connection = new SqliteConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<long?>(sql, new { Id = documentId });
    }

    public async Task<DocumentSummary?> GetAsync(long documentId)
    {
        const string sql = """
            SELECT d.Id, d.ThirdPartyDocumentId, d.FileName, d.DocumentType, d.UploadedAt, s.Status
            FROM Documents d JOIN ScannedDocuments s ON s.Id = d.ThirdPartyDocumentId
            WHERE d.Id = @Id
            """;
        await using var connection = new SqliteConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<DocumentListRow>(sql, new { Id = documentId });
        return row is null
            ? null
            : new DocumentSummary(row.Id, row.ThirdPartyDocumentId, row.FileName, row.DocumentType,
                DateTimeOffset.Parse(row.UploadedAt), row.Status);
    }
}

public sealed record DocumentSummary(long Id, long ThirdPartyDocumentId, string FileName, string DocumentType,
    DateTimeOffset UploadedAt, string ScanStatus);
internal sealed record DocumentListRow(long Id, long ThirdPartyDocumentId, string FileName, string DocumentType,
    string UploadedAt, string Status);
