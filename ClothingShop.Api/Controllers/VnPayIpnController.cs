using ClothingShop.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClothingShop.Api.Controllers;

[ApiController]
[Route("api/vnpay/ipn")]
public class VnPayIpnController(OrderService orderService, VnPayService vnPayService, ILogger<VnPayIpnController> logger) : ControllerBase
{
    // IPN: VNPAY server calls this URL to notify payment result. Verify and update order, return 200.
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var result = await vnPayService.ProcessReturnAsync(Request.QueryString.Value ?? string.Empty);
            if (result.Success)
            {
                await orderService.UpdateOrderStatusAsync(result.OrderId, result.Status);
                logger.LogInformation("VNPAY IPN - Updated order {OrderId} to {Status}", result.OrderId, result.Status);
            }
            // VNPAY expects HTTP 200 to acknowledge receipt
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "VNPAY IPN processing failed");
            return BadRequest(new { message = ex.Message });
        }
    }
}


