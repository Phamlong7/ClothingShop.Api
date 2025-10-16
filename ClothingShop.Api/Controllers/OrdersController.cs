using ClothingShop.Api.Dtos;
using ClothingShop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController(OrderService orderService) : BaseController
{

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = GetUserId();
        var orders = await orderService.GetUserOrdersAsync(userId);
        return Ok(orders);
    }

    [HttpPost]
    public async Task<IActionResult> Place([FromBody] PlaceOrderDto dto)
    {
        var userId = GetUserId();
        try
        {
            var (order, paymentData) = await orderService.CreateOrderAsync(userId, dto);
            
            var orderId = ((dynamic)order).Id;
            if (paymentData is not null)
            {
                return CreatedAtAction(nameof(GetById), new { id = orderId }, new { id = orderId, order, payment = paymentData });
            }

            return CreatedAtAction(nameof(GetById), new { id = orderId }, new { id = orderId, order });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var order = await orderService.GetOrderByIdAsync(id, userId);
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayOrderDto dto)
    {
        var userId = GetUserId();
        try
        {
            var order = await orderService.SimulatePaymentAsync(id, userId);
            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        try
        {
            await orderService.DeleteOrderAsync(id, userId);
            return Ok(new { ok = true });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}