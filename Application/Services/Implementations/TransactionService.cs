using Application.DTOs;
using Application.Services.Interfaces;
using Application.Repositories.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implementations;

/// <summary>
/// Serwis zarządzający transakcjami - wpłaty, wypłaty, zatwierdzanie.
/// Zawiera pełną logikę biznesową: walidacja limitów, AML, audyt.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<TransactionService> _log;

    public TransactionService(IUnitOfWork uow, IMapper mapper, ILogger<TransactionService> log)
    {
        _uow = uow;
        _mapper = mapper;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEPOSIT (wpłata)
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto> CreateDepositAsync(
        Guid playerId,
        CreateDepositDto dto,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        // Sprawdź status konta
        if (player.Status != AccountStatus.Active)
            throw new InvalidOperationException($"Account status is {player.Status}. Cannot deposit.");

        // Sprawdź metodę płatności
        var paymentMethod = await _uow.PaymentMethods.GetByIdAsync(dto.PaymentMethodId, ct)
            ?? throw new InvalidOperationException("Payment method not found.");

        if (!paymentMethod.IsActive)
            throw new InvalidOperationException("Payment method is not active.");

        // Walidacja kwoty (min/max metody płatności)
        if (dto.Amount < paymentMethod.MinAmount || dto.Amount > paymentMethod.MaxAmount)
            throw new InvalidOperationException(
                $"Amount must be between {paymentMethod.MinAmount} and {paymentMethod.MaxAmount}.");

        // Sprawdź dzienny limit depozytów
        var depositToday = (await _uow.Transactions.GetTodaysTransactionsByPlayerAsync(playerId, ct))
            .Where(t => t.Type == TransactionType.Deposit)
            .Sum(t => t.Amount);
        if (depositToday + dto.Amount > player.DailyDepositLimit)
            throw new InvalidOperationException(
                $"Daily deposit limit exceeded. Limit: {player.DailyDepositLimit}, " +
                $"already deposited today: {depositToday}.");

        // Utwórz transakcję
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
            BalanceAfter = player.Balance + dto.Amount  // przewidywane
        };

        // Wpłaty < 100 automatycznie zatwierdzane, większe wymagają przeglądu
        if (dto.Amount < 100m)
        {
            transaction.Status = TransactionStatus.Completed;
            transaction.CompletedAt = DateTime.UtcNow;
            player.Balance += dto.Amount;
            _uow.Players.Update(player);

            _log.LogInformation("Auto-approved deposit of {Amount} for player {PlayerId}",
                dto.Amount, playerId);
        }
        else
        {
            _log.LogInformation("Deposit of {Amount} for player {PlayerId} pending approval",
                dto.Amount, playerId);
        }

        await _uow.Transactions.AddAsync(transaction, ct);
        await _uow.SaveChangesAsync(ct);

        // Audit log
        await CreateAuditLog("CreateDeposit", playerId, transaction.Id, ipAddress, ct);

        return _mapper.Map<TransactionDto>(transaction);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WITHDRAWAL (wypłata)
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto> CreateWithdrawalAsync(
        Guid playerId,
        CreateWithdrawalDto dto,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        // Sprawdź status konta
        if (player.Status != AccountStatus.Active)
            throw new InvalidOperationException($"Account status is {player.Status}. Cannot withdraw.");

        // Weryfikacja KYC wymagana do wypłat
        if (!player.KycVerified)
            throw new InvalidOperationException("KYC verification required for withdrawals.");

        // Sprawdź metodę płatności
        var paymentMethod = await _uow.PaymentMethods.GetByIdAsync(dto.PaymentMethodId, ct)
            ?? throw new InvalidOperationException("Payment method not found.");

        if (!paymentMethod.IsActive)
            throw new InvalidOperationException("Payment method is not active.");

        // Walidacja kwoty
        if (dto.Amount < paymentMethod.MinAmount || dto.Amount > paymentMethod.MaxAmount)
            throw new InvalidOperationException(
                $"Amount must be between {paymentMethod.MinAmount} and {paymentMethod.MaxAmount}.");

        // Sprawdź czy ma wystarczający balans
        if (player.Balance < dto.Amount)
            throw new InvalidOperationException(
                $"Insufficient balance. Available: {player.Balance}, requested: {dto.Amount}.");

        // Sprawdź dzienny limit wypłat
        var withdrawalToday = (await _uow.Transactions.GetTodaysTransactionsByPlayerAsync(playerId, ct))
            .Where(t => t.Type == TransactionType.Withdrawal)
            .Sum(t => t.Amount);
        if (withdrawalToday + dto.Amount > player.DailyWithdrawalLimit)
            throw new InvalidOperationException(
                $"Daily withdrawal limit exceeded. Limit: {player.DailyWithdrawalLimit}, " +
                $"already withdrawn today: {withdrawalToday}.");

        // Utwórz transakcję (zawsze wymaga zatwierdzenia)
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
            BalanceAfter = player.Balance - dto.Amount  // przewidywane
        };

        // Wykrywanie podejrzanych wzorców (prosty AML)
        var isSuspicious = await DetectSuspiciousActivity(playerId, dto.Amount, ct);
        if (isSuspicious)
        {
            transaction.IsFlagged = true;
            transaction.FlagReason = "Unusual withdrawal pattern detected. Manual review required.";
            _log.LogWarning("Suspicious withdrawal detected for player {PlayerId}: {Amount}",
                playerId, dto.Amount);
        }

        await _uow.Transactions.AddAsync(transaction, ct);
        await _uow.SaveChangesAsync(ct);

        // Audit log
        await CreateAuditLog("CreateWithdrawal", playerId, transaction.Id, ipAddress, ct);

        _log.LogInformation("Withdrawal of {Amount} for player {PlayerId} pending approval",
            dto.Amount, playerId);

        return _mapper.Map<TransactionDto>(transaction);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // APPROVE (zatwierdzanie przez operatora)
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto> ApproveAsync(
        Guid transactionId,
        Guid operatorId,
        string? notes,
        CancellationToken ct = default)
    {
        var transaction = await _uow.Transactions.GetByIdAsync(transactionId, ct)
            ?? throw new InvalidOperationException("Transaction not found.");

        if (transaction.Status != TransactionStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot approve transaction in status {transaction.Status}.");

        var player = await _uow.Players.GetByIdAsync(transaction.PlayerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        // Dla depozytów: dodaj do balansu
        if (transaction.Type == TransactionType.Deposit)
        {
            player.Balance += transaction.Amount;
        }
        // Dla wypłat: odejmij od balansu
        else if (transaction.Type == TransactionType.Withdrawal)
        {
            if (player.Balance < transaction.Amount)
                throw new InvalidOperationException(
                    "Insufficient balance. Player balance may have changed.");

            player.Balance -= transaction.Amount;
        }

        // Aktualizuj transakcję
        transaction.Status = TransactionStatus.Completed;
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.ApprovedById = operatorId;
        transaction.ApprovedAt = DateTime.UtcNow;
        transaction.BalanceAfter = player.Balance;

        if (!string.IsNullOrEmpty(notes))
            transaction.Description = (transaction.Description ?? "") + " [Operator notes: " + notes + "]";

        _uow.Transactions.Update(transaction);
        _uow.Players.Update(player);
        await _uow.SaveChangesAsync(ct);

        // Audit log
        await CreateAuditLog("ApproveTransaction", operatorId, transactionId, null, ct);

        // Powiadomienie do gracza
        await CreateNotification(
            player.Id,
            "Transaction Approved",
            $"Your {transaction.Type} of {transaction.Amount:C} has been approved.",
            transaction.Id,
            ct);

        _log.LogInformation("Transaction {TransactionId} approved by operator {OperatorId}",
            transactionId, operatorId);

        return _mapper.Map<TransactionDto>(transaction);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // REJECT (odrzucanie)
    // ═══════════════════════════════════════════════════════════════════════════
    public async Task<TransactionDto> RejectAsync(
        Guid transactionId,
        Guid operatorId,
        string reason,
        CancellationToken ct = default)
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

        _uow.Transactions.Update(transaction);
        await _uow.SaveChangesAsync(ct);

        // Audit log
        await CreateAuditLog("RejectTransaction", operatorId, transactionId, null, ct);

        // Powiadomienie do gracza
        await CreateNotification(
            transaction.PlayerId,
            "Transaction Rejected",
            $"Your {transaction.Type} of {transaction.Amount:C} was rejected. Reason: {reason}",
            transaction.Id,
            ct);

        _log.LogInformation("Transaction {TransactionId} rejected by operator {OperatorId}",
            transactionId, operatorId);

        return _mapper.Map<TransactionDto>(transaction);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // QUERIES (zapytania)
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

    public async Task<IEnumerable<TransactionDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var transactions = await _uow.Transactions.GetPendingTransactionsAsync(ct);
        return _mapper.Map<IEnumerable<TransactionDto>>(transactions);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS (metody pomocnicze)
    // ═══════════════════════════════════════════════════════════════════════════
    private async Task<bool> DetectSuspiciousActivity(Guid playerId, decimal amount, CancellationToken ct)
    {
        // Prosty algorytm AML - w produkcji byłby o wiele bardziej zaawansowany
        var recentTransactions = await _uow.Transactions.GetByPlayerIdAsync(playerId, ct);
        var last24h = recentTransactions
            .Where(t => t.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .ToList();

        // Red flag 1: więcej niż 5 transakcji w ciągu 24h
        if (last24h.Count >= 5)
            return true;

        // Red flag 2: wypłata > 10,000
        if (amount > 10000m)
            return true;

        // Red flag 3: suma wypłat w ciągu 24h > 20,000
        var totalWithdrawals = last24h
            .Where(t => t.Type == TransactionType.Withdrawal)
            .Sum(t => t.Amount);

        if (totalWithdrawals + amount > 20000m)
            return true;

        return false;
    }

    private async Task CreateAuditLog(
        string action,
        Guid userId,
        Guid? entityId,
        string? ipAddress,
        CancellationToken ct)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = "Transaction",
            EntityId = entityId,
            IpAddress = ipAddress,
            Details = $"{action} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
        };

        await _uow.AuditLogs.AddAsync(log, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task CreateNotification(
        Guid userId,
        string title,
        string message,
        Guid? relatedEntityId,
        CancellationToken ct)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = "TransactionUpdate",
            Title = title,
            Message = message,
            IsRead = false,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = "Transaction"
        };

        await _uow.Notifications.AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);
    }
}