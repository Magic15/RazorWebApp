using Microsoft.Data.Sqlite;

namespace RazorPagesWebApp;

public sealed class DocumentStore(string connectionString)
{
    public async Task<IReadOnlyList<DocumentSummary>> ListAsync()
    {
        var documents = new List<DocumentSummary>();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.Id, d.ThirdPartyDocumentId, d.FileName, d.DocumentType, d.UploadedAt, s.Status
            FROM Documents d JOIN ScannedDocuments s ON s.Id = d.ThirdPartyDocumentId
            ORDER BY d.Id DESC
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            documents.Add(new DocumentSummary(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4)), reader.GetString(5)));
        }
        return documents;
    }

    public async Task SaveAsync(string fileName, string documentType, string contentType, byte[] data)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            var uploadedAt = DateTimeOffset.UtcNow.ToString("O");
            await using var scanCommand = connection.CreateCommand();
            scanCommand.Transaction = (SqliteTransaction)transaction;
            scanCommand.CommandText = "INSERT INTO ScannedDocuments (FileName, ContentType, Data, Status, UpdatedAt) VALUES ($name, $contentType, $data, 'InProgress', $uploadedAt); SELECT last_insert_rowid();";
            scanCommand.Parameters.AddWithValue("$name", fileName);
            scanCommand.Parameters.AddWithValue("$contentType", contentType);
            scanCommand.Parameters.AddWithValue("$data", data);
            scanCommand.Parameters.AddWithValue("$uploadedAt", uploadedAt);
            var externalId = (long)(await scanCommand.ExecuteScalarAsync())!;

            await using var documentCommand = connection.CreateCommand();
            documentCommand.Transaction = (SqliteTransaction)transaction;
            documentCommand.CommandText = "INSERT INTO Documents (FileName, DocumentType, ThirdPartyDocumentId, UploadedAt) VALUES ($name, $type, $externalId, $uploadedAt)";
            documentCommand.Parameters.AddWithValue("$name", fileName);
            documentCommand.Parameters.AddWithValue("$type", documentType);
            documentCommand.Parameters.AddWithValue("$externalId", externalId);
            documentCommand.Parameters.AddWithValue("$uploadedAt", uploadedAt);
            await documentCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateScanStatusAsync(long thirdPartyDocumentId, string status)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ScannedDocuments SET Status = $status, UpdatedAt = $updatedAt WHERE Id = $id";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", thirdPartyDocumentId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<DocumentDownloadResult?> GetDownloadAsync(long documentId)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.FileName, s.ContentType, s.Data, s.Status
            FROM Documents d JOIN ScannedDocuments s ON s.Id = d.ThirdPartyDocumentId
            WHERE d.Id = $id
            """;
        command.Parameters.AddWithValue("$id", documentId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new DocumentDownloadResult(new StoredDocument(reader.GetString(0), reader.GetString(1), (byte[])reader[2]),
                reader.GetString(3))
            : null;
    }
}

public sealed record DocumentSummary(long Id, long ThirdPartyDocumentId, string FileName, string DocumentType,
    DateTimeOffset UploadedAt, string ScanStatus);
public sealed record StoredDocument(string FileName, string ContentType, byte[] Data);
public sealed record DocumentDownloadResult(StoredDocument Document, string Status);
