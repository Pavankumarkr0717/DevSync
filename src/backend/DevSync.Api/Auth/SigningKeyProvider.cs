using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevSync.Api.Auth;

/// <summary>
/// The access-token signing key (spec 002 research R5, NFR-004). Outside Development a configured
/// key of at least 256 bits is mandatory; in Development a temporary key is generated if none is set.
/// </summary>
public sealed class SigningKeyProvider
{
    public const int MinimumKeyBytes = 32;
    private const string Setting = "DevSync__Auth__SigningKey";

    public SigningKeyProvider(IOptions<AuthOptions> options, IHostEnvironment environment, ILogger<SigningKeyProvider> logger)
    {
        var configured = options.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"{Setting} is required outside Development: a base64 value of at least {MinimumKeyBytes} bytes.");
            }

            logger.LogWarning(
                "{Setting} is not set; using a temporary signing key (Development only). Access tokens are renewed after an API restart.",
                Setting);
            Key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(MinimumKeyBytes));
            return;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(configured);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{Setting} must be base64.");
        }

        if (bytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException($"{Setting} must decode to at least {MinimumKeyBytes} bytes.");
        }

        Key = new SymmetricSecurityKey(bytes);
    }

    public SymmetricSecurityKey Key { get; }
}
