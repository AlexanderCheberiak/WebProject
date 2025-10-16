namespace WebProject.Models;

public class MenuItem
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }

    public Restaurant? Restaurant { get; set; }
}