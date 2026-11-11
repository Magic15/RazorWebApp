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
            SELECT d.Id, d.FileName, d.DocumentType, d.UploadedAt, s.Status
            FROM Documents d LEFT JOIN ScannedDocuments s ON s.DocumentId = d.Id
            ORDER BY d.Id DESC
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            documents.Add(new DocumentSummary(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3)), reader.IsDBNull(4) ? "Brak" : reader.GetString(4)));
        }
        return documents;
    }

    public async Task SaveAsync(string fileName, string documentType, string contentType, byte[] data)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Documents (FileName, DocumentType, ContentType, Data, UploadedAt)
            VALUES ($name, $type, $contentType, $data, $uploadedAt);
            INSERT INTO ScannedDocuments (DocumentId, Status, UpdatedAt)
            VALUES (last_insert_rowid(), 'InProgress', $uploadedAt);
            """;
        command.Parameters.AddWithValue("$name", fileName);
        command.Parameters.AddWithValue("$type", documentType);
        command.Parameters.AddWithValue("$contentType", contentType);
        command.Parameters.AddWithValue("$data", data);
        command.Parameters.AddWithValue("$uploadedAt", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> UpdateScanStatusAsync(long documentId, string status)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ScannedDocuments SET Status = $status, UpdatedAt = $updatedAt WHERE DocumentId = $id";
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", documentId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<StoredDocument?> GetAsync(long id)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FileName, ContentType, Data FROM Documents WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new StoredDocument(reader.GetString(0), reader.GetString(1), (byte[])reader[2])
            : null;
    }
}

public sealed record DocumentSummary(long Id, string FileName, string DocumentType, DateTimeOffset UploadedAt, string ScanStatus);
public sealed record StoredDocument(string FileName, string ContentType, byte[] Data);
