using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;

namespace FamilyExpenses.Web.Hosting;

/// <summary>
/// The app runs behind Cloudflare Tunnel: cloudflared terminates HTTPS and forwards plain HTTP to
/// Kestrel on localhost with X-Forwarded-Proto/For. Honouring those headers makes the app see the
/// original https scheme (secure cookies, correct absolute invitation links) and client IP.
/// </summary>
internal static class ReverseProxyExtensions
{
    public const string KnownNetworksKey = "ReverseProxy:KnownNetworks";

    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Loopback (cloudflared on the Pi itself) is trusted by default. If cloudflared runs in Docker,
            // add its network, e.g. ReverseProxy__KnownNetworks__0=172.17.0.0/16.
            foreach (var network in configuration.GetSection(KnownNetworksKey).Get<string[]>() ?? [])
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        // Antiforgery's cookie isn't marked Secure by default; make it follow the (forwarded) scheme
        // like the Identity cookies do, so it is Secure through the tunnel.
        services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);

        return services;
    }
}
