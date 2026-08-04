using System.ComponentModel.DataAnnotations;

namespace TaskManager.Api.DTOs;

public record RegisterDto(
    [Required] string Username, 
    [Required] [EmailAddress] string Email, 
    [Required] string Password
);

public record LoginDto(
    [Required] string Username, 
    [Required] string Password
);

public record AuthResponseDto(
    string Token, 
    DateTime Expiration
);
