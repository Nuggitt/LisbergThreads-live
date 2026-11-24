using System.ComponentModel.DataAnnotations.Schema;

namespace LisbergThreads.Api.Models
{
    [Table("OrderItems")]
    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; private set; }

    }
}
