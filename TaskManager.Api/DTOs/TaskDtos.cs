using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.DTOs;

public record CreateTaskDto(
    [Required] [MaxLength(100)] string Title,
    [MaxLength(500)] string Description
);

public record UpdateTaskDto(
    [Required] [MaxLength(100)] string Title,
    [MaxLength(500)] string Description,
    bool IsCompleted
);

public record TaskResponseDto(
    int Id,
    string Title,
    string Description,
    bool IsCompleted,
    DateTime CreatedAt
);
