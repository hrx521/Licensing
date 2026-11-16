using Licensing.Client;
using Licensing.Server.Services;

namespace Licensing.Server.Api;

public static class LicenseEndpoints
{
    public static void MapLicenseApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").DisableAntiforgery();
        group.MapPost("/activate", (LicenseCallRequest? request, ClientLicenseService service, HttpContext http, CancellationToken ct)
            => Handle(ClientOperation.Activate, request, service, http, ct));
        group.MapPost("/heartbeat", (LicenseCallRequest? request, ClientLicenseService service, HttpContext http, CancellationToken ct)
            => Handle(ClientOperation.Heartbeat, request, service, http, ct));
        group.MapPost("/deactivate", (LicenseCallRequest? request, ClientLicenseService service, HttpContext http, CancellationToken ct)
            => Handle(ClientOperation.Deactivate, request, service, http, ct));
        group.MapPost("/rehost", (LicenseCallRequest? request, ClientLicenseService service, HttpContext http, CancellationToken ct)
            => Handle(ClientOperation.Rehost, request, service, http, ct));
    }

    static async Task<IResult> Handle(ClientOperation operation, LicenseCallRequest? request, ClientLicenseService service, HttpContext http, CancellationToken cancellationToken)
    {
        try
        {
            var english = ApiText.English(http.Request);
            var response = await service.HandleAsync(operation, request, english, cancellationToken);
            return Results.Json(response, statusCode: StatusCode(response.Status));
        }
        catch (Exception ex)
        {
            var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("LicenseApi");
            logger.LogError(ex, "License API failed.");
            var english = ApiText.English(http.Request);
            return Results.Json(new LicenseCallResponse
            {
                Status = LicenseStatuses.ServerError,
                Message = ApiText.Get(english, "ServerError"),
                ServerTime = DateTimeOffset.UtcNow
            }, statusCode: 500);
        }
    }

    static int StatusCode(string status) => status switch
    {
        LicenseStatuses.Active or LicenseStatuses.Released => 200,
        LicenseStatuses.NotFound => 404,
        LicenseStatuses.Expired or LicenseStatuses.Revoked or LicenseStatuses.Suspended => 403,
        LicenseStatuses.SeatLimit or LicenseStatuses.MachineInUse or LicenseStatuses.RehostLimit or LicenseStatuses.DongleMismatch => 409,
        _ => 400
    };
}
