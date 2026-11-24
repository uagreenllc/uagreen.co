using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Admin;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Payments;

namespace UaGreen.Site.Tests;

public class AdminPaymentRulesTests
{
    private static Order Parts(string bank, OrderStatus status = OrderStatus.Paid) => new()
        { Id = Guid.NewGuid(), PaymentMethod = PaymentMethod.MonoParts, Status = status, PaymentRef = "x", PaymentStatus = bank };

    [Theory]
    [InlineData("IN_PROCESS/WAITING_FOR_STORE_CONFIRM", OrderStatus.Shipped, BankStep.Confirm)]
    [InlineData("IN_PROCESS/WAITING_FOR_CLIENT", OrderStatus.Shipped, BankStep.Block)]
    [InlineData("SUCCESS/ACTIVE", OrderStatus.Shipped, BankStep.None)]
    [InlineData("FAIL/EXCEEDED_SUM_LIMIT", OrderStatus.Shipped, BankStep.None)]
    [InlineData("IN_PROCESS/WAITING_FOR_CLIENT", OrderStatus.Cancelled, BankStep.Reject)]
    [InlineData("IN_PROCESS/WAITING_FOR_STORE_CONFIRM", OrderStatus.Cancelled, BankStep.Reject)]
    [InlineData("SUCCESS/ACTIVE", OrderStatus.Cancelled, BankStep.None)]
    [InlineData("IN_PROCESS/WAITING_FOR_STORE_CONFIRM", OrderStatus.Paid, BankStep.None)]
    public void ForStatus(string bank, OrderStatus to, BankStep expected) =>
        Assert.Equal(expected, AdminPaymentRules.ForStatus(Parts(bank), to).Step);

    [Fact]
    public void Block_explains_why() =>
        Assert.Equal("Клієнт ще не підтвердив покупку частинами",
            AdminPaymentRules.ForStatus(Parts("IN_PROCESS/WAITING_FOR_CLIENT"), OrderStatus.Shipped).Message);

    [Fact]
    public void Card_orders_never_touch_the_bank()
    {
        var o = new Order { PaymentMethod = PaymentMethod.Card, Status = OrderStatus.Paid, PaymentRef = "inv", PaymentStatus = "success" };
        Assert.Equal(BankStep.None, AdminPaymentRules.ForStatus(o, OrderStatus.Shipped).Step);
        Assert.Equal(BankStep.None, AdminPaymentRules.ForStatus(o, OrderStatus.Cancelled).Step);
    }

    [Fact]
    public void Parts_without_bank_ref_never_touch_the_bank()
    {
        var o = Parts("FAIL/CREATE_ERROR", OrderStatus.New);
        o.PaymentRef = null;
        Assert.Equal(BankStep.None, AdminPaymentRules.ForStatus(o, OrderStatus.Cancelled).Step);
    }

    [Fact]
    public void TotalLocked_only_while_the_bank_holds_the_amount()
    {
        Assert.True(AdminPaymentRules.TotalLocked(new Order { PaymentMethod = PaymentMethod.Card, Status = OrderStatus.AwaitingPayment, PaymentRef = "inv" }));
        Assert.False(AdminPaymentRules.TotalLocked(new Order { PaymentMethod = PaymentMethod.Card, Status = OrderStatus.AwaitingPayment }));
        Assert.True(AdminPaymentRules.TotalLocked(Parts("IN_PROCESS/WAITING_FOR_CLIENT", OrderStatus.AwaitingPayment)));
        Assert.False(AdminPaymentRules.TotalLocked(Parts("IN_PROCESS/WAITING_FOR_STORE_CONFIRM")));
        Assert.False(AdminPaymentRules.TotalLocked(new Order { PaymentMethod = PaymentMethod.Invoice, Status = OrderStatus.AwaitingPayment, PaymentRef = "x" }));
    }

    private sealed class Down : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }

    [Fact]
    public async Task Confirm_when_bank_down_throws_MonoPartsException()
    {
        var client = new MonoPartsClient(new HttpClient(new Down()) { BaseAddress = new Uri("https://u2-demo-ext.mono.st4g3.com") },
            Options.Create(new MonoPartsOptions { StoreId = "s", Secret = "x" }), Options.Create(new AppOptions { PublicBaseUrl = "https://uagreen.co" }));
        await Assert.ThrowsAsync<MonoPartsException>(() => client.ConfirmAsync("x", CancellationToken.None));
    }
}
