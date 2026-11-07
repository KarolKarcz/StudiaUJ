namespace KProblem;

/// <summary>Ocena rozwiązania: objętość, wartość, dopuszczalność i przekroczenie pojemności.</summary>
public sealed record Evaluation(double Volume, double Value, bool Feasible, double Excess)
{
    private const double Epsilon = 1e-6;

    public static Evaluation From(double volume, double value, double capacity)
    {
        bool feasible = volume <= capacity + Epsilon;
        return new Evaluation(volume, value, feasible, feasible ? 0 : volume - capacity);
    }
}

/// <summary>Stan rodzica zapisany w historii (próbkowany co kilka iteracji).</summary>
public sealed record Step(int Iteration, Evaluation Eval, bool Accepted);

/// <summary>Wynik działania algorytmu.</summary>
public sealed record RunResult(bool[] Bits, List<Step> History, int Iterations, int Accepted,
                               int BestIteration, Evaluation BestEval);

/// <summary>
/// Algorytm (1+1) z mutacją jednego bitu dla problemu plecakowego.
/// Rodzic i potomek to tablice bitowe (1 = przedmiot w plecaku).
/// </summary>
public static class KnapsackEA
{
    /// <summary>Maksymalna liczba punktów w historii (do wykresów).</summary>
    private const int MaxSamples = 2000;

    /// <summary>True, jeśli potomek ma zastąpić rodzica.</summary>
    public static bool IsBetter(Evaluation child, Evaluation parent)
    {
        if (child.Feasible && parent.Feasible)
            return child.Value >= parent.Value;   // oba dopuszczalne -> lepsza wartość

        if (child.Feasible != parent.Feasible)
            return child.Feasible;                // dopuszczalne zawsze wygrywa

        return child.Excess <= parent.Excess;     // oba niedopuszczalne -> bliżej dopuszczalności
    }

    public static RunResult Run(float[] volumes, float[] values, float capacity, int iterations, int? seed)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        int n = volumes.Length;

        // Start: losowe rozwiązanie rodzica
        var bits = new bool[n];
        double vol = 0, val = 0;
        for (int i = 0; i < n; i++)
        {
            bits[i] = rng.Next(2) == 1;
            if (bits[i])
            {
                vol += volumes[i];
                val += values[i];
            }
        }

        var parentEval = Evaluation.From(vol, val, capacity);
        int sampleEvery = Math.Max(1, iterations / MaxSamples);
        var history = new List<Step> { new(0, parentEval, true) };
        int accepted = 0;

        // Najlepsze dopuszczalne rozwiązanie i iteracja, na której po raz pierwszy je osiągnięto
        var bestEval = parentEval;
        int bestIteration = 0;

        for (int it = 1; it <= iterations; it++)
        {
            // Potomek = rodzic z jednym zmutowanym bitem. Zmieniamy tablicę w miejscu
            // (zamiast kopiować n elementów), a przy odrzuceniu cofamy mutację.
            int idx = rng.Next(n);
            double sign = bits[idx] ? -1 : 1;
            double dVol = sign * volumes[idx];
            double dVal = sign * values[idx];
            bits[idx] = !bits[idx];

            var childEval = Evaluation.From(vol + dVol, val + dVal, capacity);
            bool take = IsBetter(childEval, parentEval);

            if (take)
            {
                vol += dVol;
                val += dVal;
                parentEval = childEval;
                accepted++;

                if (childEval.Feasible && (!bestEval.Feasible || childEval.Value > bestEval.Value))
                {
                    bestEval = childEval;
                    bestIteration = it;
                }
            }
            else
            {
                bits[idx] = !bits[idx];   // cofnięcie mutacji
            }

            if (it % sampleEvery == 0 || it == iterations)
                history.Add(new Step(it, parentEval, take));
        }

        return new RunResult(bits, history, iterations, accepted, bestIteration, bestEval);
    }
}
