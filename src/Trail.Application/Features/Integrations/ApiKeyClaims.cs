namespace Trail.Application.Features.Integrations;

/// <summary>Claims on the principal an API key authenticates as. There is deliberately no subject (user) claim.</summary>
public static class ApiKeyClaims
{
    public const string ApiKeyId = "api_key_id";

    /// <summary>One claim per scope the key holds.</summary>
    public const string Scope = "scope";
}
