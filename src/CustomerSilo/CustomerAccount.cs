using Orleans;
using Orleans.EventSourcing;

namespace CustomerSilo;

public interface ICustomerAccount : IGrainWithGuidKey
{
    Task Deposit(decimal amount);

    Task<decimal> GetBalance();

    Task Withdrawn(decimal amount);
}

public class CustomerAccountGrain : JournaledGrain<CustomerAccount>, ICustomerAccount
{
    public Task<decimal> GetBalance()
    {
        return Task.FromResult(State.Amount);
    }

    public Task Deposit(decimal amount)
    {
        RaiseEvent(new AmountDeposited() { Amount = amount });
        return ConfirmEvents();
    }

    public Task Withdrawn(decimal amount)
    {
        RaiseEvent(new AmountWithdrawed() { Amount = amount });
        return ConfirmEvents();
    }
}

[GenerateSerializer]
public class CustomerAccount
{
    [Id(0)]
    public decimal Amount { get; private set; }

    public void Apply(AmountDeposited @event)
    {
        Amount += @event.Amount;
    }

    public void Apply(AmountWithdrawed @event)
    {
        Amount -= @event.Amount;
    }
}

public readonly record struct AmountWithdrawed(decimal Amount);

public readonly record struct AmountDeposited(decimal Amount);
