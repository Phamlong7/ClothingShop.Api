using ClothingShop.Api.Dtos;
using ClothingShop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController(CartService cartService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();
        var cart = await cartService.GetUserCartAsync(userId);
        return Ok(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CartAddDto dto)
    {
        var userId = GetUserId();
        try
        {
            var result = await cartService.AddToCartAsync(userId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CartUpdateDto dto)
    {
        var userId = GetUserId();
        try
        {
            var result = await cartService.UpdateCartItemAsync(id, userId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id)
    {
        var userId = GetUserId();
        await cartService.RemoveFromCartAsync(id, userId);
        return Ok(new { message = "Removed from cart" });
    }
}