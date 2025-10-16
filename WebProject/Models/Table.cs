namespace WebProject.Models;

public class Table
{
    public int Id { get; set; }
    public int RestaurantId { get; set; }
    public int TableNumber { get; set; }
    public int Capacity { get; set; }

    public Restaurant? Restaurant { get; set; }
}