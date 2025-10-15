namespace Donation.Core.Users;

public interface IUserService
{
    Task<User> ValidateCreateAsync(User user);
    Task<User?> CreateAsync(User user);
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> UpdateAsync(Guid id, string name, string lastname);
    Task<bool> DeleteAsync(Guid id);
}
