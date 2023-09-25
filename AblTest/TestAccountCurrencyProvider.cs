using QuantConnect;
using QuantConnect.Interfaces;

namespace AblTest;

internal class TestAccountCurrencyProvider : IAccountCurrencyProvider
{
    public string AccountCurrency { get; }

    public TestAccountCurrencyProvider() : this(Currencies.USD) { }

    public TestAccountCurrencyProvider(string accountCurrency)
    {
        AccountCurrency = accountCurrency;
    }
}