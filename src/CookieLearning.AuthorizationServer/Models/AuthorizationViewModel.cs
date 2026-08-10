namespace CookieLearning.AuthorizationServer.Models;

public sealed record AuthorizationViewModel(
    string ApplicationName,
    string ClientId,
    string Scope,
    string? UserName);
