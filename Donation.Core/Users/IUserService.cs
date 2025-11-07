namespace Donation.Core.Users;

public interface IUserService
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> UpdateAsync(Guid id, string name, string lastname);
    Task<bool> DeleteAsync(Guid id);
}
