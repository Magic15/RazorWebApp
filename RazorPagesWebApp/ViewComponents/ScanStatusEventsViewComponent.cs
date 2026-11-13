using Microsoft.AspNetCore.Mvc;

namespace RazorPagesWebApp.ViewComponents;

public sealed class ScanStatusEventsViewComponent(IConfiguration configuration) : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var apiBaseUrl = (configuration["BackendApi:BaseUrl"] ?? "http://localhost:5280").TrimEnd('/');
        var eventsPath = configuration["BackendApi:ScanStatusEventsPath"]
                         ?? throw new InvalidOperationException("BackendApi:ScanStatusEventsPath is not configured.");
        var eventsUrl = Uri.TryCreate(eventsPath, UriKind.Absolute, out var absoluteEventsUrl) &&
                        (absoluteEventsUrl.Scheme == Uri.UriSchemeHttp || absoluteEventsUrl.Scheme == Uri.UriSchemeHttps)
            ? absoluteEventsUrl.ToString()
            : new Uri(new Uri(apiBaseUrl + "/"), eventsPath.TrimStart('/')).ToString();

        return View("Default", new ScanStatusEventsComponentModel(apiBaseUrl, eventsUrl));
    }
}

public sealed record ScanStatusEventsComponentModel(string ApiBaseUrl, string EventsUrl);
