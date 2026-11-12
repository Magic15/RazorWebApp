using Microsoft.Data.Sqlite;
using FluentValidation;
using RazorPagesWebApp;
using RazorPagesWebApp.Controllers;
using RazorPagesWebApp.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddScoped<IValidator<CreateDocumentRequest>, CreateDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<UploadScannedDocumentRequest>, UploadScannedDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateScanStatusRequest>, UpdateScanStatusRequestValidator>();
builder.Services.AddSingleton<ScanStatusEvents>();

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "data", "documents.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();
builder.Services.AddSingleton(new DocumentStore(connectionString));
builder.Services.AddSingleton(new ScannedDocumentStore(connectionString));

var app = builder.Build();
await DatabaseInitializer.InitializeAsync(connectionString);

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
