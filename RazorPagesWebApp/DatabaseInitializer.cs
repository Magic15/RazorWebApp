using Microsoft.Data.Sqlite;

namespace RazorPagesWebApp;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ScannedDocuments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                ContentType TEXT NOT NULL,
                Data BLOB NOT NULL,
                Status TEXT NOT NULL CHECK (Status IN ('InProgress', 'Approved', 'Rejected')),
                UpdatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Documents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                DocumentType TEXT NOT NULL,
                ThirdPartyDocumentId INTEGER NOT NULL UNIQUE,
                UploadedAt TEXT NOT NULL,
                FOREIGN KEY (ThirdPartyDocumentId) REFERENCES ScannedDocuments(Id)
            );
            """;
        await command.ExecuteNonQueryAsync();
    }
}
