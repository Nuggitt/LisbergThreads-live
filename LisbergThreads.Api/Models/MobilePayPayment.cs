using LisbergThreads.Contracts.Orders;

namespace LisbergThreads.Api.Models;

public sealed class MobilePayPayment
{
    public Guid Id { get; set; }
    public Guid AccessToken { get; set; }
    public Guid? UserId { get; set; }
    public Guid? CartId { get; set; }
    public Guid? OrderId { get; set; }
    public decimal Total { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public string State { get; set; } = PaymentStates.Creating;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextCheckAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
