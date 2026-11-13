using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Models;

namespace TaskFlow.Api.Services;

public interface ITaskService
{
    Task<PagedResult<TaskResponse>> GetAllAsync(int userId, TaskQuery query, CancellationToken ct = default);
    Task<TaskResponse?> GetByIdAsync(int userId, int id, CancellationToken ct = default);
    Task<TaskResponse> CreateAsync(int userId, CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskResponse?> UpdateAsync(int userId, int id, UpdateTaskRequest request, CancellationToken ct = default);
    Task<TaskResponse?> CompleteAsync(int userId, int id, CancellationToken ct = default);
    Task<bool> DeleteAsync(int userId, int id, CancellationToken ct = default);
}

public class TaskService(AppDbContext db) : ITaskService
{
    public async Task<PagedResult<TaskResponse>> GetAllAsync(int userId, TaskQuery query, CancellationToken ct = default)
    {
        // Luôn lọc theo userId: người dùng chỉ thấy task của chính mình
        IQueryable<TaskItem> q = db.Tasks.AsNoTracking().Where(t => t.UserId == userId);

        if (query.Status is not null)
            q = q.Where(t => t.Status == query.Status);

        if (query.Priority is not null)
            q = q.Where(t => t.Priority == query.Priority);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(t => t.Title.Contains(term) || (t.Description != null && t.Description.Contains(term)));
        }

        q = query.SortBy?.ToLowerInvariant() switch
        {
            "title" => query.Descending ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title),
            "duedate" => query.Descending ? q.OrderByDescending(t => t.DueDate) : q.OrderBy(t => t.DueDate),
            "priority" => query.Descending ? q.OrderByDescending(t => t.Priority) : q.OrderBy(t => t.Priority),
            _ => query.Descending ? q.OrderByDescending(t => t.CreatedAt) : q.OrderBy(t => t.CreatedAt)
        };

        var total = await q.CountAsync(ct);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<TaskResponse>(
            items.Select(TaskResponse.From).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<TaskResponse?> GetByIdAsync(int userId, int id, CancellationToken ct = default)
    {
        var task = await db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

        return task is null ? null : TaskResponse.From(task);
    }

    public async Task<TaskResponse> CreateAsync(int userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        var task = new TaskItem
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = request.Priority,
            DueDate = request.DueDate
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);

        return TaskResponse.From(task);
    }

    public async Task<TaskResponse?> UpdateAsync(int userId, int id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
        if (task is null) return null;

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.DueDate = request.DueDate;
        task.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return TaskResponse.From(task);
    }

    public async Task<TaskResponse?> CompleteAsync(int userId, int id, CancellationToken ct = default)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
        if (task is null) return null;

        task.Status = TaskItemStatus.Done;
        task.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return TaskResponse.From(task);
    }

    public async Task<bool> DeleteAsync(int userId, int id, CancellationToken ct = default)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
        if (task is null) return false;

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
