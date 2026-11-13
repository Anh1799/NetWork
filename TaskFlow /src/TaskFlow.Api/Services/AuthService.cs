using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Models;
using BCryptNet = BCrypt.Net.BCrypt;

namespace TaskFlow.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService(AppDbContext db, ITokenService tokens) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(
            u => u.Username.ToLower() == username.ToLower() || u.Email == email, ct);

        if (exists)
            throw new AppException(StatusCodes.Status409Conflict, "Username or email already exists.");

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCryptNet.HashPassword(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == username.ToLower(), ct);

        // Thông báo chung để không lộ việc username có tồn tại hay không
        if (user is null || !BCryptNet.Verify(request.Password, user.PasswordHash))
            throw new AppException(StatusCodes.Status401Unauthorized, "Invalid username or password.");

        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, user.Username);
    }
}
