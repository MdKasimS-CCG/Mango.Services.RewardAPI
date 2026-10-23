using System.Text.Json.Serialization;

namespace Mango.Services.RewardAPI.Messaging;

public sealed class OrderCreatedEvent
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; set; }

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("contractVersion")]
    public int ContractVersion { get; set; }

    [JsonPropertyName("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }

    [JsonPropertyName("orderId")]
    public int OrderId { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("customerEmail")]
    public string CustomerEmail { get; set; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public double TotalAmount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<OrderCreatedItem> Items { get; set; } = new();
}

public sealed class OrderCreatedItem
{
    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public double UnitPrice { get; set; }
}
