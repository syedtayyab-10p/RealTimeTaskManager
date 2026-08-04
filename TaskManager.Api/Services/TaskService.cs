using TaskManager.Api.DTOs;
using TaskManager.Api.Models;
using TaskManager.Api.Repositories;

namespace TaskManager.Api.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repository;
    private readonly ICacheService _cache;

    public TaskService(ITaskRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    private static string GetTasksCacheKey(string userId) => $"tasks_{userId}";

    public async Task<IEnumerable<TaskResponseDto>> GetTasksAsync(string userId)
    {
        string cacheKey = GetTasksCacheKey(userId);
        
        var cachedTasks = await _cache.GetAsync<List<TaskResponseDto>>(cacheKey);
        if (cachedTasks != null) return cachedTasks;

        var dbTasks = await _repository.GetAllByUserIdAsync(userId);
        
        var taskResponses = dbTasks
            .Select(t => MapToResponseDto(t))
            .ToList();

        await _cache.SetAsync(cacheKey, taskResponses);

        return taskResponses;
    }

    public async Task<TaskResponseDto?> GetTaskByIdAsync(int id, string userId)
    {
        var task = await _repository.GetByIdAsync(id, userId);
        return task == null ? null : MapToResponseDto(task);
    }

    public async Task<TaskResponseDto> CreateTaskAsync(CreateTaskDto dto, string userId)
    {
        var task = new TodoTask
        {
            Title = dto.Title,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        await _repository.AddAsync(task);
        await _cache.RemoveAsync(GetTasksCacheKey(userId));

        return MapToResponseDto(task);
    }

    public async Task<bool> UpdateTaskAsync(int id, UpdateTaskDto dto, string userId)
    {
        var task = await _repository.GetByIdAsync(id, userId);
        if (task == null) return false;

        task.Title = dto.Title;
        task.Description = dto.Description;
        task.IsCompleted = dto.IsCompleted;

        await _repository.UpdateAsync(task);
        await _cache.RemoveAsync(GetTasksCacheKey(userId));

        return true;
    }

    public async Task<bool> DeleteTaskAsync(int id, string userId)
    {
        var task = await _repository.GetByIdAsync(id, userId);
        if (task == null) return false;

        await _repository.DeleteAsync(task);
        await _cache.RemoveAsync(GetTasksCacheKey(userId));

        return true;
    }

    // DRY Principle: Centralized, reusable object mapping expression
    private static TaskResponseDto MapToResponseDto(TodoTask task) =>
        new(task.Id, task.Title, task.Description, task.IsCompleted, task.CreatedAt);
}
