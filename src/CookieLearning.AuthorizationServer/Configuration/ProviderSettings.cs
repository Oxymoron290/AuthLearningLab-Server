namespace CookieLearning.AuthorizationServer.Configuration;

public sealed class ProviderSettings
{
    public const string SectionName = "Provider";

    public required string InstanceId { get; init; }
    public required string DisplayName { get; init; }
    public required string Issuer { get; init; }
    public required string CookieName { get; init; }
}
