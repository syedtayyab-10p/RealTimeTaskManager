using Microsoft.AspNetCore.Identity;
using TaskManager.Api.DTOs;

namespace TaskManager.Api.Services;

public interface IAuthService
{
    // Register returns an IdentityResult indicating success or errors
    Task<IdentityResult> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
}
