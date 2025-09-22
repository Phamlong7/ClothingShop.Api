namespace ClothingShop.Api.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;          // required
    public string Description { get; set; } = default!;   // required
    public decimal Price { get; set; }                    // required >= 0
    public string? Image { get; set; }                    // optional URL
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
