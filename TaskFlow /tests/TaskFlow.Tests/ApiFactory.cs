using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace TaskFlow.Tests;

/// <summary>
/// Khởi chạy toàn bộ API trong bộ nhớ với một SQLite in-memory riêng cho mỗi lần chạy test.
/// Connection "keeper" phải luôn mở, nếu không database in-memory sẽ bị xóa.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = $"Data Source=TaskFlowTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keeper;

    public ApiFactory()
    {
        _keeper = new SqliteConnection(_connectionString);
        _keeper.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keeper.Dispose();
    }
}
