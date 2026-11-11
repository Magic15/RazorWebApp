using Microsoft.Data.Sqlite;
using RazorPagesWebApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "data", "documents.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();
builder.Services.AddSingleton(new DocumentStore(connectionString));

var app = builder.Build();
await using (var connection = new SqliteConnection(connectionString))
{
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = """
        CREATE TABLE IF NOT EXISTS Documents (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FileName TEXT NOT NULL,
            DocumentType TEXT NOT NULL,
            ContentType TEXT NOT NULL,
            Data BLOB NOT NULL,
            UploadedAt TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS ScannedDocuments (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DocumentId INTEGER NOT NULL UNIQUE,
            Status TEXT NOT NULL CHECK (Status IN ('InProgress', 'Approved', 'Rejected')),
            UpdatedAt TEXT NOT NULL,
            FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
        );
        """;
    await command.ExecuteNonQueryAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();
app.MapControllers();

app.Run();
