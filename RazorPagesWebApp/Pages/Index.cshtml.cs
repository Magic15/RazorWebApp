using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorPagesWebApp.Pages;

public class IndexModel(BackendApiClient backendApiClient, IConfiguration configuration) : PageModel
{
    public IReadOnlyList<DocumentListItem> Documents { get; private set; } = [];
    public string BackendApiBaseUrl { get; } =
        (configuration["BackendApi:BaseUrl"] ?? "http://localhost:5280").TrimEnd('/');
    public string? BackendError { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            Documents = await backendApiClient.GetDocumentsAsync(HttpContext.RequestAborted);
        }
        catch (HttpRequestException)
        {
            BackendError = "Nie można połączyć się z backendem. Sprawdź, czy API jest uruchomione.";
        }
    }
}
