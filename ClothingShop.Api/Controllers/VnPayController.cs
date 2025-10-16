using System.Web;
using ClothingShop.Api.Data;
using ClothingShop.Api.Models;
using ClothingShop.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/vnpay")] 
public class VnPayController(OrderService orderService, VnPayService vnPayService, ILogger<VnPayController> logger) : ControllerBase
{
    // Create payment URL for an order (simple, unsecured demo)
    [HttpPost("create/{orderId:guid}")]
    public async Task<IActionResult> Create(Guid orderId)
    {
        var order = await orderService.GetOrderByIdAsync(orderId, Guid.Empty);
        if (order is null) return NotFound();
        if (((dynamic)order).Status == "paid") return BadRequest(new { message = "Order already paid" });

        // Convert dynamic order to Order model for VnPayService
        var orderModel = new Order
        {
            Id = ((dynamic)order).Id,
            TotalAmount = ((decimal)((dynamic)order).TotalAmount)
        };

        var (payUrl, rawSignData) = vnPayService.CreatePaymentUrl(orderModel);
        return Ok(new { url = payUrl, debug_sign_data = rawSignData });
    }

    [HttpGet("return")]
    public async Task<IActionResult> Return()
    {
        try
        {
            var result = await vnPayService.ProcessReturnAsync(Request.QueryString.Value ?? string.Empty);
            if (result.Success)
            {
                await orderService.UpdateOrderStatusAsync(result.OrderId, result.Status);
                logger.LogInformation("Order {OrderId} status updated to '{Status}'.", result.OrderId, result.Status);
            }
            return Ok(new { ok = true, message = "Payment status updated." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "VNPAY Return processing failed");
            return BadRequest(new { message = ex.Message });
        }
    }

}


