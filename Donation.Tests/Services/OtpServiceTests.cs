using Donation.Application.Services;
using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.OTPs;
using Donation.Core.Users;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Donation.Tests.Services;

public class OtpServiceTests
{
    private readonly Mock<IOtpRepository> _otpRepository = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _userRepository = new(MockBehavior.Strict);
    private readonly Mock<ISendGridClient> _sendGridClient = new(MockBehavior.Strict);

    private OtpService CreateService() => new(_otpRepository.Object, _userRepository.Object, _sendGridClient.Object);

    [Fact]
    public async Task GenerateAuthOtpAsync_ShouldThrow_WhenUserDoesNotExist()
    {
        var service = CreateService();

        _userRepository.Setup(r => r.ExistsEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.GenerateAuthOtpAsync("user@example.com"));
        Assert.Equal(GeneralError.UserNotFound, ex.ErrorCode);

        _userRepository.VerifyAll();
    }

    [Fact]
    public async Task GenerateAuthOtpAsync_ShouldReturnTrue_WhenEmailExists()
    {
        var service = CreateService();
        var storedOtps = new List<Otp>();

        _userRepository.Setup(r => r.ExistsEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sendGridClient.Setup(c => c.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _otpRepository.Setup(r => r.ClearHistoryByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _otpRepository.Setup(r => r.AddAsync(It.IsAny<Otp>(), It.IsAny<CancellationToken>()))
            .Callback<Otp, CancellationToken>((otp, _) => storedOtps.Add(otp))
            .Returns(Task.CompletedTask);
        _otpRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await service.GenerateAuthOtpAsync("user@example.com");

        Assert.True(result);
        Assert.Single(storedOtps);
        Assert.Equal("user@example.com", storedOtps[0].Email);
        Assert.False(string.IsNullOrEmpty(storedOtps[0].Code));

        _userRepository.VerifyAll();
        _sendGridClient.VerifyAll();
        _otpRepository.VerifyAll();
    }

    [Fact]
    public async Task SendOtpEmailAsync_ShouldSendEmailPersistOtpAndReturnHash()
    {
        var service = CreateService();
        Otp? savedOtp = null;
        string? sentSubject = null;
        string? sentBody = null;

        _sendGridClient.Setup(c => c.SendAsync("user@example.com", It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string?, CancellationToken>((_, subject, body, _, _) =>
            {
                sentSubject = subject;
                sentBody = body;
            })
            .Returns(Task.CompletedTask);
        _otpRepository.Setup(r => r.ClearHistoryByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _otpRepository.Setup(r => r.AddAsync(It.IsAny<Otp>(), It.IsAny<CancellationToken>()))
            .Callback<Otp, CancellationToken>((otp, _) => savedOtp = otp)
            .Returns(Task.CompletedTask);
        _otpRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var hash = await service.SendOtpEmailAsync("user@example.com");

        Assert.False(string.IsNullOrEmpty(hash));
        Assert.NotNull(savedOtp);
        Assert.Equal("user@example.com", savedOtp!.Email);
        Assert.Equal(hash, savedOtp.Code);
        Assert.False(string.IsNullOrEmpty(sentSubject));
        Assert.Contains("verification", sentSubject!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(sentBody);
        Assert.Contains("Your one time password is", sentBody!);

        _sendGridClient.VerifyAll();
        _otpRepository.VerifyAll();
    }
}
