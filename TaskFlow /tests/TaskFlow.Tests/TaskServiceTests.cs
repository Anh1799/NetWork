using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Models;
using TaskFlow.Api.Services;

namespace TaskFlow.Tests;

public class TaskServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly TaskService _service;
    private readonly int _aliceId;
    private readonly int _bobId;

    public TaskServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var alice = new User { Username = "alice", Email = "alice@test.com", PasswordHash = "x" };
        var bob = new User { Username = "bob", Email = "bob@test.com", PasswordHash = "x" };
        _db.Users.AddRange(alice, bob);
        _db.SaveChanges();

        _aliceId = alice.Id;
        _bobId = bob.Id;
        _service = new TaskService(_db);
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsTask()
    {
        var created = await _service.CreateAsync(_aliceId, new CreateTaskRequest
        {
            Title = "  Write docs  ",
            Priority = TaskPriority.High
        });

        var fetched = await _service.GetByIdAsync(_aliceId, created.Id);

        Assert.NotNull(fetched);
        Assert.Equal("Write docs", fetched!.Title); // đã được Trim
        Assert.Equal(TaskPriority.High, fetched.Priority);
        Assert.Equal(TaskItemStatus.Todo, fetched.Status);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnTasks()
    {
        await _service.CreateAsync(_aliceId, new CreateTaskRequest { Title = "Alice task" });
        await _service.CreateAsync(_bobId, new CreateTaskRequest { Title = "Bob task" });

        var result = await _service.GetAllAsync(_aliceId, new TaskQuery());

        Assert.Single(result.Items);
        Assert.Equal("Alice task", result.Items[0].Title);
    }

    [Fact]
    public async Task GetAll_FiltersByStatus_AndPaginates()
    {
        for (var i = 1; i <= 5; i++)
            await _service.CreateAsync(_aliceId, new CreateTaskRequest { Title = $"Task {i}" });

        var first = await _service.GetAllAsync(_aliceId, new TaskQuery());
        await _service.CompleteAsync(_aliceId, first.Items[0].Id);

        var done = await _service.GetAllAsync(_aliceId, new TaskQuery { Status = TaskItemStatus.Done });
        Assert.Equal(1, done.TotalCount);

        var page2 = await _service.GetAllAsync(_aliceId, new TaskQuery { Page = 2, PageSize = 2, SortBy = "title" });
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal("Task 3", page2.Items[0].Title);
    }

    [Fact]
    public async Task GetAll_Search_MatchesTitle()
    {
        await _service.CreateAsync(_aliceId, new CreateTaskRequest { Title = "Learn ASP.NET" });
        await _service.CreateAsync(_aliceId, new CreateTaskRequest { Title = "Buy milk" });

        var result = await _service.GetAllAsync(_aliceId, new TaskQuery { Search = "asp" });

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Update_OtherUsersTask_ReturnsNull()
    {
        var bobTask = await _service.CreateAsync(_bobId, new CreateTaskRequest { Title = "Secret" });

        var result = await _service.UpdateAsync(_aliceId, bobTask.Id, new UpdateTaskRequest
        {
            Title = "Hacked",
            Status = TaskItemStatus.Done,
            Priority = TaskPriority.Low
        });

        Assert.Null(result);
        Assert.Equal("Secret", (await _service.GetByIdAsync(_bobId, bobTask.Id))!.Title);
    }

    [Fact]
    public async Task Delete_RemovesTask()
    {
        var created = await _service.CreateAsync(_aliceId, new CreateTaskRequest { Title = "Temp" });

        Assert.True(await _service.DeleteAsync(_aliceId, created.Id));
        Assert.Null(await _service.GetByIdAsync(_aliceId, created.Id));
        Assert.False(await _service.DeleteAsync(_aliceId, created.Id));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
