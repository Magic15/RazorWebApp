using FluentValidation;
using Microsoft.Data.Sqlite;
using RazorPagesWebApi;
using RazorPagesWebApi.Controllers;
using RazorPagesWebApi.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddScoped<IValidator<CreateDocumentRequest>, CreateDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<UploadScannedDocumentRequest>, UploadScannedDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateScanStatusRequest>, UpdateScanStatusRequestValidator>();
builder.Services.AddSingleton<ScanStatusEvents>();

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "data", "documents.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    ForeignKeys = true
}.ToString();
builder.Services.AddSingleton(new DocumentStore(connectionString));
builder.Services.AddSingleton(new ScannedDocumentStore(connectionString));

var app = builder.Build();
await DatabaseInitializer.InitializeAsync(connectionString);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
