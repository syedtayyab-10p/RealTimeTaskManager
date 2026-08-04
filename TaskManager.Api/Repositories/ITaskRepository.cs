using TaskManager.Api.Models;

namespace TaskManager.Api.Repositories;

public interface ITaskRepository
{
    Task<IEnumerable<TodoTask>> GetAllByUserIdAsync(string userId);
    Task<TodoTask?> GetByIdAsync(int id, string userId);
    Task AddAsync(TodoTask task);
    Task UpdateAsync(TodoTask task);
    Task DeleteAsync(TodoTask task);
}
