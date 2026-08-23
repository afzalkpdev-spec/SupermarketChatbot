using Microsoft.EntityFrameworkCore;
using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class AdminAuthService
{
    private readonly AppDbContext _db;

    public AdminAuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> AnyAdminExistsAsync()
    {
        return await _db.AdminUsers.AnyAsync();
    }

    public async Task<AdminUser> CreateAdminUserAsync(string username, string password, string fullName, int? branchId)
    {
        var admin = new AdminUser
        {
            Username = username,
            PasswordHash = PasswordHasher.HashPassword(password),
            FullName = fullName,
            BranchId = branchId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AdminUsers.Add(admin);
        await _db.SaveChangesAsync();
        return admin;
    }

    public async Task<AdminUser?> ValidateCredentialsAsync(string username, string password)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(a => a.Username == username && a.IsActive);
        if (admin is null) return null;

        return PasswordHasher.VerifyPassword(password, admin.PasswordHash) ? admin : null;
    }
}