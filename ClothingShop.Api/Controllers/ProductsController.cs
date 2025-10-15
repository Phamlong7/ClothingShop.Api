using ClothingShop.Api.Data;
using ClothingShop.Api.Dtos;
using ClothingShop.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int limit = 12)
    {
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 12;

        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{q}%"));

        var total = await query.CountAsync();
        var data = await query.OrderByDescending(p => p.CreatedAt)
                              .Skip((page - 1) * limit)
                              .Take(limit)
                              .ToListAsync();

        return Ok(new { data, total, page, pages = (int)Math.Ceiling(total / (double)limit) });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await db.Products.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
    {
        var entity = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            Price = dto.Price,
            Image = string.IsNullOrWhiteSpace(dto.Image) ? null : dto.Image!.Trim()
        };

        db.Products.Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProductUpdateDto dto)
    {
        var entity = await db.Products.FindAsync(id);
        if (entity is null) return NotFound();

        if (dto.Name is not null)
            entity.Name = dto.Name.Trim();
        
        if (dto.Description is not null)
            entity.Description = dto.Description.Trim();
        
        if (dto.Price.HasValue)
            entity.Price = dto.Price.Value;
        
        if (dto.Image is not null)
            entity.Image = string.IsNullOrWhiteSpace(dto.Image) ? null : dto.Image.Trim();

        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(entity);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await db.Products.FindAsync(id);
        if (entity is null) return NotFound();
        db.Products.Remove(entity);
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
