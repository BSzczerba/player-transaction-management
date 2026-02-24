using Application.DTOs;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentMethodsController : ControllerBase
{
    private readonly IPaymentMethodService _svc;

    public PaymentMethodsController(IPaymentMethodService svc) => _svc = svc;

    /// <summary>Get all active payment methods</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PaymentMethodDto>), 200)]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var methods = await _svc.GetActiveAsync(ct);
        return Ok(methods);
    }

    /// <summary>Get a payment method by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentMethodDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var method = await _svc.GetByIdAsync(id, ct);
        return method is null ? NotFound() : Ok(method);
    }
}
