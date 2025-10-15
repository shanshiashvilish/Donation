using Donation.Core.OTPs;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Donation.Infrastructure.Repositories;

public class OtpRepository(AppDbContext db) : BaseRepository<Otp>(db), IOtpRepository
{
    public async Task<Otp?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var otp = await _set.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        return otp;
    }

    public async Task<bool> VerifyAsync(string email, string code, CancellationToken ct = default)
    {
        if (code.Length != 4)
        {
            // otp lenght must be exactly 4 digits
            return false;
        }

        var otp = await GetByEmailAsync(email, ct);

        if (otp == null)
        {
            // otp not found
            return false;
        }

        if (otp.Email != email)
        {
            // No such code was found matching the provided email
            return false;
        }
        var isotpValid = CompareHashes(otp.Code, Hash(code));

        if (!isotpValid)
        {
            // Invalid otp
            return false;
        }

        await ClearHistoryByEmailAsync(email, ct);
        await SaveChangesAsync(ct);

        return true;
    }


    public async Task ClearHistoryByEmailAsync(string email, CancellationToken ct = default)
    {
        var otps = await _set.Where(o => o.Email == email).ToListAsync(ct);

        if (otps != null || otps.Count != 0)
        {
            _set.RemoveRange(otps);
        }
    }


    #region Private Methods

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private static bool CompareHashes(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    #endregion

}
