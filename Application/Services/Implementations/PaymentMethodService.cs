using Application.DTOs;
using Application.Repositories.Interfaces;
using Application.Services.Interfaces;
using AutoMapper;

namespace Application.Services.Implementations;

/// <summary>
/// Service for payment method operations
/// </summary>
public class PaymentMethodService : IPaymentMethodService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public PaymentMethodService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IEnumerable<PaymentMethodDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var methods = await _uow.PaymentMethods.GetActivePaymentMethodsAsync(ct);
        return _mapper.Map<IEnumerable<PaymentMethodDto>>(methods);
    }

    public async Task<PaymentMethodDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var method = await _uow.PaymentMethods.GetByIdAsync(id, ct);
        return method is null ? null : _mapper.Map<PaymentMethodDto>(method);
    }
}
