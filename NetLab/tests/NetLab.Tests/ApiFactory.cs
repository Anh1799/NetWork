using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NetLab.Tests;

/// <summary>Chạy toàn bộ API trong bộ nhớ (không cần database, không mở cổng thật).</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    protected virtual IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in Settings)
            builder.UseSetting(key, value);
    }
}

/// <summary>Phiên bản bật API key.</summary>
public class SecuredApiFactory : ApiFactory
{
    public const string Key = "test-secret-key";

    protected override IReadOnlyDictionary<string, string?> Settings =>
        new Dictionary<string, string?> { ["Security:ApiKey"] = Key };
}
