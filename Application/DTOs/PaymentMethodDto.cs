

namespace Application.DTOs
{
    /// <summary>
    /// DTO for payment method
    /// </summary>
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
        public string? LogoUrl { get; set; }
    }
}
