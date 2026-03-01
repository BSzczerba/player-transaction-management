using Application.DTOs;
using Application.Repositories.Interfaces;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implementations;

/// <summary>
/// Service responsible for transaction management - deposits, withdrawals, approvals.
/// Contains full business logic: limit validation, AML detection, audit logging.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<TransactionService> _log;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    // AML thresholds (configurable via constructor or IOptions in future)
    private const int AmlVelocityThreshold = 5;
    private const decimal AmlSingleAmountThreshold = 10_000m;
    private const decimal AmlDailyVolumeThreshold = 20_000m;
    private const decimal AutoApproveDepositThreshold = 100m;

    public TransactionService(IUnitOfWork uow, IMapper mapper, ILogger<TransactionService> log,
        IAuditService audit, INotificationService notifications)
    {
        _uow = uow;
        _mapper = mapper;
        _log = log;
        _audit = audit;
        _notifications = notifications;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEPOSIT
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto> CreateDepositAsync(
        Guid playerId,
        CreateDepositDto dto,
        string? ipAddress,
        CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var player = await _uow.Players.GetByIdAsync(playerId, ct)
                ?? throw new InvalidOperationException("Player not found.");

            if (player.Status != AccountStatus.Active)
                throw new InvalidOperationException($"Account status is {player.Status}. Cannot deposit.");

            var paymentMethod = await _uow.PaymentMethods.GetByIdAsync(dto.PaymentMethodId, ct)
                ?? throw new InvalidOperationException("Payment method not found.");

            if (!paymentMethod.IsActive)
                throw new InvalidOperationException("Payment method is not active.");

            if (dto.Amount < paymentMethod.MinAmount || dto.Amount > paymentMethod.MaxAmount)
                throw new InvalidOperationException(
                    $"Amount must be between {paymentMethod.MinAmount} and {paymentMethod.MaxAmount}.");

            var depositToday = (await _uow.Transactions.GetTodaysTransactionsByPlayerAsync(playerId, ct))
                .Where(t => t.Type == TransactionType.Deposit
                         && (t.Status == TransactionStatus.Completed || t.Status == TransactionStatus.Pending))
                .Sum(t => t.Amount);
            if (depositToday + dto.Amount > player.DailyDepositLimit)
                throw new InvalidOperationException(
                    $"Daily deposit limit exceeded. Limit: {player.DailyDepositLimit}, " +
                    $"already deposited today: {depositToday}.");

            var transaction = new Transaction
            {
                PlayerId = playerId,
                Type = TransactionType.Deposit,
                Amount = dto.Amount,
                Status = TransactionStatus.Pending,
                PaymentMethodId = dto.PaymentMethodId,
                Description = dto.Description,
                IpAddress = ipAddress,
                BalanceBefore = player.Balance,
                BalanceAfter = player.Balance + dto.Amount
            };

            // AML check
            var (isSuspicious, flagReason) = await DetectSuspiciousActivity(playerId, dto.Amount, TransactionType.Deposit, ct);
            if (isSuspicious)
            {
                transaction.IsFlagged = true;
                transaction.FlagReason = flagReason;
                _log.LogWarning("Suspicious deposit detected for player {PlayerId}: {Amount}", playerId, dto.Amount);
            }

            // Auto-approve only if NOT flagged AND below threshold
            if (dto.Amount < AutoApproveDepositThreshold && !isSuspicious)
            {
                transaction.Status = TransactionStatus.Completed;
                transaction.CompletedAt = DateTime.UtcNow;
                player.Balance += dto.Amount;
                _uow.Players.Update(player);
                _log.LogInformation("Auto-approved deposit of {Amount} for player {PlayerId}", dto.Amount, playerId);
            }
            else
            {
                _log.LogInformation("Deposit of {Amount} for player {PlayerId} pending approval (flagged={IsFlagged})",
                    dto.Amount, playerId, isSuspicious);
            }

            await _uow.Transactions.AddAsync(transaction, ct);
            await _audit.LogAsync(playerId, "CreateDeposit", "Transaction", transaction.Id, ipAddress, ct: ct);

            // Notify compliance officers about AML-flagged transactions
            if (isSuspicious)
            {
                await NotifyComplianceOfficersAsync(transaction, flagReason, ct);
            }

            await _uow.CommitTransactionAsync(ct);

            return _mapper.Map<TransactionDto>(transaction);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WITHDRAWAL
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto> CreateWithdrawalAsync(
        Guid playerId,
        CreateWithdrawalDto dto,
        string? ipAddress,
        CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var player = await _uow.Players.GetByIdAsync(playerId, ct)
                ?? throw new InvalidOperationException("Player not found.");

            if (player.Status != AccountStatus.Active)
                throw new InvalidOperationException($"Account status is {player.Status}. Cannot withdraw.");

            if (!player.KycVerified)
                throw new InvalidOperationException("KYC verification required for withdrawals.");

            var paymentMethod = await _uow.PaymentMethods.GetByIdAsync(dto.PaymentMethodId, ct)
                ?? throw new InvalidOperationException("Payment method not found.");

            if (!paymentMethod.IsActive)
                throw new InvalidOperationException("Payment method is not active.");

            if (dto.Amount < paymentMethod.MinAmount || dto.Amount > paymentMethod.MaxAmount)
                throw new InvalidOperationException(
                    $"Amount must be between {paymentMethod.MinAmount} and {paymentMethod.MaxAmount}.");

            if (player.Balance < dto.Amount)
                throw new InvalidOperationException(
                    $"Insufficient balance. Available: {player.Balance}, requested: {dto.Amount}.");

            var withdrawalToday = (await _uow.Transactions.GetTodaysTransactionsByPlayerAsync(playerId, ct))
                .Where(t => t.Type == TransactionType.Withdrawal
                         && (t.Status == TransactionStatus.Completed || t.Status == TransactionStatus.Pending))
                .Sum(t => t.Amount);
            if (withdrawalToday + dto.Amount > player.DailyWithdrawalLimit)
                throw new InvalidOperationException(
                    $"Daily withdrawal limit exceeded. Limit: {player.DailyWithdrawalLimit}, " +
                    $"already withdrawn today: {withdrawalToday}.");

            var transaction = new Transaction
            {
                PlayerId = playerId,
                Type = TransactionType.Withdrawal,
                Amount = dto.Amount,
                Status = TransactionStatus.Pending,
                PaymentMethodId = dto.PaymentMethodId,
                Description = dto.Description,
                IpAddress = ipAddress,
                BalanceBefore = player.Balance,
                BalanceAfter = player.Balance - dto.Amount
            };

            var (isSuspicious, flagReason) = await DetectSuspiciousActivity(playerId, dto.Amount, TransactionType.Withdrawal, ct);
            if (isSuspicious)
            {
                transaction.IsFlagged = true;
                transaction.FlagReason = flagReason;
                _log.LogWarning("Suspicious withdrawal detected for player {PlayerId}: {Amount}", playerId, dto.Amount);
            }

            await _uow.Transactions.AddAsync(transaction, ct);
            await _audit.LogAsync(playerId, "CreateWithdrawal", "Transaction", transaction.Id, ipAddress, ct: ct);

            // Notify compliance officers about AML-flagged transactions
            if (isSuspicious)
            {
                await NotifyComplianceOfficersAsync(transaction, flagReason, ct);
            }

            await _uow.CommitTransactionAsync(ct);

            _log.LogInformation("Withdrawal of {Amount} for player {PlayerId} pending approval", dto.Amount, playerId);

            return _mapper.Map<TransactionDto>(transaction);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // APPROVE
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto> ApproveAsync(
        Guid transactionId,
        Guid operatorId,
        string? notes,
        CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var transaction = await _uow.Transactions.GetByIdAsync(transactionId, ct)
                ?? throw new InvalidOperationException("Transaction not found.");

            if (transaction.Status != TransactionStatus.Pending)
                throw new InvalidOperationException(
                    $"Cannot approve transaction in status {transaction.Status}.");

            // Segregation of duties: operator cannot approve their own transaction
            if (transaction.PlayerId == operatorId)
                throw new InvalidOperationException("Cannot approve your own transaction.");

            var player = await _uow.Players.GetByIdAsync(transaction.PlayerId, ct)
                ?? throw new InvalidOperationException("Player not found.");

            if (transaction.Type == TransactionType.Deposit)
            {
                player.Balance += transaction.Amount;
            }
            else if (transaction.Type == TransactionType.Withdrawal)
            {
                if (player.Balance < transaction.Amount)
                    throw new InvalidOperationException(
                        "Insufficient balance. Player balance may have changed.");

                player.Balance -= transaction.Amount;
            }

            transaction.Status = TransactionStatus.Completed;
            transaction.CompletedAt = DateTime.UtcNow;
            transaction.ApprovedById = operatorId;
            transaction.ApprovedAt = DateTime.UtcNow;
            transaction.BalanceAfter = player.Balance;

            if (!string.IsNullOrEmpty(notes))
                transaction.Description = (transaction.Description ?? "") + " [Operator notes: " + notes + "]";

            _uow.Transactions.Update(transaction);
            _uow.Players.Update(player);

            await _audit.LogAsync(operatorId, "ApproveTransaction", "Transaction", transactionId, ct: ct);

            await _notifications.CreateAsync(
                player.Id,
                "TransactionUpdate",
                "Transaction Approved",
                $"Your {transaction.Type} of {transaction.Amount:C} has been approved.",
                "Transaction", transaction.Id, ct);

            await _uow.CommitTransactionAsync(ct);

            _log.LogInformation("Transaction {TransactionId} approved by operator {OperatorId}",
                transactionId, operatorId);

            return _mapper.Map<TransactionDto>(transaction);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // REJECT
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto> RejectAsync(
        Guid transactionId,
        Guid operatorId,
        string reason,
        CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var transaction = await _uow.Transactions.GetByIdAsync(transactionId, ct)
                ?? throw new InvalidOperationException("Transaction not found.");

            if (transaction.Status != TransactionStatus.Pending)
                throw new InvalidOperationException(
                    $"Cannot reject transaction in status {transaction.Status}.");

            transaction.Status = TransactionStatus.Rejected;
            transaction.ApprovedById = operatorId;
            transaction.ApprovedAt = DateTime.UtcNow;
            transaction.RejectionReason = reason;
            transaction.BalanceAfter = transaction.BalanceBefore;

            _uow.Transactions.Update(transaction);

            await _audit.LogAsync(operatorId, "RejectTransaction", "Transaction", transactionId, ct: ct);

            await _notifications.CreateAsync(
                transaction.PlayerId,
                "TransactionUpdate",
                "Transaction Rejected",
                $"Your {transaction.Type} of {transaction.Amount:C} was rejected. Reason: {reason}",
                "Transaction", transaction.Id, ct);

            await _uow.CommitTransactionAsync(ct);

            _log.LogInformation("Transaction {TransactionId} rejected by operator {OperatorId}",
                transactionId, operatorId);

            return _mapper.Map<TransactionDto>(transaction);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // QUERIES
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var transaction = await _uow.Transactions.GetByIdAsync(id, ct);
        return transaction is null ? null : _mapper.Map<TransactionDto>(transaction);
    }

    public async Task<IEnumerable<TransactionDto>> GetByPlayerAsync(Guid playerId, CancellationToken ct = default)
    {
        var transactions = await _uow.Transactions.GetByPlayerIdAsync(playerId, ct);
        return _mapper.Map<IEnumerable<TransactionDto>>(transactions);
    }

    public async Task<PagedResult<TransactionDto>> GetByPlayerPagedAsync(
        Guid playerId, int page, int pageSize, CancellationToken ct = default)
    {
        var filter = new TransactionFilterDto
        {
            PlayerId = playerId,
            Page = page,
            PageSize = pageSize
        };
        return await GetAllAsync(filter, ct);
    }

    public async Task<PagedResult<TransactionDto>> GetPendingAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var filter = new TransactionFilterDto
        {
            Status = TransactionStatus.Pending,
            Page = page,
            PageSize = pageSize
        };
        return await GetAllAsync(filter, ct);
    }

    public async Task<PagedResult<TransactionDto>> GetAllAsync(
        TransactionFilterDto filter, CancellationToken ct = default)
    {
        var (items, totalCount) = await _uow.Transactions.GetFilteredAsync(filter, ct);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        return new PagedResult<TransactionDto>
        {
            Items = _mapper.Map<IEnumerable<TransactionDto>>(items),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<TransactionDto>> GetFlaggedAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var filter = new TransactionFilterDto
        {
            IsFlagged = true,
            Page = page,
            PageSize = pageSize
        };
        return await GetAllAsync(filter, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// AML detection — uses a 24-hour rolling window query translated to SQL.
    /// Returns a flag reason string for transparency in audit trail.
    /// </summary>
    private async Task<(bool IsSuspicious, string Reason)> DetectSuspiciousActivity(
        Guid playerId, decimal amount, TransactionType type, CancellationToken ct)
    {
        var last24h = (await _uow.Transactions.GetLast24HoursTransactionsByPlayerAsync(playerId, ct)).ToList();

        // Red flag 1: velocity check
        if (last24h.Count >= AmlVelocityThreshold)
            return (true, $"High transaction velocity: {AmlVelocityThreshold}+ transactions in 24 hours.");

        // Red flag 2: single transaction above threshold
        if (amount > AmlSingleAmountThreshold)
            return (true, $"Single transaction amount ({amount:C}) exceeds AML threshold of {AmlSingleAmountThreshold:C}.");

        // Red flag 3: total volume in 24 hours exceeds threshold
        var total24h = last24h.Sum(t => t.Amount) + amount;
        if (total24h > AmlDailyVolumeThreshold)
            return (true, $"24-hour transaction volume ({total24h:C}) exceeds AML threshold of {AmlDailyVolumeThreshold:C}.");

        return (false, string.Empty);
    }

    /// <summary>
    /// Notify all compliance officers about a flagged transaction.
    /// </summary>
    private async Task NotifyComplianceOfficersAsync(Transaction transaction, string flagReason, CancellationToken ct)
    {
        var complianceOfficers = await _uow.Players.GetByRoleAsync(UserRole.ComplianceOfficer, ct);
        foreach (var officer in complianceOfficers)
        {
            await _notifications.CreateAsync(
                officer.Id,
                "AmlAlert",
                "Suspicious Transaction Detected",
                $"Transaction {transaction.Id} ({transaction.Type}, {transaction.Amount:C}) flagged: {flagReason}",
                "Transaction", transaction.Id, ct);
        }
    }
}
