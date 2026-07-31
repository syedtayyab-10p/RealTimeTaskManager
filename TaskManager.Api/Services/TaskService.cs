using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using TaskManager.Api.Data;
using TaskManager.Api.DTOs;
using TaskManager.Api.Models;

namespace TaskManager.Api.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext _context;
    private readonly IDistributedCache _cache;

    public TaskService(AppDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    private static string GetCacheKey(string userId) => $"tasks_{userId}";

    public async Task<IEnumerable<TaskResponseDto>> GetTasksAsync(string userId)
    {
        string cacheKey = GetCacheKey(userId);
        string? cachedJson = null;

        try
        {
            cachedJson = await _cache.GetStringAsync(cacheKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache Warning] Redis Get failed: {ex.Message}");
        }

        if (!string.IsNullOrEmpty(cachedJson))
        {
            return JsonSerializer.Deserialize<List<TaskResponseDto>>(cachedJson) ?? new List<TaskResponseDto>();
        }

        // Cache Miss / Redis offline: Query DB
        var tasks = await _context.Tasks
            .Where(t => t.UserId == userId)
            .Select(t => new TaskResponseDto(t.Id, t.Title, t.Description, t.IsCompleted, t.CreatedAt))
            .ToListAsync();

        try
        {
            string serializedData = JsonSerializer.Serialize(tasks);
            var cacheOptions = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                .SetSlidingExpiration(TimeSpan.FromMinutes(10));

            await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache Warning] Redis Set failed: {ex.Message}");
        }

        return tasks;
    }

    public async Task<TaskResponseDto?> GetTaskByIdAsync(int id, string userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task == null) return null;

        return new TaskResponseDto(task.Id, task.Title, task.Description, task.IsCompleted, task.CreatedAt);
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

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        await EvictCacheAsync(userId);

        return new TaskResponseDto(task.Id, task.Title, task.Description, task.IsCompleted, task.CreatedAt);
    }

    public async Task<bool> UpdateTaskAsync(int id, UpdateTaskDto dto, string userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task == null) return false;

        task.Title = dto.Title;
        task.Description = dto.Description;
        task.IsCompleted = dto.IsCompleted;

        await _context.SaveChangesAsync();
        await EvictCacheAsync(userId);

        return true;
    }

    public async Task<bool> DeleteTaskAsync(int id, string userId)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task == null) return false;

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();
        await EvictCacheAsync(userId);

        return true;
    }

    private async Task EvictCacheAsync(string userId)
    {
        try
        {
            await _cache.RemoveAsync(GetCacheKey(userId));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache Warning] Redis Evict failed: {ex.Message}");
        }
    }
}
