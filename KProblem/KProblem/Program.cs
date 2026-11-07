using System.Globalization;
using KProblem;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var inv = CultureInfo.InvariantCulture;

int sims = AskSimulations();
float capacity = Config.Capacity;
var records = new List<SimRecord>(sims);
RunResult? firstRun = null;
var runs = new List<RunResult>(sims);
float[] firstVolumes = [];

for (int s = 0; s < sims; s++)
{
    // każda symulacja: nowe losowe objętości (kolejne ziarna) i nowe ziarno algorytmu
    int? volSeed = Config.VolumeSeed.HasValue ? Config.VolumeSeed.Value + s : null;
    int? algSeed = Config.Seed.HasValue ? Config.Seed.Value + s : null;
    float[] volumes = Config.RandomVolumes(Config.ItemCount, Config.MinVolume, Config.MaxVolume, volSeed);
    float[] values = Config.Values ?? volumes;   // brak wartości -> wartość = objętość

    var run = KnapsackEA.Run(volumes, values, capacity, Config.Iterations, algSeed);
    records.Add(SimRecord.From(run, volumes, capacity));
    runs.Add(run);

    if (s == 0)
    {
        firstRun = run;
        firstVolumes = volumes;
    }
}

if (sims == 1)
    PrintSingle(firstRun!, firstVolumes, capacity, records[0]);
else
    PrintStats(records, sims);

if (Config.SvgPath is { } svgPath && firstRun is not null)
{
    if (sims == 1)
        Visualizer.SaveSvg(svgPath, firstRun, firstVolumes, capacity);
    else
        Visualizer.SaveMultiSvg(svgPath, runs, records, capacity);
    Console.WriteLine($"Wizualizacja zapisana: {Path.GetFullPath(svgPath)}");
}

static int AskSimulations()
{
    Console.Write("Liczba symulacji (np. 1, 10, 100) [1]: ");
    string? input = Console.ReadLine();
    return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 1 ? n : 1;
}

void PrintSingle(RunResult run, float[] volumes, float cap, SimRecord r)
{
    var result = run.History[^1].Eval;
    Console.WriteLine(new string('=', 56));
    Console.WriteLine($"Przedmiotów         : {volumes.Length}");
    Console.WriteLine($"Pojemność plecaka   : {cap.ToString("0.00", inv)}");
    Console.WriteLine($"Iteracje            : {run.Iterations}");
    Console.WriteLine($"Zaakceptowane       : {run.Accepted}");
    Console.WriteLine($"Najlepsze na kroku  : {run.BestIteration} / {run.Iterations}");
    Console.WriteLine(new string('-', 56));
    Console.WriteLine($"Przedmiotów w plecaku: {r.InBag} / {volumes.Length}");
    Console.WriteLine($"Objętość rozwiązania : {result.Volume.ToString("0.00", inv)} / {cap.ToString("0.00", inv)}");
    Console.WriteLine($"Wartość rozwiązania  : {result.Value.ToString("0.00", inv)}");
    Console.WriteLine(result.Feasible
        ? "Status               : DOPUSZCZALNE"
        : $"Status               : NIEDOPUSZCZALNE (przekroczenie {result.Excess.ToString("0.00", inv)})");
    Console.WriteLine(new string('-', 56));
    Console.WriteLine("Scenariusze brzegowe:");
    var notes = r.Notes().ToList();
    if (notes.Count == 0)
        Console.WriteLine("  brak");
    foreach (var note in notes)
        Console.WriteLine($"  - {note}");
    Console.WriteLine(new string('=', 56));
}

void PrintStats(List<SimRecord> rs, int count)
{
    string seedInfo = Config.VolumeSeed.HasValue
        ? $"ziarno bazowe: {Config.VolumeSeed.Value}"
        : "ziarno losowe";

    Console.WriteLine(new string('=', 78));
    Console.WriteLine($"Symulacje: {count}  (nowe losowe objętości w każdej, {seedInfo})");
    Console.WriteLine(new string('-', 78));
    Console.WriteLine($"{"Metryka",-26}{"Średnia",12}{"Min",12}{"Max",12}{"Odch.",12}");

    void Row(string label, Func<SimRecord, double> f)
    {
        var (mean, min, max, std) = Summary.Of(rs.Select(f));
        Console.WriteLine($"{label,-26}{mean,12:0.00}{min,12:0.00}{max,12:0.00}{std,12:0.00}");
    }

    Row("Wartość rozwiązania", r => r.Value);
    Row("Objętość rozwiązania", r => r.Volume);
    Row("Wypełnienie [%]", r => r.Volume / Config.Capacity * 100);
    Row("Przedmioty w plecaku", r => r.InBag);
    Row("Krok najlepszego wyniku", r => r.BestIter);
    Row("Zaakceptowane mutacje", r => r.Accepted);

    Console.WriteLine(new string('-', 78));
    Console.WriteLine($"Dopuszczalne rozwiązania: {rs.Count(r => r.Feasible)} / {count}");
    Console.WriteLine("Scenariusze brzegowe (liczba symulacji):");
    foreach (var (label, test) in SimRecord.Edges)
        Console.WriteLine($"  {label,-36}{rs.Count(test),6}");
    Console.WriteLine(new string('=', 78));
}
