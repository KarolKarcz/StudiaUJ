namespace KProblem;

/// <summary>
/// Konfiguracja problemu plecakowego - wszystkie parametry w jednym miejscu.
/// </summary>
public static class Config
{
    /// <summary>Liczba przedmiotów.</summary>
    public const int ItemCount = 100;

    /// <summary>Zakres losowania objętości przedmiotów.</summary>
    public const float MinVolume = 10.0f;
    public const float MaxVolume = 90.0f;

    /// <summary>Ziarno losowania objętości; null = za każdym razem inaczej.</summary>
    public static readonly int? VolumeSeed = 7;

    /// <summary>Objętości przedmiotów - losowane z zakresu [MinVolume, MaxVolume].</summary>
    public static readonly float[] Volumes = RandomVolumes(ItemCount, MinVolume, MaxVolume, VolumeSeed);

    /// <summary>Wartości przedmiotów; null = wartość równa objętości.</summary>
    public static readonly float[]? Values = null;

    /// <summary>Pojemność plecaka.</summary>
    public const float Capacity = 2500f;

    /// <summary>Liczba iteracji algorytmu.</summary>
    public const int Iterations = 200;

    /// <summary>Ziarno losowości algorytmu; null = za każdym razem inaczej.</summary>
    public static readonly int? Seed = null;

    /// <summary>Ścieżka pliku SVG z wizualizacją; null = nie zapisuj.</summary>
    public static readonly string? SvgPath = "knapsack_result.svg";

    /// <summary>Losuje <paramref name="count"/> wartości float z przedziału [min, max], zaokrąglonych do 2 miejsc.</summary>
    public static float[] RandomVolumes(int count, float min, float max, int? seed)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var result = new float[count];
        for (int i = 0; i < count; i++)
            result[i] = MathF.Round(min + (float)rng.NextDouble() * (max - min), 2);
        return result;
    }
}
