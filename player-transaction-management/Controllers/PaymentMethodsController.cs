using Application.Repositories.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentMethodsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public PaymentMethodsController(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    /// <summary>Pobierz wszystkie aktywne metody płatności</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PaymentMethodDto>), 200)]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var methods = await _uow.PaymentMethods.GetActivePaymentMethodsAsync(ct);
        var dtos = _mapper.Map<IEnumerable<PaymentMethodDto>>(methods);
        return Ok(dtos);
    }

    /// <summary>Pobierz metodę płatności po ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentMethodDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var method = await _uow.PaymentMethods.GetByIdAsync(id, ct);
        if (method is null) return NotFound();

        var dto = _mapper.Map<PaymentMethodDto>(method);
        return Ok(dto);
    }
}

// DTO dla Payment Method (mogłoby być w Application/DTOs ale trzymamy tutaj dla szybkości)
public class PaymentMethodDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public decimal FeePercentage { get; set; }
    public decimal FixedFee { get; set; }
    public int ProcessingTimeMinutes { get; set; }
}