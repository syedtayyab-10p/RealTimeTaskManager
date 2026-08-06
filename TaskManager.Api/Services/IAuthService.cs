using Microsoft.AspNetCore.Identity;
using TaskManager.Api.DTOs;

namespace TaskManager.Api.Services;

public interface IAuthService
{
    Task<IdentityResult> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
}
