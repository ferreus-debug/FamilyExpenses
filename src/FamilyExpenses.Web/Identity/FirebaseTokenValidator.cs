using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FamilyExpenses.Web.Identity;

/// <summary>The verified person behind a Firebase ID token.</summary>
public sealed record FirebaseIdentity(string Uid, string? Email, bool EmailVerified, string? Name);

public interface IFirebaseTokenValidator
{
    /// <summary>Returns the identity for a valid Google sign-in token, or null if the token can't be trusted.</summary>
    Task<FirebaseIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Verifies Firebase ID tokens the way Firebase documents it: signed by Google's securetoken keys,
/// issued for this project, not expired, and with a non-empty subject (the Firebase uid).
/// </summary>
internal sealed partial class FirebaseTokenValidator : IFirebaseTokenValidator
{
    private const string GoogleProvider = "google.com";

    private readonly FirebaseOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configuration;
    private readonly ILogger<FirebaseTokenValidator> _logger;
    private readonly JsonWebTokenHandler _handler = new();

    public FirebaseTokenValidator(
        IOptions<FirebaseOptions> options,
        IConfigurationManager<OpenIdConnectConfiguration> configuration,
        ILogger<FirebaseTokenValidator> logger)
    {
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Downloads (and caches/refreshes) Google's signing keys from the issuer's discovery document.</summary>
    public static IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(FirebaseOptions options) =>
        new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{options.Issuer}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });

    public async Task<FirebaseIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(_options.ProjectId))
        {
            return null;
        }

        var configuration = await _configuration.GetConfigurationAsync(cancellationToken);
        var result = await _handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.ProjectId,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        });

        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
        {
            LogRejected(_logger, result.Exception);
            return null;
        }

        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(token.EncodedPayload));
        var root = payload.RootElement;

        var uid = String(root, "sub");
        if (string.IsNullOrEmpty(uid) || SignInProvider(root) != GoogleProvider)
        {
            LogNotGoogle(_logger);
            return null;
        }

        var emailVerified = root.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True;
        return new FirebaseIdentity(uid, String(root, "email"), emailVerified, String(root, "name"));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected a Firebase ID token")]
    private static partial void LogRejected(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected a Firebase ID token without a uid or not from Google sign-in")]
    private static partial void LogNotGoogle(ILogger logger);

    private static string? SignInProvider(JsonElement root) =>
        root.TryGetProperty("firebase", out var firebase) && firebase.ValueKind == JsonValueKind.Object
            ? String(firebase, "sign_in_provider")
            : null;

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
