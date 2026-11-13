namespace TaskFlow.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TaskFlow";
    public string Audience { get; set; } = "TaskFlow.Clients";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
