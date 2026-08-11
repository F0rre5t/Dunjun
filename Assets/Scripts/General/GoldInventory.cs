using System;

public static class GoldInventory
{
    public const int StartingAmount = 20;

    public static int Amount { get; private set; }

    public static event Action<int> GoldChanged;

    public static void Add(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Amount += amount;
        GoldChanged?.Invoke(Amount);
    }

    public static bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (Amount < amount)
        {
            return false;
        }

        Amount -= amount;
        GoldChanged?.Invoke(Amount);
        return true;
    }

    public static void Reset()
    {
        Amount = StartingAmount;
        GoldChanged?.Invoke(Amount);
    }
}
