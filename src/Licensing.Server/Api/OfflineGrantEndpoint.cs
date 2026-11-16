using System.Globalization;
using Licensing.Server.Services;
using Microsoft.AspNetCore.Antiforgery;

namespace Licensing.Server.Api;

public static class OfflineGrantEndpoint
{
    public static void MapOfflineGrant(this WebApplication app)
    {
        app.MapPost("/licenses/{id:guid}/offline-file", async (Guid id, HttpContext http, IAntiforgery antiforgery, LicenseAdminService admin) =>
        {
            if (http.User.Identity?.IsAuthenticated != true)
                return Results.Redirect("/login");
            try
            {
                await antiforgery.ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Redirect("/login");
            }

            var form = await http.Request.ReadFormAsync();
            if (!DateOnly.TryParse(form["expires"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var expires))
                return Results.Redirect("/licenses/" + id + "?m=" + Uri.EscapeDataString("到期日无效。"));

            var result = await admin.PresignOfflineAsync(http.User, id, form["machineId"].ToString(), expires);
            if (result.Error != null)
                return Results.Redirect("/licenses/" + id + "?m=" + Uri.EscapeDataString(result.Error));
            return Results.File(result.Content!, "application/json", result.FileName);
        }).RequireAuthorization();
    }
}
