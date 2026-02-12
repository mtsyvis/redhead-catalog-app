using Microsoft.AspNetCore.Mvc;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy", message = "Redhead Sites Catalog API is running" });
    }
}
