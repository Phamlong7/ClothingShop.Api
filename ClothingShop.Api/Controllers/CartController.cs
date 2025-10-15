using System.Security.Claims;
using ClothingShop.Api.Data;
using ClothingShop.Api.Dtos;
using ClothingShop.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController(AppDbContext db) : ControllerBase
{
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();
        var items = await db.Set<CartItem>()
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var data = items.Select(i => new
        {
            id = i.Id,
            product = products.GetValueOrDefault(i.ProductId),
            quantity = i.Quantity,
            lineTotal = (products.GetValueOrDefault(i.ProductId)?.Price ?? 0) * i.Quantity
        }).ToList();

        var total = data.Sum(x => x.lineTotal);
        return Ok(new { items = data, total });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CartAddDto dto)
    {
        var userId = GetUserId();
        var exists = await db.Products.AnyAsync(p => p.Id == dto.ProductId);
        if (!exists) return NotFound(new { message = "Product not found" });

        var item = await db.Set<CartItem>().FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == dto.ProductId);
        if (item is null)
        {
            item = new CartItem { UserId = userId, ProductId = dto.ProductId, Quantity = dto.Quantity };
            db.Add(item);
        }
        else
        {
            item.Quantity += dto.Quantity;
        }
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CartUpdateDto dto)
    {
        var userId = GetUserId();
        var item = await db.Set<CartItem>().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (item is null) return NotFound();
        item.Quantity = dto.Quantity;
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id)
    {
        var userId = GetUserId();
        var item = await db.Set<CartItem>().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (item is null) return NotFound();
        db.Remove(item);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}


