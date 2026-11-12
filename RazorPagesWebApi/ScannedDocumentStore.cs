using Dapper;
using Microsoft.Data.Sqlite;

namespace RazorPagesWebApi;

/// <summary>Local stand-in for the third-party antivirus document API and storage.</summary>
public sealed class ScannedDocumentStore(string connectionString)
{
    public async Task<bool> ExistsAsync(long thirdPartyDocumentId)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM ScannedDocuments WHERE Id = @Id)";
        await using var connection = new SqliteConnection(connectionString);
        return await connection.ExecuteScalarAsync<bool>(sql, new { Id = thirdPartyDocumentId });
    }

    public async Task<long> UploadAsync(string fileName, string contentType, byte[] data)
    {
        const string sql = """
            INSERT INTO ScannedDocuments (FileName, ContentType, Data, Status, UpdatedAt)
            VALUES (@FileName, @ContentType, @Data, 'InProgress', @UpdatedAt);
            SELECT last_insert_rowid();
            """;

        await using var connection = new SqliteConnection(connectionString);
        return await connection.ExecuteScalarAsync<long>(sql, new
        {
            FileName = fileName,
            ContentType = contentType,
            Data = data,
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    public async Task<bool> UpdateStatusAsync(long thirdPartyDocumentId, string status)
    {
        const string sql = "UPDATE ScannedDocuments SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        await using var connection = new SqliteConnection(connectionString);
        return await connection.ExecuteAsync(sql, new
        {
            Status = status,
            UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Id = thirdPartyDocumentId
        }) > 0;
    }

    public async Task<ScannedFileResult?> GetAsync(long thirdPartyDocumentId)
    {
        const string sql = """
            SELECT FileName, ContentType, Data, Status
            FROM ScannedDocuments
            WHERE Id = @Id
            """;

        await using var connection = new SqliteConnection(connectionString);
        var row = await connection.QuerySingleOrDefaultAsync<ScannedFileRow>(sql, new { Id = thirdPartyDocumentId });
        return row is null
            ? null
            : new ScannedFileResult(new StoredDocument(row.FileName, row.ContentType, row.Data), row.Status);
    }
}

public sealed record StoredDocument(string FileName, string ContentType, byte[] Data);
public sealed record ScannedFileResult(StoredDocument Document, string Status);
internal sealed record ScannedFileRow(string FileName, string ContentType, byte[] Data, string Status);
