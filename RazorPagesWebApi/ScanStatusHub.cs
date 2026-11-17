using Microsoft.AspNetCore.SignalR;

namespace RazorPagesWebApi;

public sealed class ScanStatusHub : Hub
{
}

public sealed record ScanStatusChanged(long ThirdPartyDocumentId, string Status);
