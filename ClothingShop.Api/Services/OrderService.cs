using ClothingShop.Api.Data;
using ClothingShop.Api.Dtos;
using ClothingShop.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingShop.Api.Services;

public class OrderService
{
    private readonly AppDbContext _db;
    private readonly VnPayService _vnPayService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public OrderService(AppDbContext db, VnPayService vnPayService, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _db = db;
        _vnPayService = vnPayService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<List<object>> GetUserOrdersAsync(Guid userId)
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var allProductIds = orders.SelectMany(o => o.Items.Select(i => i.ProductId)).Distinct().ToList();
        var products = await _db.Products
            .Where(p => allProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        return orders.Select(o => new
        {
            id = o.Id,
            userId = o.UserId,
            totalAmount = o.TotalAmount,
            status = o.Status,
            createdAt = o.CreatedAt,
            items = o.Items.Select(i => new
            {
                id = i.Id,
                orderId = i.OrderId,
                productId = i.ProductId,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                product = products.TryGetValue(i.ProductId, out var p)
                    ? new { p.Id, p.Name, p.Image, p.Price }
                    : null
            })
        }).ToList<object>();
    }

    public async Task<object?> GetOrderByIdAsync(Guid orderId, Guid userId)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        if (order is null) return null;

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        return new
        {
            id = order.Id,
            userId = order.UserId,
            totalAmount = order.TotalAmount,
            status = order.Status,
            createdAt = order.CreatedAt,
            items = order.Items.Select(i => new
            {
                id = i.Id,
                orderId = i.OrderId,
                productId = i.ProductId,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                product = products.TryGetValue(i.ProductId, out var p)
                    ? new { p.Id, p.Name, p.Image, p.Price }
                    : null
            })
        };
    }

    public async Task<(object Order, object? PaymentData)> CreateOrderAsync(Guid userId, PlaceOrderDto dto)
    {
        var cartItems = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();
        if (cartItems.Count == 0) throw new InvalidOperationException("Cart is empty");

        var productIds = cartItems.Select(c => c.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var order = new Order { UserId = userId };
        decimal total = 0;
        foreach (var c in cartItems)
        {
            if (!products.TryGetValue(c.ProductId, out var p))
            {
                throw new InvalidOperationException($"Product with ID {c.ProductId} is no longer available.");
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
        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cartItems);
        await _db.SaveChangesAsync();

        object? paymentData = null;

        // Handle PayOS payment
        if (string.Equals(dto.PaymentMethod, "payos", StringComparison.OrdinalIgnoreCase))
        {
            var client = _httpClientFactory.CreateClient("payos");
            var payReq = new
            {
                orderCode = order.Id.ToString("N"),
                amount = (int)Math.Round(order.TotalAmount),
                description = $"Order {order.Id}",
                returnUrl = _configuration["PayOS:ReturnUrl"]
            };
            var resp = await client.PostAsJsonAsync("/payments", payReq);
            if (resp.IsSuccessStatusCode)
            {
                paymentData = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            }
        }
        // Handle VNPAY payment
        else if (string.Equals(dto.PaymentMethod, "vnpay", StringComparison.OrdinalIgnoreCase))
        {
            var (payUrl, rawSignData) = _vnPayService.CreatePaymentUrl(order);
            paymentData = new { url = payUrl, debug_sign_data = rawSignData };
        }

        return (order, paymentData);
    }

    public async Task<object> SimulatePaymentAsync(Guid orderId, Guid userId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        if (order is null) throw new InvalidOperationException("Order not found");
        if (order.Status == "paid") throw new InvalidOperationException("Order already paid");

        order.Status = "paid";
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task DeleteOrderAsync(Guid orderId, Guid userId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        if (order is null) throw new InvalidOperationException("Order not found");

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, string status)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is not null)
        {
            order.Status = status;
            await _db.SaveChangesAsync();
        }
    }
}
