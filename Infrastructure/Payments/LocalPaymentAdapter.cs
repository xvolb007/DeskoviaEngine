using Deskovia.Application.Interfaces;

namespace Deskovia.Infrastructure.Payments;

public sealed class LocalPaymentAdapter : IPaymentAdapter
{
    public string Name => "LocalSimulator";
    public bool Simulate(bool succeed) => succeed;
}
