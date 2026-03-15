using Application.DTOs;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Financial summary: deposits, withdrawals, net flow, and a period-by-period breakdown.
    /// GroupBy accepts Daily | Weekly | Monthly (default: Daily).
    /// </summary>
    [HttpGet("financial-summary")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<ActionResult<FinancialSummaryReportDto>> GetFinancialSummary(
        [FromQuery] ReportFilterDto filter, CancellationToken ct)
    {
        var report = await _reportService.GetFinancialSummaryAsync(filter, ct);
        return Ok(report);
    }

    /// <summary>
    /// Player activity: counts by status, KYC, new registrations, and top 10 players by volume.
    /// </summary>
    [HttpGet("players")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<PlayerActivityReportDto>> GetPlayerActivity(
        [FromQuery] ReportFilterDto filter, CancellationToken ct)
    {
        var report = await _reportService.GetPlayerActivityReportAsync(filter, ct);
        return Ok(report);
    }

    /// <summary>
    /// Payment method usage: transaction count, volume, and average per method.
    /// </summary>
    [HttpGet("payment-methods")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<ActionResult<PaymentMethodReportDto>> GetPaymentMethodReport(
        [FromQuery] ReportFilterDto filter, CancellationToken ct)
    {
        var report = await _reportService.GetPaymentMethodReportAsync(filter, ct);
        return Ok(report);
    }

    /// <summary>
    /// Export filtered transactions as CSV (max 10 000 rows).
    /// </summary>
    [HttpGet("export/transactions")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> ExportTransactions(
        [FromQuery] TransactionFilterDto filter, CancellationToken ct)
    {
        var csv = await _reportService.ExportTransactionsCsvAsync(filter, ct);
        var filename = $"transactions_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(csv, "text/csv", filename);
    }

    /// <summary>
    /// Export all active players as CSV.
    /// </summary>
    [HttpGet("export/players")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ExportPlayers(CancellationToken ct)
    {
        var csv = await _reportService.ExportPlayersCsvAsync(ct);
        var filename = $"players_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(csv, "text/csv", filename);
    }
}
