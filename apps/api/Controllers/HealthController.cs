using Api.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth()
    {
        var response = new ApiResponse<object>
        {
            Data = new
            {
                Status = "healthy",
                Service = "mayorx-content-engine-api"
            },
            Message = "Service is healthy"
        };

        return Ok(response);
    }
}