namespace Nexus.Services.Cart;

/// <summary>
/// Circuit-scoped notifier that keeps the header cart badge in sync with cart mutations.
/// Register as scoped so it lives for the duration of a Blazor Server connection.
/// </summary>
public sealed class CartState
{
    public int Count { get; private set; }

    public event Action? OnChange;

    public void SetCount(int count)
    {
        if (count < 0)
            count = 0;

        if (Count == count)
            return;

        Count = count;
        OnChange?.Invoke();
    }
}
