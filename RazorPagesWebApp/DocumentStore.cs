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

    public async Task SaveAsync(string fileName, string documentType, string contentType, byte[] data)
    {
        const string insertScannedDocument = """
            INSERT INTO ScannedDocuments (FileName, ContentType, Data, Status, UpdatedAt)
            VALUES (@FileName, @ContentType, @Data, 'InProgress', @UploadedAt);
            SELECT last_insert_rowid();
            """;
        const string insertDocument = """
            INSERT INTO Documents (FileName, DocumentType, ThirdPartyDocumentId, UploadedAt)
            VALUES (@FileName, @DocumentType, @ThirdPartyDocumentId, @UploadedAt);
            """;

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var uploadedAt = DateTimeOffset.UtcNow.ToString("O");
        var parameters = new { FileName = fileName, ContentType = contentType, Data = data, DocumentType = documentType, UploadedAt = uploadedAt };

        var externalId = await connection.ExecuteScalarAsync<long>(insertScannedDocument, parameters, transaction);
        await connection.ExecuteAsync(insertDocument,
            new { parameters.FileName, parameters.DocumentType, ThirdPartyDocumentId = externalId, parameters.UploadedAt },
            transaction);
        await transaction.CommitAsync();
    }

    public async Task<bool> UpdateScanStatusAsync(long thirdPartyDocumentId, string status)
    {
        const string sql = "UPDATE ScannedDocuments SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        await using var connection = new SqliteConnection(connectionString);
        var rowsUpdated = await connection.ExecuteAsync(sql, new
        {
            Status = status,
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Id = thirdPartyDocumentId
        });
        return rowsUpdated > 0;
    }

    public async Task<DocumentDownloadResult?> GetDownloadAsync(long documentId)
    {
        const string sql = """
            SELECT s.FileName, s.ContentType, s.Data, s.Status
            FROM Documents d JOIN ScannedDocuments s ON s.Id = d.ThirdPartyDocumentId
            WHERE d.Id = @Id
            """;

        await using var connection = new SqliteConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<DocumentDownloadRow>(sql, new { Id = documentId });
        return row is null
            ? null
            : new DocumentDownloadResult(new StoredDocument(row.FileName, row.ContentType, row.Data), row.Status);
    }
}

public sealed record DocumentSummary(long Id, long ThirdPartyDocumentId, string FileName, string DocumentType,
    DateTimeOffset UploadedAt, string ScanStatus);
public sealed record StoredDocument(string FileName, string ContentType, byte[] Data);
public sealed record DocumentDownloadResult(StoredDocument Document, string Status);
internal sealed record DocumentListRow(long Id, long ThirdPartyDocumentId, string FileName, string DocumentType,
    string UploadedAt, string Status);
internal sealed record DocumentDownloadRow(string FileName, string ContentType, byte[] Data, string Status);
