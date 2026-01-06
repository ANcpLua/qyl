using Microsoft.Agents.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;
using System.Collections.Concurrent;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;

namespace AgentGateway.Core;

public static class AspNetExtensions
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>
        _openIdMetadataCache = new();

    public static void AddBotAspNetAuthentication(this IServiceCollection services, IConfiguration configuration,
        string tokenValidationSectionName = "TokenValidation", ILogger? logger = null)
    {
        IConfigurationSection tokenValidationSection = configuration.GetSection(tokenValidationSectionName);
        
        if (!tokenValidationSection.Exists())
        {
            logger?.LogError(
                "Missing configuration section '{tokenValidationSectionName}'. This section is required to be present in appsettings.json",
                tokenValidationSectionName);
            throw new InvalidOperationException(
                $"Missing configuration section '{tokenValidationSectionName}'. This section is required to be present in appsettings.json");
        }

        List<string> validTokenIssuers = tokenValidationSection.GetSection("ValidIssuers").Get<List<string>>() ?? new List<string>();
        List<string> audiences = tokenValidationSection.GetSection("Audiences").Get<List<string>>() ?? new List<string>();

        // If ValidIssuers is empty, default for ABS Public Cloud
        if (validTokenIssuers.Count == 0)
        {
            validTokenIssuers =
            [
                "https://api.botframework.com",
                "https://sts.windows.net/d6d49420-f39b-4df7-a1dc-d59a935871db/",
                "https://login.microsoftonline.com/d6d49420-f39b-4df7-a1dc-d59a935871db/v2.0",
                "https://sts.windows.net/f8cdef31-a31e-4b4a-93e4-5f571e91255a/",
                "https://login.microsoftonline.com/f8cdef31-a31e-4b4a-93e4-5f571e91255a/v2.0",
                "https://sts.windows.net/69e9b82d-4842-4902-8d1e-abc5b98a55e8/",
                "https://login.microsoftonline.com/69e9b82d-4842-4902-8d1e-abc5b98a55e8/v2.0",
            ];

            string? tenantId = tokenValidationSection["TenantId"];
            if (!string.IsNullOrEmpty(tenantId))
            {
                validTokenIssuers.Add(string.Format(CultureInfo.InvariantCulture,
                    AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, tenantId));
                validTokenIssuers.Add(string.Format(CultureInfo.InvariantCulture,
                    AuthenticationConstants.ValidTokenIssuerUrlTemplateV2, tenantId));
            }
        }

        if (audiences.Count == 0)
        {
            throw new ArgumentException($"{tokenValidationSectionName}:Audiences requires at least one value");
        }

        bool isGov = tokenValidationSection.GetValue("IsGov", false);
        bool azureBotServiceTokenHandling = tokenValidationSection.GetValue("AzureBotServiceTokenHandling", true);

        string? azureBotServiceOpenIdMetadataUrl = tokenValidationSection["AzureBotServiceOpenIdMetadataUrl"];
        if (string.IsNullOrEmpty(azureBotServiceOpenIdMetadataUrl))
        {
            azureBotServiceOpenIdMetadataUrl = isGov
                ? AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl
                : AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl;
        }

        string? openIdMetadataUrl = tokenValidationSection["OpenIdMetadataUrl"];
        if (string.IsNullOrEmpty(openIdMetadataUrl))
        {
            openIdMetadataUrl =
                isGov ? AuthenticationConstants.GovOpenIdMetadataUrl : AuthenticationConstants.PublicOpenIdMetadataUrl;
        }

        TimeSpan openIdRefreshInterval = tokenValidationSection.GetValue("OpenIdMetadataRefresh",
            BaseConfigurationManager.DefaultAutomaticRefreshInterval);

        _ = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidIssuers = validTokenIssuers,
                    ValidAudiences = audiences,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                };

                // Using Microsoft.IdentityModel.Validators
                options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();

                options.Events = new JwtBearerEvents
                {
                    // Create a ConfigurationManager based on the requestor.  This is to handle ABS non-Entra tokens.
                    OnMessageReceived = async context =>
                    {
                        string? authorizationHeader = context.Request.Headers.Authorization.ToString();

                        if (string.IsNullOrEmpty(authorizationHeader))
                        {
                            // Default to AadTokenValidation handling
                            context.Options.TokenValidationParameters.ConfigurationManager ??=
                                options.ConfigurationManager as BaseConfigurationManager;
                            await Task.CompletedTask.ConfigureAwait(false);
                            return;
                        }

                        string[] parts = authorizationHeader.Split(' ');
                        if (parts.Length != 2 || parts[0] != "Bearer")
                        {
                            // Default to AadTokenValidation handling
                            context.Options.TokenValidationParameters.ConfigurationManager ??=
                                options.ConfigurationManager as BaseConfigurationManager;
                            await Task.CompletedTask.ConfigureAwait(false);
                            return;
                        }

                        JwtSecurityToken token = new(parts[1]);
                        string? issuer = token.Claims
                            .FirstOrDefault(claim => claim.Type == AuthenticationConstants.IssuerClaim)?.Value;

                        if (azureBotServiceTokenHandling &&
                            AuthenticationConstants.BotFrameworkTokenIssuer.Equals(issuer))
                        {
                            // Use the Bot Framework authority for this configuration manager
                            context.Options.TokenValidationParameters.ConfigurationManager =
                                _openIdMetadataCache.GetOrAdd(azureBotServiceOpenIdMetadataUrl, key =>
                                {
                                    return new ConfigurationManager<OpenIdConnectConfiguration>(
                                        azureBotServiceOpenIdMetadataUrl, new OpenIdConnectConfigurationRetriever(),
                                        new HttpClient())
                                    {
                                        AutomaticRefreshInterval = openIdRefreshInterval
                                    };
                                });
                        }
                        else
                        {
                            context.Options.TokenValidationParameters.ConfigurationManager =
                                _openIdMetadataCache.GetOrAdd(openIdMetadataUrl, key =>
                                {
                                    return new ConfigurationManager<OpenIdConnectConfiguration>(openIdMetadataUrl,
                                        new OpenIdConnectConfigurationRetriever(), new HttpClient())
                                    {
                                        AutomaticRefreshInterval = openIdRefreshInterval
                                    };
                                });
                        }

                        await Task.CompletedTask.ConfigureAwait(false);
                    },

                    OnTokenValidated = context =>
                    {
                        logger?.LogDebug("TOKEN Validated");
                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        logger?.LogWarning("Forbidden: {m}", context.Result?.ToString());
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        logger?.LogWarning("Auth Failed {m}", context.Exception.ToString());
                        return Task.CompletedTask;
                    }
                };
            });
    }
}