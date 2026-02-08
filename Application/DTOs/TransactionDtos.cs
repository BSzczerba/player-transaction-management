using Domain.Enums;

namespace Application.DTOs;

/// <summary>
/// DTO for creating a deposit
/// </summary>
public class CreateDepositDto
{
    public decimal Amount { get; set; }
    public Guid PaymentMethodId { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO for creating a withdrawal
/// </summary>
public class CreateWithdrawalDto
{
    public decimal Amount { get; set; }
    public Guid PaymentMethodId { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO for transaction information
/// </summary>
public class TransactionDto
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerUsername { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PaymentMethodName { get; set; }
    public string? PaymentGatewayReference { get; set; }
    public string? Description { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByUsername { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsFlagged { get; set; }
    public string? FlagReason { get; set; }
    public decimal? BalanceBefore { get; set; }
    public decimal? BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for approving a transaction
/// </summary>
public class ApproveTransactionDto
{
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for rejecting a transaction
/// </summary>
public class RejectTransactionDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO for transaction filters
/// </summary>
public class TransactionFilterDto
{
    public Guid? PlayerId { get; set; }
    public TransactionType? Type { get; set; }
    public TransactionStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public bool? IsFlagged { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}