using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.Users;

namespace Donation.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        var result = await _userRepository.GetByIdAsync(id, true);

        return result ?? throw new AppException(GeneralError.UserNotFound);
    }

    public async Task<User?> UpdateAsync(Guid id, string name, string lastname)
    {
        var user = await _userRepository.GetByIdAsync(id) ?? throw new AppException(GeneralError.UserNotFound);
        user.Update(name, lastname);
        await _userRepository.SaveChangesAsync();

        return user;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id, includeProperties: true) ?? throw new AppException(GeneralError.UserNotFound);

        _userRepository.Remove(user);
        await _userRepository.SaveChangesAsync();

        return true;
    }
}
