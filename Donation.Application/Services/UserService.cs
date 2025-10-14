using Donation.Core.Users;

namespace Donation.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User> ValidateCreateAsync(User user)
    {
        var exists = await _userRepository.GetByEmailAsync(user.Email);

        if (exists != null)
        {
            return exists;
        }

        // flitt payment request

        return default;
    }

    public async Task<User> CreateAsync(User user)
    {
        var exists = await _userRepository.GetByEmailAsync(user.Email);

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return await _userRepository.GetByEmailAsync(user.Email);
    }

    public async Task<User> GetByIdAsync(Guid id)
    {
        var result = await _userRepository.GetByIdAsync(id);

        return result;
    }

    public async Task<User> UpdateAsync(Guid id, string name, string lastname)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            return default;
        }

        user.Update(name, lastname);
        await _userRepository.SaveChangesAsync();

        return user;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
        {
            return false;
        }

        _userRepository.Remove(user);
        await _userRepository.SaveChangesAsync();
        return true;
    }
}
