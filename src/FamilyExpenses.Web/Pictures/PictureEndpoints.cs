using FamilyExpenses.Application.Expenses;
using FamilyExpenses.Domain.Events;

namespace FamilyExpenses.Web.Pictures;

internal static class PictureEndpoints
{
    /// <summary>GET /pictures/{id}[?thumb=true]: an expense picture, only for members of its event.</summary>
    public static IEndpointRouteBuilder MapPictureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/pictures/{id:guid}", async (
                Guid id,
                bool? thumb,
                ExpensePictureService pictures,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var data = await pictures.GetAsync(id, thumb == true, cancellationToken);
                if (data is null)
                {
                    return Results.NotFound();
                }

                // A picture id never points at other bytes, so the browser may keep it. "private" keeps
                // shared caches (Cloudflare) from storing someone's receipt.
                http.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
                return Results.File(data, ExpensePicture.ContentType);
            })
            .RequireAuthorization();

        return endpoints;
    }
}
