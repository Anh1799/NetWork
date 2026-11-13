using System.ComponentModel.DataAnnotations;
using TaskFlow.Api.Models;

namespace TaskFlow.Api.Dtos;

public class CreateTaskRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? DueDate { get; set; }
}

public class UpdateTaskRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public TaskItemStatus Status { get; set; }

    public TaskPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }
}

public class TaskQuery
{
    public TaskItemStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public string? Search { get; set; }

    /// <summary>title | duedate | priority | createdat (mặc định)</summary>
    public string? SortBy { get; set; }
    public bool Descending { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}

public record TaskResponse(
    int Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static TaskResponse From(TaskItem t) =>
        new(t.Id, t.Title, t.Description, t.Status, t.Priority, t.DueDate, t.CreatedAt, t.UpdatedAt);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
