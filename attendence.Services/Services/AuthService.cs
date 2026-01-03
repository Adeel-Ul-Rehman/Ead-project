using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace attendence.Services.Services;

public interface IAuthService
{
    Task<User?> AuthenticateAsync(string email, string password);
    Task<User?> GetUserByIdAsync(int userId);
    Task<User?> GetUserByEmailAsync(string email);
}

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(ApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return null;

        var user = await _context.Users
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
            return null;

        // 1) If stored password is PBKDF2, verify directly
        if (_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return user;
        }

        // 2) Legacy: stored value might be plain-text password -> accept and migrate
        if (user.PasswordHash == password)
        {
            user.PasswordHash = _passwordHasher.HashPassword(password);
            await _context.SaveChangesAsync();
            return user;
        }

        // 3) Legacy: stored value might be SHA256 base64 (previous implementation)
        //    We compute SHA256(base) and compare; if matches, migrate to PBKDF2.
        try
        {
            var sha256Base64 = PasswordHasher.ComputeSha256Base64(password);
            if (sha256Base64 == user.PasswordHash)
            {
                user.PasswordHash = _passwordHasher.HashPassword(password);
                await _context.SaveChangesAsync();
                return user;
            }
        }
        catch
        {
            // ignore and fall through
        }

        // not authenticated
        return null;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.Student)
            .Include(u => u.Teacher)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }
}