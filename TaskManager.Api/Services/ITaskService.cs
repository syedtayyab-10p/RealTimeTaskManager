using TaskManager.Api.DTOs;

namespace TaskManager.Api.Services;

public interface ITaskService
{
    Task<IEnumerable<TaskResponseDto>> GetTasksAsync(string userId);
    Task<TaskResponseDto?> GetTaskByIdAsync(int id, string userId);
    Task<TaskResponseDto> CreateTaskAsync(CreateTaskDto dto, string userId);
    Task<bool> UpdateTaskAsync(int id, UpdateTaskDto dto, string userId);
    Task<bool> DeleteTaskAsync(int id, string userId);
}
