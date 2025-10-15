using Microsoft.AspNetCore.Identity;

namespace ClothingShop.Api.Models;

/// <summary>
/// Represents an application user and extends <see cref="IdentityUser{TKey}"/> with <see cref="Guid"/> key.
/// This entity integrates with ASP.NET Core Identity and can be extended with custom profile fields.
/// </summary>
public class User : IdentityUser<Guid>
{
    /// <summary>
    /// Gets or sets the UTC timestamp when the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


