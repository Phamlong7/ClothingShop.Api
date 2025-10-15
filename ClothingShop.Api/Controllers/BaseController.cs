using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ClothingShop.Api.Controllers;

public abstract class BaseController : ControllerBase
{
    protected Guid GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (userIdStr == null)
        {
            throw new UnauthorizedAccessException("User identifier claim not found.");
        }
        return Guid.Parse(userIdStr);
    }
}
