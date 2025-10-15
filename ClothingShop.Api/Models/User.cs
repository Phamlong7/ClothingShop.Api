using Microsoft.AspNetCore.Identity;

namespace ClothingShop.Api.Models;

public class User : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


