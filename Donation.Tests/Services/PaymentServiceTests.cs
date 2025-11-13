using Donation.Application.Services;
using Donation.Core.Enums;
using Donation.Core.Payments;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Donation.Tests.Services;

public class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> _paymentRepository = new(MockBehavior.Strict);
    private readonly Mock<ILogger<PaymentService>> _logger = new(MockBehavior.Loose);

    [Fact]
    public async Task CreateAsync_ShouldPersistPaymentAndReturnTrue()
    {
        var service = new PaymentService(_paymentRepository.Object, _logger.Object);
        Payment? captured = null;
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        _paymentRepository.Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => captured = payment)
            .Returns(Task.CompletedTask);
        _paymentRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await service.CreateAsync(1234, "payer@example.com", PaymentType.Subscription, Currency.USD, userId, subscriptionId);

        Assert.True(result);
        Assert.NotNull(captured);
        Assert.Equal(1234, captured!.Amount);
        Assert.Equal("payer@example.com", captured.Email);
        Assert.Equal(PaymentType.Subscription, captured.Type);
        Assert.Equal(Currency.USD, captured.Currency);
        Assert.Equal(userId, captured.UserId);
        Assert.Equal(subscriptionId, captured.SubscriptionId);

        _paymentRepository.VerifyAll();
    }
}
