using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Models;

namespace TaskFlow.Tests;

public class ApiIntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var name = "user" + Guid.NewGuid().ToString("N")[..8];
        var response = await _client.PostAsJsonAsync("api/auth/register",
            new { username = name, email = $"{name}@test.com", password = "Secret123" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        return auth!.Token;
    }

    private HttpClient Authorized(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Tasks_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidData_Returns400()
    {
        var response = await _client.PostAsJsonAsync("api/auth/register",
            new { username = "ab", email = "not-an-email", password = "1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUser_Returns409()
    {
        var name = "dup" + Guid.NewGuid().ToString("N")[..8];
        var body = new { username = name, email = $"{name}@test.com", password = "Secret123" };

        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("api/auth/register", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("api/auth/register", body)).StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var name = "login" + Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("api/auth/register",
            new { username = name, email = $"{name}@test.com", password = "Secret123" });

        var response = await _client.PostAsJsonAsync("api/auth/login", new { username = name, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FullFlow_CreateListUpdateCompleteDelete()
    {
        var client = Authorized(await RegisterAndGetTokenAsync());

        // Create
        var create = await client.PostAsJsonAsync("api/tasks", new { title = "Write tests", priority = "High" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var created = (await create.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
        Assert.Equal(TaskPriority.High, created.Priority);

        // List
        var list = await client.GetFromJsonAsync<PagedResult<TaskResponse>>("api/tasks?search=write", Json);
        Assert.Single(list!.Items);

        // Update
        var update = await client.PutAsJsonAsync($"api/tasks/{created.Id}",
            new { title = "Write more tests", status = "InProgress", priority = "Low" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // Complete
        var complete = await client.PatchAsync($"api/tasks/{created.Id}/complete", null);
        var completed = (await complete.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
        Assert.Equal(TaskItemStatus.Done, completed.Status);

        // Delete
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"api/tasks/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"api/tasks/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Users_CannotSeeEachOthersTasks()
    {
        var alice = Authorized(await RegisterAndGetTokenAsync());
        var bob = Authorized(await RegisterAndGetTokenAsync());

        var create = await alice.PostAsJsonAsync("api/tasks", new { title = "Private" });
        var created = (await create.Content.ReadFromJsonAsync<TaskResponse>(Json))!;

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"api/tasks/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Holidays_InvalidCountryCode_Returns400()
    {
        var client = Authorized(await RegisterAndGetTokenAsync());

        var response = await client.GetAsync("api/holidays/2026/VNM");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NetworkInfo_IsPublic_AndHealthIsOk()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("api/network/info")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("health")).StatusCode);
    }
}
