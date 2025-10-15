using System.Net.Http.Json;
using System.Text;
using System.Security.Claims;
using ClothingShop.Api.Data;
using ClothingShop.Api.Dtos;
using ClothingShop.Api.Models;
using ClothingShop.Api.Utils;
using ClothingShop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration, VnPayService vnPayService) : BaseController
{

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = GetUserId();
        var orders = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return Ok(orders);
    }

    [HttpPost]
    public async Task<IActionResult> Place([FromBody] PlaceOrderDto dto)
    {
        var userId = GetUserId();
        var cartItems = await db.CartItems.Where(c => c.UserId == userId).ToListAsync();
        if (cartItems.Count == 0) return BadRequest(new { message = "Cart is empty" });

        var productIds = cartItems.Select(c => c.ProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var order = new Order { UserId = userId };
        decimal total = 0;
        foreach (var c in cartItems)
        {
            if (!products.TryGetValue(c.ProductId, out var p))
            {
                return BadRequest(new { message = $"Product with ID {c.ProductId} is no longer available." });
            }
            order.Items.Add(new OrderItem
            {
                ProductId = p.Id,
                Quantity = c.Quantity,
                UnitPrice = p.Price
            });
            total += p.Price * c.Quantity;
        }
        order.TotalAmount = total;
        db.Orders.Add(order);
        db.CartItems.RemoveRange(cartItems);
        await db.SaveChangesAsync();
        // If client requests PayOS payment link, create it
        if (string.Equals(dto.PaymentMethod, "payos", StringComparison.OrdinalIgnoreCase))
        {
            var client = httpClientFactory.CreateClient("payos");
            var payReq = new
            {
                orderCode = order.Id.ToString("N"),
                amount = (int)Math.Round(order.TotalAmount),
                description = $"Order {order.Id}",
                returnUrl = configuration["PayOS:ReturnUrl"]
            };
            var resp = await client.PostAsJsonAsync("/payments", payReq);
            if (resp.IsSuccessStatusCode)
            {
                var payload = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                return CreatedAtAction(nameof(GetById), new { id = order.Id }, new { order, payos = payload });
            }
        }
        // If client requests VNPAY payment link, create it via service
        if (string.Equals(dto.PaymentMethod, "vnpay", StringComparison.OrdinalIgnoreCase))
        {
            var (payUrl, rawSignData) = vnPayService.CreatePaymentUrl(order);
            return CreatedAtAction(nameof(GetById), new { id = order.Id }, new { order, vnpay = new { url = payUrl, debug_sign_data = rawSignData } });
        }
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayOrderDto dto)
    {
        var userId = GetUserId();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
        if (order is null) return NotFound();
        if (order.Status == "paid") return BadRequest(new { message = "Order already paid" });

        // Simulate payment success
        order.Status = "paid";
        await db.SaveChangesAsync();
        return Ok(order);
    }
}


