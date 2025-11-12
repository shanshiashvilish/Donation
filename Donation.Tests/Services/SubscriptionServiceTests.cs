using Donation.Application.Services;
using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.Payments;
using Donation.Core.Subscriptions;
using Donation.Core.Users;
using Donation.Infrastructure;
using Donation.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Donation.Tests.Services;

public class SubscriptionServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static SubscriptionService CreateService(AppDbContext context, Mock<IFlittClient> flittMock)
    {
        var subscriptionRepository = new SubscriptionRepository(context);
        var userRepository = new UserRepository(context);
        var paymentRepository = new PaymentRepository(context);
        return new SubscriptionService(subscriptionRepository, userRepository, flittMock.Object, paymentRepository);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrow_WhenUserHasActiveSubscription()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        var user = new User("user@example.com", "John", "Doe");
        var subscription = new Subscription(user.Id, 1000, Currency.GEL, "ext-1");
        subscription.Activate();
        context.Users.Add(user);
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.SubscribeAsync(1500, "user@example.com", "John", "Doe"));
        Assert.Equal(GeneralError.UserAlreadyExists, ex.ErrorCode);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrow_WhenCheckoutUrlMissing()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        flittClient.Setup(c => c.SubscribeAsync(1500, "new@example.com", "Jane", "Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string.Empty, "order", "ext"));

        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.SubscribeAsync(1500, "new@example.com", "Jane", "Doe"));
        Assert.Equal(GeneralError.UnableToGenerateSubscriptionCheckoutUrl, ex.ErrorCode);

        flittClient.VerifyAll();
    }

    [Fact]
    public async Task SubscribeAsync_ShouldReturnCheckoutUrl_WhenCreated()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        flittClient.Setup(c => c.SubscribeAsync(2000, "new@example.com", "Jane", "Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(("https://checkout", "order", "ext"));

        var service = CreateService(context, flittClient);

        var checkout = await service.SubscribeAsync(2000, "new@example.com", "Jane", "Doe");

        Assert.Equal("https://checkout", checkout);
        flittClient.VerifyAll();
    }

    [Fact]
    public async Task EditSubscriptionAsync_ShouldThrow_WhenSubscriptionNotFound()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.EditSubscriptionAsync(Guid.NewGuid(), Guid.NewGuid(), 1000));
        Assert.Equal(GeneralError.SubscriptionNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task EditSubscriptionAsync_ShouldThrow_WhenUserNotFound()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        var orphanUserId = Guid.NewGuid();
        var subscription = new Subscription(orphanUserId, 1000, Currency.GEL, "ext-1");
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.EditSubscriptionAsync(orphanUserId, subscription.Id, 2000));
        Assert.Equal(GeneralError.UserNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task EditSubscriptionAsync_ShouldReturnCheckoutUrl_WhenUpdated()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        var user = new User("user@example.com", "John", "Doe");
        var subscription = new Subscription(user.Id, 1000, Currency.GEL, "ext-1");
        subscription.Activate();
        context.Users.Add(user);
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        flittClient.Setup(c => c.UnsubscribeAsync("ext-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        flittClient.Setup(c => c.SubscribeAsync(1500, "user@example.com", "John", "Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(("https://new-checkout", "order2", "ext2"));

        var service = CreateService(context, flittClient);

        var checkout = await service.EditSubscriptionAsync(user.Id, subscription.Id, 1500);

        Assert.Equal("https://new-checkout", checkout);
        var storedSubscription = await context.Subscriptions.FindAsync(subscription.Id);
        Assert.Equal(SubscriptionStatus.Canceled, storedSubscription!.Status);

        flittClient.VerifyAll();
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldThrow_WhenSubscriptionMissing()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.UnsubscribeAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(GeneralError.SubscriptionNotFound, ex.ErrorCode);
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldThrow_WhenUserMismatch()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        var user = new User("user@example.com", "John", "Doe");
        var otherUser = new User("other@example.com", "Jane", "Doe");
        var subscription = new Subscription(user.Id, 1000, Currency.GEL, "ext-1");
        context.Users.AddRange(user, otherUser);
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.UnsubscribeAsync(subscription.Id, otherUser.Id));
        Assert.Equal(GeneralError.CurrentUserNotSubscriptionCreator, ex.ErrorCode);
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldCancelSubscription_WhenFlittSucceeds()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        var user = new User("user@example.com", "John", "Doe");
        var subscription = new Subscription(user.Id, 1000, Currency.GEL, "ext-1");
        context.Users.Add(user);
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        flittClient.Setup(c => c.UnsubscribeAsync("ext-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(context, flittClient);

        var result = await service.UnsubscribeAsync(subscription.Id, user.Id);

        Assert.True(result);
        var stored = await context.Subscriptions.FindAsync(subscription.Id);
        Assert.Equal(SubscriptionStatus.Canceled, stored!.Status);

        flittClient.VerifyAll();
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldReturnFalse_WhenFlittFails()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        var user = new User("user@example.com", "John", "Doe");
        var subscription = new Subscription(user.Id, 1000, Currency.GEL, "ext-1");
        context.Users.Add(user);
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        flittClient.Setup(c => c.UnsubscribeAsync("ext-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService(context, flittClient);

        var result = await service.UnsubscribeAsync(subscription.Id, user.Id);

        Assert.False(result);
        var stored = await context.Subscriptions.FindAsync(subscription.Id);
        Assert.NotEqual(SubscriptionStatus.Canceled, stored!.Status);

        flittClient.VerifyAll();
    }

    [Fact]
    public async Task HandleFlittCallbackAsync_ShouldThrow_WhenSignatureInvalid()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        flittClient.Setup(c => c.VerifySignature(It.IsAny<IDictionary<string, string?>>()))
            .Returns(false);

        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.HandleFlittCallbackAsync(new Dictionary<string, string>()));
        Assert.Equal(GeneralError.FlittSignatureInvalid, ex.ErrorCode);

        flittClient.VerifyAll();
    }

    [Fact]
    public async Task HandleFlittCallbackAsync_ShouldThrow_WhenOrderIdMissing()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        flittClient.Setup(c => c.VerifySignature(It.IsAny<IDictionary<string, string?>>()))
            .Returns(true);

        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.HandleFlittCallbackAsync(new Dictionary<string, string>
        {
            ["payment_id"] = "pid"
        }));
        Assert.Equal(GeneralError.MissingParameter, ex.ErrorCode);

        flittClient.VerifyAll();
    }

    [Fact]
    public async Task HandleFlittCallbackAsync_ShouldMarkPastDue_WhenPaymentFails()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        flittClient.Setup(c => c.VerifySignature(It.IsAny<IDictionary<string, string?>>()))
            .Returns(true);

        var user = new User("user@example.com", "John", "Doe");
        var subscription = new Subscription(user.Id, 1000, Currency.GEL, "order-1");
        subscription.Activate();
        context.Users.Add(user);
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        var service = CreateService(context, flittClient);

        await service.HandleFlittCallbackAsync(new Dictionary<string, string>
        {
            ["order_id"] = "order-1",
            ["order_status"] = "failed",
            ["response_status"] = "declined",
            ["amount"] = "1000"
        });

        var stored = await context.Subscriptions.FindAsync(subscription.Id);
        Assert.Equal(SubscriptionStatus.PastDue, stored!.Status);

        flittClient.VerifyAll();
    }

    [Fact]
    public async Task HandleFlittCallbackAsync_ShouldCreateUserSubscriptionAndPayment_WhenSuccessful()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        flittClient.Setup(c => c.VerifySignature(It.IsAny<IDictionary<string, string?>>()))
            .Returns(true);

        var service = CreateService(context, flittClient);

        var merchantData = new
        {
            Email = "new@example.com",
            Name = "Jane",
            LastName = "Doe"
        };
        var merchantJson = JsonSerializer.Serialize(merchantData);
        var merchantB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(merchantJson));

        await service.HandleFlittCallbackAsync(new Dictionary<string, string>
        {
            ["order_id"] = "order-1",
            ["order_status"] = "approved",
            ["response_status"] = "success",
            ["amount"] = "2500",
            ["merchant_data"] = merchantB64
        });

        var createdUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "new@example.com");
        Assert.NotNull(createdUser);
        var createdSubscription = await context.Subscriptions.FirstOrDefaultAsync(s => s.ExternalId == "order-1");
        Assert.NotNull(createdSubscription);
        Assert.Equal(SubscriptionStatus.Active, createdSubscription!.Status);
        Assert.NotNull(createdSubscription.NextBillingAt);
        var payment = await context.Payments.FirstOrDefaultAsync(p => p.Email == "new@example.com");
        Assert.NotNull(payment);
        Assert.Equal(2500, payment!.Amount);
        Assert.Equal(PaymentType.Subscription, payment.Type);

        flittClient.VerifyAll();
    }

    [Fact]
    public async Task HandleFlittCallbackAsync_ShouldThrow_WhenMerchantDataInvalid()
    {
        using var context = CreateContext();
        var flittClient = new Mock<IFlittClient>(MockBehavior.Strict);
        flittClient.Setup(c => c.VerifySignature(It.IsAny<IDictionary<string, string?>>()))
            .Returns(true);

        var service = CreateService(context, flittClient);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.HandleFlittCallbackAsync(new Dictionary<string, string>
        {
            ["order_id"] = "order-1",
            ["order_status"] = "approved",
            ["response_status"] = "success",
            ["amount"] = "2500",
            ["merchant_data"] = "not-base64"
        }));
        Assert.Equal(GeneralError.MissingParameter, ex.ErrorCode);

        flittClient.VerifyAll();
    }
}
