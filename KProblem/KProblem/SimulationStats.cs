namespace KProblem;

/// <summary>Wynik pojedynczej symulacji wraz z flagami scenariuszy brzegowych.</summary>
public sealed record SimRecord(
    double Value,
    double Volume,
    bool Feasible,
    int InBag,
    int BestIter,
    int Accepted,
    bool StartInfeasible,
    bool AllFit,
    bool NoneFit,
    bool NeverFeasible,
    bool NoImprovement,
    bool NoAccepted)
{
    /// <summary>Scenariusze brzegowe: etykieta i warunek.</summary>
    public static readonly (string Label, Func<SimRecord, bool> Test)[] Edges =
    {
        ("start niedopuszczalny", r => r.StartInfeasible),
        ("wszystkie przedmioty się mieszczą", r => r.AllFit),
        ("żaden przedmiot się nie mieści", r => r.NoneFit),
        ("wynik nigdy nie dopuszczalny", r => r.NeverFeasible),
        ("brak poprawy względem startu", r => r.NoImprovement),
        ("brak zaakceptowanych mutacji", r => r.NoAccepted),
    };

    public IEnumerable<string> Notes() => Edges.Where(e => e.Test(this)).Select(e => e.Label);

    public static SimRecord From(RunResult run, float[] volumes, float capacity)
    {
        var final = run.History[^1].Eval;
        double total = volumes.Sum(v => (double)v);
        return new SimRecord(
            Value: final.Value,
            Volume: final.Volume,
            Feasible: final.Feasible,
            InBag: run.Bits.Count(b => b),
            BestIter: run.BestIteration,
            Accepted: run.Accepted,
            StartInfeasible: !run.History[0].Eval.Feasible,
            AllFit: total <= capacity,
            NoneFit: volumes.Min() > capacity,
            NeverFeasible: !final.Feasible,
            NoImprovement: run.BestIteration == 0,
            NoAccepted: run.Accepted == 0);
    }
}

public static class Summary
{
    public static (double Mean, double Min, double Max, double Std) Of(IEnumerable<double> xs)
    {
        var a = xs.ToArray();
        double mean = a.Average();
        double std = Math.Sqrt(a.Sum(x => (x - mean) * (x - mean)) / a.Length);
        return (mean, a.Min(), a.Max(), std);
    }
}
