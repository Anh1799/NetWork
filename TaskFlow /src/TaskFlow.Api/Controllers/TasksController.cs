using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class TasksController(ITaskService tasks) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Danh sách task của tôi (lọc, tìm kiếm, sắp xếp, phân trang).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskResponse>>> GetAll([FromQuery] TaskQuery query, CancellationToken ct)
        => Ok(await tasks.GetAllAsync(UserId, query, ct));

    /// <summary>Lấy một task theo id.</summary>
    [HttpGet("{id:int}", Name = "GetTaskById")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(UserId, id, ct);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>Tạo task mới.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var created = await tasks.CreateAsync(UserId, request, ct);
        return CreatedAtRoute("GetTaskById", new { id = created.Id }, created);
    }

    /// <summary>Cập nhật toàn bộ task.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateTaskRequest request, CancellationToken ct)
    {
        var updated = await tasks.UpdateAsync(UserId, id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Đánh dấu task đã hoàn thành.</summary>
    [HttpPatch("{id:int}/complete")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
    {
        var task = await tasks.CompleteAsync(UserId, id, ct);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>Xóa task.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await tasks.DeleteAsync(UserId, id, ct) ? NoContent() : NotFound();
}
