using Application.DTOs;

namespace Application.Services.Interfaces;

public interface IReportService
{
    Task<FinancialSummaryReportDto> GetFinancialSummaryAsync(ReportFilterDto filter, CancellationToken ct = default);
    Task<PlayerActivityReportDto> GetPlayerActivityReportAsync(ReportFilterDto filter, CancellationToken ct = default);
    Task<PaymentMethodReportDto> GetPaymentMethodReportAsync(ReportFilterDto filter, CancellationToken ct = default);
    Task<byte[]> ExportTransactionsCsvAsync(TransactionFilterDto filter, CancellationToken ct = default);
    Task<byte[]> ExportPlayersCsvAsync(CancellationToken ct = default);
}
