using Donation.Application.Services;
using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.Users;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Donation.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new(MockBehavior.Strict);
    private readonly Mock<ILogger<UserService>> _logger = new(MockBehavior.Loose);

    private UserService CreateService() => new(_userRepository.Object, _logger.Object);

    [Fact]
    public async Task GetByIdAsync_ShouldReturnUser_WhenFound()
    {
        var service = CreateService();
        var user = new User("user@example.com", "John", "Doe");
        var userId = user.Id;

        _userRepository.Setup(r => r.GetByIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await service.GetByIdAsync(userId);

        Assert.Equal(user, result);
        _userRepository.VerifyAll();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrow_WhenUserMissing()
    {
        var service = CreateService();
        var id = Guid.NewGuid();

        _userRepository.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.GetByIdAsync(id));
        Assert.Equal(GeneralError.UserNotFound, ex.ErrorCode);

        _userRepository.VerifyAll();
    }

    [Fact]
    public async Task UpdateAsync_ShouldMutateUserAndPersist()
    {
        var service = CreateService();
        var user = new User("user@example.com", "John", "Doe");
        var id = user.Id;

        _userRepository.Setup(r => r.GetByIdAsync(id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var updated = await service.UpdateAsync(id, "  New  ", "Name  ");

        Assert.Equal("New", updated!.Name);
        Assert.Equal("Name", updated.Lastname);

        _userRepository.VerifyAll();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveUser()
    {
        var service = CreateService();
        var user = new User("user@example.com", "John", "Doe");
        var id = user.Id;

        _userRepository.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _userRepository.Setup(r => r.Remove(user));

        var result = await service.DeleteAsync(id);

        Assert.True(result);

        _userRepository.Verify(r => r.Remove(user), Times.Once);
        _userRepository.VerifyAll();
    }
}
