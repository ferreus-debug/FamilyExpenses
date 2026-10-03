using System.Security.Cryptography;
using FamilyExpenses.Web.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FamilyExpenses.Web.Tests;

public sealed class FirebaseTokenValidatorTests : IDisposable
{
    private const string ProjectId = "turkassen";
    private const string Issuer = "https://securetoken.google.com/turkassen";

    private readonly RSA _googleKey = RSA.Create(2048);
    private readonly FirebaseTokenValidator _validator;

    public FirebaseTokenValidatorTests()
    {
        // Stands in for Google's published signing keys.
        var configuration = new OpenIdConnectConfiguration();
        configuration.SigningKeys.Add(new RsaSecurityKey(_googleKey) { KeyId = "google" });

        _validator = new FirebaseTokenValidator(
            Options.Create(new FirebaseOptions { ProjectId = ProjectId }),
            new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration),
            NullLogger<FirebaseTokenValidator>.Instance);
    }

    public void Dispose() => _googleKey.Dispose();

    [Fact]
    public async Task Valid_google_sign_in_token_gives_the_identity()
    {
        var identity = await _validator.ValidateAsync(Token());

        identity.ShouldBe(new FirebaseIdentity("uid-anna", "anna@example.com", true, "Anna Andersen"));
    }

    [Fact]
    public async Task Token_for_another_firebase_project_is_rejected() =>
        (await _validator.ValidateAsync(Token(audience: "someone-elses-project"))).ShouldBeNull();

    [Fact]
    public async Task Token_from_another_issuer_is_rejected() =>
        (await _validator.ValidateAsync(Token(issuer: "https://securetoken.google.com/someone-elses-project"))).ShouldBeNull();

    [Fact]
    public async Task Expired_token_is_rejected() =>
        (await _validator.ValidateAsync(Token(expires: DateTime.UtcNow.AddMinutes(-5)))).ShouldBeNull();

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        using var forger = RSA.Create(2048);

        (await _validator.ValidateAsync(Token(signingKey: forger))).ShouldBeNull();
    }

    [Fact]
    public async Task Sign_in_with_another_provider_than_google_is_rejected() =>
        (await _validator.ValidateAsync(Token(provider: "anonymous"))).ShouldBeNull();

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Garbage_is_rejected(string idToken) =>
        (await _validator.ValidateAsync(idToken)).ShouldBeNull();

    private string Token(
        string issuer = Issuer,
        string audience = ProjectId,
        DateTime? expires = null,
        string provider = "google.com",
        RSA? signingKey = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddHours(1);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiresAt.AddHours(-1),
            NotBefore = expiresAt.AddHours(-1),
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "uid-anna",
                ["email"] = "anna@example.com",
                ["email_verified"] = true,
                ["name"] = "Anna Andersen",
                ["firebase"] = new Dictionary<string, object> { ["sign_in_provider"] = provider },
            },
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(signingKey ?? _googleKey) { KeyId = "google" }, SecurityAlgorithms.RsaSha256),
        });
    }
}
