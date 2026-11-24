using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Orders;
using UaGreen.Site.Api.Infrastructure.Payments;

namespace UaGreen.Site.Tests;

public class PaymentSyncTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    private static Order Card(OrderStatus status = OrderStatus.AwaitingPayment) => new()
        { Id = Guid.NewGuid(), Number = 5, PaymentMethod = PaymentMethod.Card, Status = status, Total = 14999.99m, PaymentRef = "inv_1" };

    private static Order Parts(OrderStatus status = OrderStatus.AwaitingPayment, string? bank = "IN_PROCESS/WAITING_FOR_CLIENT") => new()
        { Id = Guid.NewGuid(), Number = 6, PaymentMethod = PaymentMethod.MonoParts, Status = status, Total = 15000m, PaymentRef = "p_1", PaymentStatus = bank };

    private static MonoInvoiceStatus Inv(string status, long amount = 1499999, int ccy = 980) => new("inv_1", status, amount, ccy);
    private static MonoPartsState St(string state, string sub) => new("p_1", state, sub);

    [Fact]
    public void Card_success_exact_amount_marks_paid()
    {
        var o = Card();
        Assert.Equal(PayNotice.Paid, PaymentSync.ApplyCard(o, Inv("success"), Now));
        Assert.Equal(OrderStatus.Paid, o.Status);
        Assert.Equal(Now, o.PaidAt);
        Assert.Equal("success", o.PaymentStatus);
        Assert.Equal(Now, o.PaymentCheckedAt);
    }

    [Fact]
    public void Card_success_twice_notifies_once()
    {
        var o = Card();
        PaymentSync.ApplyCard(o, Inv("success"), Now);
        Assert.Equal(PayNotice.None, PaymentSync.ApplyCard(o, Inv("success"), Now.AddMinutes(1)));
        Assert.Equal(Now, o.PaidAt);
    }

    [Fact]
    public void Card_success_wrong_amount_is_not_paid()
    {
        var o = Card();
        Assert.Equal(PayNotice.AmountMismatch, PaymentSync.ApplyCard(o, Inv("success", 1000), Now));
        Assert.Equal(OrderStatus.AwaitingPayment, o.Status);
        Assert.Null(o.PaidAt);
    }

    [Fact]
    public void Card_success_wrong_currency_is_not_paid()
    {
        var o = Card();
        Assert.Equal(PayNotice.AmountMismatch, PaymentSync.ApplyCard(o, Inv("success", 1499999, 840), Now));
        Assert.Equal(OrderStatus.AwaitingPayment, o.Status);
    }

    [Fact]
    public void Card_failure_only_records_status()
    {
        var o = Card();
        Assert.Equal(PayNotice.None, PaymentSync.ApplyCard(o, Inv("failure"), Now));
        Assert.Equal(OrderStatus.AwaitingPayment, o.Status);
        Assert.Equal("failure", o.PaymentStatus);
    }

    // Money arrived on an order the manager closed: never reopen it, but tell a human — once.
    [Fact]
    public void Card_success_on_cancelled_order_tells_managers_once()
    {
        var o = Card(OrderStatus.Cancelled);
        Assert.Equal(PayNotice.PaidInactive, PaymentSync.ApplyCard(o, Inv("success"), Now));
        Assert.Equal(OrderStatus.Cancelled, o.Status);
        Assert.Equal(PayNotice.None, PaymentSync.ApplyCard(o, Inv("success"), Now.AddMinutes(1)));
    }

    [Fact]
    public void Card_success_on_shipped_order_is_quiet()
    {
        var o = Card(OrderStatus.Shipped);
        o.PaymentStatus = "success";
        Assert.Equal(PayNotice.None, PaymentSync.ApplyCard(o, Inv("success"), Now));
    }

    // An older invoice of the same order (second tab, retry after a failure) got paid.
    [Fact]
    public void AppliesTo_foreign_invoice_only_when_paid_for_this_order()
    {
        var o = Card();
        Assert.True(PaymentSync.AppliesTo(o, "inv_1", Inv("failure")));
        Assert.True(PaymentSync.AppliesTo(o, "inv_old", new MonoInvoiceStatus("inv_old", "success", 1499999, 980, o.Id.ToString())));
        Assert.False(PaymentSync.AppliesTo(o, "inv_old", new MonoInvoiceStatus("inv_old", "failure", 1499999, 980, o.Id.ToString())));
        Assert.False(PaymentSync.AppliesTo(o, "inv_old", new MonoInvoiceStatus("inv_old", "success", 1499999, 980, Guid.NewGuid().ToString())));
        Assert.False(PaymentSync.AppliesTo(o, "inv_old", new MonoInvoiceStatus("inv_old", "success", 1499999, 980)));
    }

    // Our own reject (admin cancels a waiting application) comes back as a FAIL callback: not a bank refusal.
    [Fact]
    public void Parts_rejected_by_store_while_waiting_is_not_a_refusal()
    {
        var o = Parts();
        Assert.Equal(PayNotice.None, PaymentSync.ApplyParts(o, St("FAIL", "REJECTED_BY_STORE"), Now));
        Assert.Equal(OrderStatus.AwaitingPayment, o.Status);
    }

    [Fact]
    public void Parts_waiting_for_client_changes_nothing()
    {
        var o = Parts();
        Assert.Equal(PayNotice.None, PaymentSync.ApplyParts(o, St("IN_PROCESS", "WAITING_FOR_CLIENT"), Now));
        Assert.Equal(OrderStatus.AwaitingPayment, o.Status);
    }

    [Fact]
    public void Parts_waiting_for_store_confirm_marks_paid()
    {
        var o = Parts();
        Assert.Equal(PayNotice.PartsApproved, PaymentSync.ApplyParts(o, St("IN_PROCESS", "WAITING_FOR_STORE_CONFIRM"), Now));
        Assert.Equal(OrderStatus.Paid, o.Status);
        Assert.Equal(Now, o.PaidAt);
        Assert.Equal("IN_PROCESS/WAITING_FOR_STORE_CONFIRM", o.PaymentStatus);
    }

    [Fact]
    public void Parts_straight_to_success_marks_paid()
    {
        var o = Parts();
        Assert.Equal(PayNotice.PartsApproved, PaymentSync.ApplyParts(o, St("SUCCESS", "ACTIVE"), Now));
        Assert.Equal(OrderStatus.Paid, o.Status);
    }

    [Fact]
    public void Parts_fail_while_waiting_goes_back_to_new()
    {
        var o = Parts();
        Assert.Equal(PayNotice.PartsFailed, PaymentSync.ApplyParts(o, St("FAIL", "EXCEEDED_SUM_LIMIT"), Now));
        Assert.Equal(OrderStatus.New, o.Status);
        Assert.Equal("FAIL/EXCEEDED_SUM_LIMIT", o.PaymentStatus);
    }

    [Fact]
    public void Parts_rejected_by_store_leaves_cancelled_order()
    {
        var o = Parts(OrderStatus.Cancelled);
        Assert.Equal(PayNotice.None, PaymentSync.ApplyParts(o, St("FAIL", "REJECTED_BY_STORE"), Now));
        Assert.Equal(OrderStatus.Cancelled, o.Status);
    }

    [Fact]
    public void Parts_fail_after_approval_cancels_the_payment()
    {
        var o = Parts(OrderStatus.Paid, "IN_PROCESS/WAITING_FOR_STORE_CONFIRM");
        o.PaidAt = Now.AddDays(-1);
        Assert.Equal(PayNotice.PartsCancelled, PaymentSync.ApplyParts(o, St("FAIL", "CLIENT_PUSH_TIMEOUT"), Now));
        Assert.Equal(OrderStatus.New, o.Status);
        Assert.Null(o.PaidAt);
    }

    [Fact]
    public void Parts_done_after_shipping_only_records_status()
    {
        var o = Parts(OrderStatus.Shipped, "SUCCESS/ACTIVE");
        Assert.Equal(PayNotice.None, PaymentSync.ApplyParts(o, St("SUCCESS", "DONE"), Now));
        Assert.Equal(OrderStatus.Shipped, o.Status);
        Assert.Equal("SUCCESS/DONE", o.PaymentStatus);
    }

    [Fact]
    public void Waiting_covers_unpaid_and_approved_unconfirmed_parts()
    {
        Assert.True(PaymentSync.Waiting(Card()));
        var noRef = Card();
        noRef.PaymentRef = null;
        Assert.False(PaymentSync.Waiting(noRef));
        Assert.True(PaymentSync.Waiting(Parts(OrderStatus.Paid, "IN_PROCESS/WAITING_FOR_STORE_CONFIRM")));
        Assert.False(PaymentSync.Waiting(Parts(OrderStatus.Shipped, "SUCCESS/ACTIVE")));
        Assert.False(PaymentSync.Waiting(Card(OrderStatus.Paid)));
    }
}
