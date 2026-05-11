using Microsoft.AspNetCore.Mvc;
using BiasAudit.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace BiasAudit.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public IActionResult Get() =>
        Ok(new HealthResponse("ok", "bias-audit-api"));
}
