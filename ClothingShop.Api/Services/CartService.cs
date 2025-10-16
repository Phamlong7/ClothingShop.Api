using ClothingShop.Api.Data;
using ClothingShop.Api.Dtos;
using ClothingShop.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Api.Services;

public class CartService
{
    private readonly AppDbContext _db;

    public CartService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<object> GetUserCartAsync(Guid userId)
    {
        var cartItems = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();
        if (cartItems.Count == 0) return new { items = new object[0], total = 0 };

        var productIds = cartItems.Select(c => c.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var items = cartItems.Select(c => new
        {
            id = c.Id,
            product = products.TryGetValue(c.ProductId, out var p) ? new { p.Id, p.Name, p.Image, p.Price } : null,
            quantity = c.Quantity,
            lineTotal = products.TryGetValue(c.ProductId, out var prod) ? prod.Price * c.Quantity : 0
        }).ToList();

        var total = items.Sum(i => i.lineTotal);

        return new { items, total };
    }

    public async Task<object> AddToCartAsync(Guid userId, CartAddDto dto)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId);
        if (product is null) throw new InvalidOperationException("Product not found");

        var existing = await _db.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == dto.ProductId);

        if (existing is not null)
        {
            existing.Quantity += dto.Quantity;
        }
        else
        {
            _db.CartItems.Add(new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            });
        }

        await _db.SaveChangesAsync();
        return new { message = "Added to cart" };
    }

    public async Task<object> UpdateCartItemAsync(Guid cartItemId, Guid userId, CartUpdateDto dto)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);
        if (item is null) throw new InvalidOperationException("Cart item not found");

        item.Quantity = dto.Quantity;
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task RemoveFromCartAsync(Guid cartItemId, Guid userId)
    {
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);
        if (item is not null)
        {
            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync();
        }
    }
}
