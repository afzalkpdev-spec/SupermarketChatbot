using SupermarketBot.Data;
using SupermarketBot.Models;

namespace SupermarketBot.Services;

public class PaymentService
{
    private readonly AppDbContext _db;

    public PaymentService(AppDbContext db)
    {
        _db = db;
    }

    /// MVP: only cash-on-delivery is wired up (status stays "pending" until
    /// collected). Add card/WhatsApp Pay by calling a gateway here and
    /// setting status "paid" on success, "failed" on failure.
    public async Task<Payment> RecordCashOnDeliveryAsync(long orderId, decimal amount, string currency)
    {
        var payment = new Payment
        {
            OrderId = orderId,
            Method = "cash_on_delivery",
            Amount = amount,
            Currency = currency,
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return payment;
    }
}