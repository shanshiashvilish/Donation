using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.Users;
using Microsoft.Extensions.Logging;

namespace Donation.Application.Services;

public sealed class UserService(IUserRepository userRepository, ILogger<UserService> logger) : IUserService
{
    public async Task<User?> GetByIdAsync(Guid id)
    {
        logger.LogDebug("Fetching user by ID: {UserId}", id);

        var user = await userRepository.GetByIdAsync(id, includeProperties: true);
        if (user is null)
        {
            logger.LogWarning("User not found: {UserId}", id);
            throw new AppException(GeneralError.UserNotFound);
        }

        logger.LogInformation("Fetched user {UserId} ({Email})", user.Id, user.Email);
        return user;
    }

    public async Task<User?> UpdateAsync(Guid id, string name, string lastname)
    {
        logger.LogDebug("Updating user {UserId} with new values: {Name} {Lastname}", id, name, lastname);

        var user = await userRepository.GetByIdAsync(id)
            ?? throw new AppException(GeneralError.UserNotFound);

        user.Update(name, lastname);

        await userRepository.SaveChangesAsync();

        logger.LogInformation("User {UserId} updated successfully", id);
        return user;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        logger.LogDebug("Attempting to delete user {UserId}", id);

        var user = await userRepository.GetByIdAsync(id, includeProperties: true)
            ?? throw new AppException(GeneralError.UserNotFound);

        userRepository.Remove(user);
        await userRepository.SaveChangesAsync();

        logger.LogInformation("User {UserId} deleted successfully", id);
        return true;
    }
}
