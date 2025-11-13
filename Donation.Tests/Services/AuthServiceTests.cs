using Castle.Core.Logging;
using Donation.Application.Services;
using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.OTPs;
using Donation.Core.Users;
using Microsoft.Extensions.Logging;
using Moq;
using OpenIddict.Abstractions;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Donation.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new(MockBehavior.Strict);
    private readonly Mock<IOtpRepository> _otpRepository = new(MockBehavior.Strict);
    private readonly Mock<ILogger<AuthService>> _logger = new(MockBehavior.Loose);

    private AuthService CreateService() => new(_userRepository.Object, _otpRepository.Object, _logger.Object);

    [Fact]
    public async Task LoginAsync_ShouldThrow_WhenEmailMissing()
    {
        var service = CreateService();
        var request = new OpenIddictRequest();
        request.SetParameter("email", " ");
        request.SetParameter("otp", "1234");

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request));
        Assert.Equal(GeneralError.EmailOrOtpNull, ex.ErrorCode);
    }

    [Fact]
    public async Task LoginAsync_ShouldThrow_WhenOtpMissing()
    {
        var service = CreateService();
        var request = new OpenIddictRequest();
        request.SetParameter("email", "test@example.com");
        request.SetParameter("otp", " ");

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request));
        Assert.Equal(GeneralError.EmailOrOtpNull, ex.ErrorCode);
    }

    [Fact]
    public async Task LoginAsync_ShouldThrow_WhenOtpInvalid()
    {
        var service = CreateService();
        var request = new OpenIddictRequest();
        request.SetParameter("email", "user@example.com");
        request.SetParameter("otp", "1234");

        _otpRepository.Setup(r => r.VerifyAsync("user@example.com", "1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request));
        Assert.Equal(GeneralError.OtpInvalid, ex.ErrorCode);

        _otpRepository.VerifyAll();
    }

    [Fact]
    public async Task LoginAsync_ShouldThrow_WhenUserNotFound()
    {
        var service = CreateService();
        var request = new OpenIddictRequest();
        request.SetParameter("email", "user@example.com");
        request.SetParameter("otp", "1234");

        _otpRepository.Setup(r => r.VerifyAsync("user@example.com", "1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepository.Setup(r => r.GetByEmailAsync("user@example.com", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User)null!);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request));
        Assert.Equal(GeneralError.UserNotFound, ex.ErrorCode);

        _otpRepository.VerifyAll();
        _userRepository.VerifyAll();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnPrincipal_WhenCredentialsValid()
    {
        var service = CreateService();
        var request = new OpenIddictRequest();
        request.SetParameter("email", "user@example.com");
        request.SetParameter("otp", "1234");

        var user = new User("user@example.com", "John", "Doe")
        {
            Role = "donor"
        };
        var userId = user.Id;

        _otpRepository.Setup(r => r.VerifyAsync("user@example.com", "1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepository.Setup(r => r.GetByEmailAsync("user@example.com", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var principal = await service.LoginAsync(request);

        Assert.NotNull(principal);
        Assert.Equal(userId.ToString(), principal!.FindFirstValue(Claims.Subject));
        Assert.Equal(user.Email, principal.FindFirstValue(Claims.Email));
        Assert.Equal(user.Role, principal.FindFirstValue(Claims.Role));

        _otpRepository.VerifyAll();
        _userRepository.VerifyAll();
    }

    [Fact]
    public async Task LoginAsync_ShouldSucceed_WhenMasterOtpUsed()
    {
        var service = CreateService();
        var request = new OpenIddictRequest();
        request.SetParameter("email", "user@example.com");
        request.SetParameter("otp", "2468");

        var user = new User("user@example.com", "John", "Doe")
        {
            Role = "donor"
        };

        _otpRepository.Setup(r => r.VerifyAsync("user@example.com", "2468", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepository.Setup(r => r.GetByEmailAsync("user@example.com", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var principal = await service.LoginAsync(request);

        Assert.NotNull(principal);
        Assert.Equal(user.Email, principal!.FindFirstValue(Claims.Email));

        _otpRepository.VerifyAll();
        _userRepository.VerifyAll();
    }
}
