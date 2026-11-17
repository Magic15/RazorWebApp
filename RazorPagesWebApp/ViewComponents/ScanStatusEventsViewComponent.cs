using Microsoft.AspNetCore.Mvc;

namespace RazorPagesWebApp.ViewComponents;

public sealed class ScanStatusEventsViewComponent(IConfiguration configuration) : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var apiBaseUrl = (configuration["BackendApi:BaseUrl"] ?? "http://localhost:5280").TrimEnd('/');
        var hubPath = configuration["BackendApi:ScanStatusHubPath"]
                      ?? throw new InvalidOperationException("BackendApi:ScanStatusHubPath is not configured.");
        var hubUrl = Uri.TryCreate(hubPath, UriKind.Absolute, out var absoluteHubUrl) &&
                     (absoluteHubUrl.Scheme == Uri.UriSchemeHttp || absoluteHubUrl.Scheme == Uri.UriSchemeHttps)
            ? absoluteHubUrl.ToString()
            : new Uri(new Uri(apiBaseUrl + "/"), hubPath.TrimStart('/')).ToString();

        return View("Default", new ScanStatusEventsComponentModel(apiBaseUrl, hubUrl));
    }
}

public sealed record ScanStatusEventsComponentModel(string ApiBaseUrl, string HubUrl);
