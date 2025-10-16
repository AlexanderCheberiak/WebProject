namespace WebProject.Models;

public class Order
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public bool IsReservation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Restaurant? Restaurant { get; set; }
    public ICollection<OrderItem>? OrderItems { get; set; }
}