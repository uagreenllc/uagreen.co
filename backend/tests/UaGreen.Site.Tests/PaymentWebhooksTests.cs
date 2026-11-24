using UaGreen.Site.Api.Features.Orders;

namespace UaGreen.Site.Tests;

public class PaymentWebhooksTests
{
    [Theory]
    [InlineData("{\"invoiceId\":\"p2_9ZgpZVsl3\",\"status\":\"success\"}", "invoiceId", "p2_9ZgpZVsl3")]
    [InlineData("{\"order_id\":\"a0eebc99-9c0b-4ef8-bb6d-000000000004\"}", "order_id", "a0eebc99-9c0b-4ef8-bb6d-000000000004")]
    [InlineData("not json", "invoiceId", null)]
    [InlineData("{\"invoiceId\":42}", "invoiceId", null)]
    [InlineData("[1,2]", "invoiceId", null)]
    [InlineData("{\"invoiceId\":\"\"}", "invoiceId", null)]
    [InlineData("", "order_id", null)]
    public void ExtractId(string body, string field, string? expected) => Assert.Equal(expected, PaymentWebhooks.ExtractId(body, field));
}
