using System.ComponentModel.DataAnnotations.Schema;
using LisbergThreads.Api.Data;

namespace LisbergThreads.Api.Models
{
    [Table("Orders")]
    public class Order
    {
        public Guid Id { get; set; }

        public Guid ConfirmationToken { get; set; } = Guid.NewGuid();

        public Guid? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = "Pending";
        public bool IsActive { get; set; } = true;

        public byte[] RowVersion { get; set; } = [];

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}
