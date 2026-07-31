using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TaskManager.Api.DTOs;
using TaskManager.Api.Hubs;
using TaskManager.Api.Services; 

namespace TaskManager.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IHubContext<TaskHub> _hubContext;

    public TasksController(ITaskService taskService, IHubContext<TaskHub> hubContext)
    {
        _taskService = taskService;
        _hubContext = hubContext;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // GET: api/tasks
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponseDto>>> GetTasks()
    {
        var tasks = await _taskService.GetTasksAsync(CurrentUserId);
        return Ok(tasks);
    }

    // GET: api/tasks/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<TaskResponseDto>> GetTask(int id)
    {
        var task = await _taskService.GetTaskByIdAsync(id, CurrentUserId);
        if (task == null) return NotFound();

        return Ok(task);
    }

    // POST: api/tasks
    [HttpPost]
    public async Task<ActionResult<TaskResponseDto>> CreateTask(CreateTaskDto dto)
    {
        var response = await _taskService.CreateTaskAsync(dto, CurrentUserId);

        // Real-time broadcast
        await _hubContext.Clients.All.SendAsync("TaskCreated", response);

        return CreatedAtAction(nameof(GetTask), new { id = response.Id }, response);
    }

    // PUT: api/tasks/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(int id, UpdateTaskDto dto)
    {
        var isUpdated = await _taskService.UpdateTaskAsync(id, dto, CurrentUserId);
        if (!isUpdated) return NotFound();

        // Broadcast the update back down the WebSocket channel
        var updatedTask = await _taskService.GetTaskByIdAsync(id, CurrentUserId);
        if (updatedTask != null)
        {
            await _hubContext.Clients.All.SendAsync("TaskUpdated", updatedTask);
        }

        return NoContent();
    }

    // DELETE: api/tasks/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var isDeleted = await _taskService.DeleteTaskAsync(id, CurrentUserId);
        if (!isDeleted) return NotFound();

        // Broadcast removal ID
        await _hubContext.Clients.All.SendAsync("TaskDeleted", id);

        return NoContent();
    }
}
