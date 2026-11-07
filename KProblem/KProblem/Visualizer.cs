using System.Globalization;
using System.Text;

namespace KProblem;

/// <summary>Generuje dashboard SVG z przebiegu algorytmu.</summary>
public static class Visualizer
{
    private const int W = 1200;
    private const int H = 1010;

    private const string Bg = "#0b1120";
    private const string Card = "#111a2e";
    private const string Grid = "#1f2a44";
    private const string Slate = "#334155";
    private const string Empty = "#243247";
    private const string Txt = "#f8fafc";
    private const string TxtDim = "#94a3b8";
    private const string Blue = "#38bdf8";
    private const string Green = "#22c55e";
    private const string Amber = "#f59e0b";
    private const string Red = "#f43f5e";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly record struct Box(double X, double Y, double W, double H);

    public static void SaveSvg(string path, RunResult run, float[] volumes, float capacity)
    {
        var hist = run.History;
        var first = hist[0].Eval;
        var last = hist[^1].Eval;
        int n = volumes.Length;
        int iters = run.Iterations;
        int inBag = run.Bits.Count(b => b);
        double lo = volumes.Min();
        double hi = volumes.Max();
        double maxVal = hist.Max(s => s.Eval.Value);
        double maxVol = hist.Max(s => s.Eval.Volume);
        double maxExcess = hist.Max(s => s.Eval.Excess);
        int sampleEvery = hist.Count > 1 ? hist[1].Iteration : 1;

        var sb = new StringBuilder();
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Segoe UI, Arial, sans-serif">""");
        sb.AppendLine($"""<rect width="{W}" height="{H}" fill="{Bg}"/>""");

        // ---- nagłówek ----
        Text(sb, 24, 40, "Problem plecakowy · algorytm (1+1) z mutacją jednego bitu", 24, Txt, bold: true);
        Text(sb, 24, 64, $"{n} przedmiotów · pojemność {F(capacity)} · {iters.ToString(Inv)} iteracji · objętości losowe (float)", 13, TxtDim);

        // ---- karty KPI ----
        string pctFill = Pct(last.Volume / capacity);
        string pctItems = Pct((double)inBag / n);
        double acceptRate = (double)run.Accepted / iters;
        string statusText = last.Feasible ? "DOPUSZCZALNE" : "NIEDOPUSZCZALNE";
        string statusSub = last.Feasible ? $"zapas {F(capacity - last.Volume)}" : $"przekroczenie {F(last.Excess)}";
        var cards = new (string Label, string Value, string Sub, string Color, int Size)[]
        {
            ("Wartość rozwiązania", F(last.Value), $"start: {F(first.Value)}", Blue, 22),
            ("Objętość / pojemność", $"{F(last.Volume)} / {F(capacity)}", $"wypełnienie {pctFill}", Blue, 22),
            ("Przedmioty w plecaku", $"{inBag} / {n}", $"{pctItems} wszystkich", Blue, 22),
            ("Zaakceptowane mutacje", run.Accepted.ToString(Inv), $"{Pct(acceptRate)} iteracji", Blue, 22),
            ("Status", statusText, statusSub, last.Feasible ? Green : Red, 18),
        };
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            double x = 20 + i * 235;
            sb.AppendLine($"""<rect x="{x}" y="90" width="220" height="84" rx="10" fill="{Card}" stroke="{Slate}"/>""");
            Text(sb, x + 14, 112, c.Label, 12, TxtDim);
            Text(sb, x + 14, 141, c.Value, c.Size, c.Color, bold: true);
            Text(sb, x + 14, 162, c.Sub, 11, TxtDim);
        }

        // ---- wiersz A: wartość w czasie + mapa bitów ----
        var boxA1 = new Box(20, 195, 740, 270);
        var boxA2 = new Box(780, 195, 400, 270);

        var (xv, yv, _) = Axes(sb, boxA1, "Wartość rozwiązania w czasie (skala log)", Math.Max(maxVal * 1.05, 1), iters);
        Polyline(sb, hist.Select(s => $"{F(xv(s.Iteration))},{F(yv(s.Eval.Value))}"), Green, 2.2);
        var lastPt = hist[^1];
        Circle(sb, xv(lastPt.Iteration), yv(lastPt.Eval.Value), 4, Green);
        Text(sb, xv(lastPt.Iteration) - 8, yv(lastPt.Eval.Value) - 10, F(lastPt.Eval.Value), 12, Txt, anchor: "end");

        // znacznik najlepszego rozwiązania (pierwsza iteracja z tą wartością)
        double bx = xv(Math.Max(1, run.BestIteration)), by = yv(run.BestEval.Value);
        sb.AppendLine($"""<line x1="{F(bx)}" y1="{F(by)}" x2="{F(bx)}" y2="{F(yv(0))}" stroke="{Amber}" stroke-dasharray="4 3"/>""");
        Circle(sb, bx, by, 5, Amber);
        Text(sb, bx + 8, by + 18, $"najlepsze: krok {run.BestIteration.ToString(Inv)}", 12, Amber);

        Frame(sb, boxA2, "Mapa bitów rozwiązania (zielony = 1)");
        DrawBitmap(sb, boxA2, run.Bits);

        // ---- wiersz B: objętość vs pojemność + histogram ----
        var boxB1 = new Box(20, 485, 740, 250);
        var boxB2 = new Box(780, 485, 400, 250);

        var (xo, yo, _) = Axes(sb, boxB1, "Objętość rozwiązania rodzica vs pojemność", Math.Max(capacity, maxVol) * 1.08, iters);
        double baseY = yo(0);
        var volPts = hist.Select(s => $"{F(xo(s.Iteration))},{F(yo(s.Eval.Volume))}").ToList();
        Polygon(sb, [$"{F(xo(hist[0].Iteration))},{F(baseY)}", .. volPts, $"{F(xo(hist[^1].Iteration))},{F(baseY)}"], Blue, 0.18);
        Polyline(sb, volPts, Blue, 1.8);
        double capY = yo(capacity);
        sb.AppendLine($"""<line x1="{F(xo(1))}" y1="{F(capY)}" x2="{F(xo(iters))}" y2="{F(capY)}" stroke="{Red}" stroke-width="1.5" stroke-dasharray="6 4"/>""");
        Text(sb, xo(iters) - 6, capY - 6, $"pojemność {F(capacity)}", 12, Red, anchor: "end");

        DrawHistogram(sb, boxB2, volumes, run.Bits, lo, hi);

        // ---- wiersz C: przekroczenie + parametry ----
        var boxC1 = new Box(20, 755, 740, 240);
        var boxC2 = new Box(780, 755, 400, 240);

        var (xe, ye, _) = Axes(sb, boxC1, "Przekroczenie pojemności (0 = dopuszczalne)", Math.Max(1, maxExcess) * 1.1, iters);
        double baseE = ye(0);
        var exPts = hist.Select(s => $"{F(xe(s.Iteration))},{F(ye(s.Eval.Excess))}").ToList();
        Polygon(sb, [$"{F(xe(hist[0].Iteration))},{F(baseE)}", .. exPts, $"{F(xe(hist[^1].Iteration))},{F(baseE)}"], Amber, 0.25);
        Polyline(sb, exPts, Amber, 1.6);

        DrawParams(sb, boxC2, n, lo, hi, capacity, iters, sampleEvery, run.Accepted, run.BestIteration, first, last);

        sb.AppendLine("</svg>");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Dashboard zbiorczy dla wielu symulacji: średnie z pasmem min-max, rozkłady, statystyki, scenariusze brzegowe.</summary>
    public static void SaveMultiSvg(string path, List<RunResult> runs, List<SimRecord> recs, float capacity)
    {
        int count = recs.Count;
        int iters = runs[0].Iterations;
        int samples = runs.Min(r => r.History.Count);

        var sb = new StringBuilder();
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Segoe UI, Arial, sans-serif">""");
        sb.AppendLine($"""<rect width="{W}" height="{H}" fill="{Bg}"/>""");

        Text(sb, 24, 40, "Problem plecakowy · zbiorcze statystyki algorytmu (1+1)", 24, Txt, bold: true);
        Text(sb, 24, 64, $"{count} symulacji · pojemność {F(capacity)} · {iters.ToString(Inv)} iteracji · nowe losowe objętości w każdej symulacji", 13, TxtDim);

        // ---- agregaty ----
        var values = recs.Select(r => r.Value).ToArray();
        var (vMean, vMin, vMax, vStd) = Summary.Of(values);
        var (volMean, _, _, _) = Summary.Of(recs.Select(r => r.Volume));
        var (stepMean, _, _, stepStd) = Summary.Of(recs.Select(r => (double)r.BestIter));
        int feasibleCount = recs.Count(r => r.Feasible);
        double fillMean = recs.Average(r => r.Volume / capacity);
        double inBagMean = recs.Average(r => (double)r.InBag);

        // ---- karty KPI ----
        var cards = new (string Label, string Value, string Sub, string Color, int Size)[]
        {
            ("Średnia wartość", F(vMean), $"±{F(vStd)} (min {F(vMin)}, max {F(vMax)})", Blue, 22),
            ("Średnie wypełnienie", Pct(fillMean), $"objętość {F(volMean)} / {F(capacity)}", Blue, 22),
            ("Średnio w plecaku", F(inBagMean), "przedmiotów na symulację", Blue, 22),
            ("Średni krok najlepszego", F(stepMean), $"±{F(stepStd)}", Blue, 22),
            ("Dopuszczalne", $"{feasibleCount} / {count}", $"{Pct((double)feasibleCount / count)} symulacji", feasibleCount == count ? Green : Amber, 22),
        };
        for (int i = 0; i < cards.Length; i++)
        {
            var c = cards[i];
            double x = 20 + i * 235;
            sb.AppendLine($"""<rect x="{x}" y="90" width="220" height="84" rx="10" fill="{Card}" stroke="{Slate}"/>""");
            Text(sb, x + 14, 112, c.Label, 12, TxtDim);
            Text(sb, x + 14, 141, c.Value, c.Size, c.Color, bold: true);
            Text(sb, x + 14, 162, c.Sub, 11, TxtDim);
        }

        // ---- wiersz A: średnia wartość w czasie + rozkład wartości końcowych ----
        var boxA1 = new Box(20, 195, 740, 270);
        var boxA2 = new Box(780, 195, 400, 270);

        var iterAt = new int[samples];
        var meanV = new double[samples];
        var minV = new double[samples];
        var maxV = new double[samples];
        for (int i = 0; i < samples; i++)
        {
            var col = runs.Select(r => r.History[i].Eval.Value).ToArray();
            iterAt[i] = runs[0].History[i].Iteration;
            meanV[i] = col.Average();
            minV[i] = col.Min();
            maxV[i] = col.Max();
        }

        var (xa, ya, _) = Axes(sb, boxA1, "Średnia wartość w czasie (pasmo min-max)", Math.Max(maxV.Max() * 1.05, 1), iters);
        var upper = Enumerable.Range(0, samples).Select(i => $"{F(xa(iterAt[i]))},{F(ya(maxV[i]))}");
        var lowerRev = Enumerable.Range(0, samples).Reverse().Select(i => $"{F(xa(iterAt[i]))},{F(ya(minV[i]))}");
        Polygon(sb, upper.Concat(lowerRev), Blue, 0.18);
        Polyline(sb, Enumerable.Range(0, samples).Select(i => $"{F(xa(iterAt[i]))},{F(ya(meanV[i]))}"), Green, 2.2);
        Text(sb, xa(iterAt[samples - 1]) - 8, ya(meanV[samples - 1]) - 10, $"średnia {F(meanV[samples - 1])}", 12, Green, anchor: "end");

        Frame(sb, boxA2, "Rozkład wartości końcowej");
        var plotA = new Box(boxA2.X + 20, boxA2.Y + 50, boxA2.W - 40, boxA2.H - 80);
        const int bins = 15;
        var feasBins = new int[bins];
        var infBins = new int[bins];
        double span = vMax - vMin + 1e-9;
        foreach (var r in recs)
        {
            int bin = Math.Clamp((int)((r.Value - vMin) / span * bins), 0, bins - 1);
            if (r.Feasible) feasBins[bin]++;
            else infBins[bin]++;
        }
        int maxCnt = Enumerable.Range(0, bins).Max(k => feasBins[k] + infBins[k]);
        double binW = plotA.W / bins;
        double baseA = plotA.Y + plotA.H;
        for (int k = 0; k < bins; k++)
        {
            double hF = (double)feasBins[k] / maxCnt * plotA.H;
            double hI = (double)infBins[k] / maxCnt * plotA.H;
            double x = plotA.X + k * binW + 1;
            if (hF > 0)
                sb.AppendLine($"""<rect x="{F(x)}" y="{F(baseA - hF)}" width="{F(binW - 2)}" height="{F(hF)}" fill="{Green}"/>""");
            if (hI > 0)
                sb.AppendLine($"""<rect x="{F(x)}" y="{F(baseA - hF - hI)}" width="{F(binW - 2)}" height="{F(hI)}" fill="{Amber}"/>""");
        }
        sb.AppendLine($"""<line x1="{F(plotA.X)}" y1="{F(baseA)}" x2="{F(plotA.X + plotA.W)}" y2="{F(baseA)}" stroke="{Slate}"/>""");
        Text(sb, plotA.X, baseA + 16, F(vMin), 11, TxtDim);
        Text(sb, plotA.X + plotA.W, baseA + 16, F(vMax), 11, TxtDim, anchor: "end");
        double lx = boxA2.X + boxA2.W - 230, ly = boxA2.Y + boxA2.H - 26;
        sb.AppendLine($"""<rect x="{F(lx)}" y="{F(ly)}" width="10" height="10" fill="{Green}"/>""");
        Text(sb, lx + 16, ly + 10, "dopuszczalne", 11, TxtDim);
        sb.AppendLine($"""<rect x="{F(lx + 110)}" y="{F(ly)}" width="10" height="10" fill="{Amber}"/>""");
        Text(sb, lx + 126, ly + 10, "niedopuszczalne", 11, TxtDim);

        // ---- wiersz B: krok najlepszego w każdej symulacji + tabela statystyk ----
        var boxB1 = new Box(20, 485, 740, 250);
        var boxB2 = new Box(780, 485, 400, 250);

        Frame(sb, boxB1, "Krok najlepszego wyniku w każdej symulacji");
        var plotB = new Box(boxB1.X + 56, boxB1.Y + 40, boxB1.W - 72, boxB1.H - 70);
        double slot = plotB.W / count;
        double barW = Math.Max(1, slot * 0.75);
        double baseB = plotB.Y + plotB.H;
        for (int i = 0; i < count; i++)
        {
            double h = (double)recs[i].BestIter / iters * plotB.H;
            double x = plotB.X + i * slot + (slot - barW) / 2;
            string fill = recs[i].Feasible ? Green : Amber;
            sb.AppendLine($"""<rect x="{F(x)}" y="{F(baseB - h)}" width="{F(barW)}" height="{F(h)}" fill="{fill}"/>""");
        }
        sb.AppendLine($"""<line x1="{F(plotB.X)}" y1="{F(baseB)}" x2="{F(plotB.X + plotB.W)}" y2="{F(baseB)}" stroke="{Slate}"/>""");
        Text(sb, plotB.X - 6, baseB + 4, "0", 11, TxtDim, anchor: "end");
        Text(sb, plotB.X - 6, plotB.Y + 10, Human(iters), 11, TxtDim, anchor: "end");
        double meanY = baseB - stepMean / iters * plotB.H;
        sb.AppendLine($"""<line x1="{F(plotB.X)}" y1="{F(meanY)}" x2="{F(plotB.X + plotB.W)}" y2="{F(meanY)}" stroke="{Blue}" stroke-width="1.5" stroke-dasharray="6 4"/>""");
        Text(sb, plotB.X + plotB.W - 6, meanY - 6, $"średnio {F(stepMean)}", 12, Blue, anchor: "end");
        Text(sb, plotB.X, baseB + 16, "symulacja 1", 11, TxtDim);
        Text(sb, plotB.X + plotB.W, baseB + 16, $"symulacja {count.ToString(Inv)}", 11, TxtDim, anchor: "end");

        Frame(sb, boxB2, $"Statystyki ({count.ToString(Inv)} symulacji)");
        double tx = boxB2.X + 16, ty = boxB2.Y + 52;
        Text(sb, tx, ty, "Metryka", 11, TxtDim);
        Text(sb, boxB2.X + 210, ty, "Średnia", 11, TxtDim, anchor: "end");
        Text(sb, boxB2.X + 275, ty, "Min", 11, TxtDim, anchor: "end");
        Text(sb, boxB2.X + 340, ty, "Max", 11, TxtDim, anchor: "end");
        Text(sb, boxB2.X + 384, ty, "Odch.", 11, TxtDim, anchor: "end");
        var metrics = new (string Name, double[] Data)[]
        {
            ("Wartość", values),
            ("Objętość", recs.Select(r => r.Volume).ToArray()),
            ("Wypełnienie %", recs.Select(r => r.Volume / capacity * 100).ToArray()),
            ("W plecaku (szt.)", recs.Select(r => (double)r.InBag).ToArray()),
            ("Krok najlepszego", recs.Select(r => (double)r.BestIter).ToArray()),
            ("Zaakceptowane", recs.Select(r => (double)r.Accepted).ToArray()),
        };
        for (int i = 0; i < metrics.Length; i++)
        {
            var (m, mn, mx, sd) = Summary.Of(metrics[i].Data);
            double y = ty + 24 + i * 22;
            Text(sb, tx, y, metrics[i].Name, 12, Txt);
            Text(sb, boxB2.X + 210, y, F(m), 12, Txt, anchor: "end");
            Text(sb, boxB2.X + 275, y, F(mn), 12, Txt, anchor: "end");
            Text(sb, boxB2.X + 340, y, F(mx), 12, Txt, anchor: "end");
            Text(sb, boxB2.X + 384, y, F(sd), 12, Txt, anchor: "end");
        }

        // ---- wiersz C: scenariusze brzegowe + mapa bitów symulacji 1 ----
        var boxC1 = new Box(20, 755, 740, 240);
        var boxC2 = new Box(780, 755, 400, 240);

        Frame(sb, boxC1, "Scenariusze brzegowe (liczba symulacji)");
        const double barMax = 340;
        for (int i = 0; i < SimRecord.Edges.Length; i++)
        {
            var (label, test) = SimRecord.Edges[i];
            int cnt = recs.Count(test);
            double y = boxC1.Y + 52 + i * 30;
            double bw = barMax * cnt / count;
            Text(sb, boxC1.X + 16, y + 4, label, 12, Txt);
            sb.AppendLine($"""<rect x="{F(boxC1.X + 300)}" y="{F(y - 8)}" width="{F(bw)}" height="14" rx="3" fill="{(cnt > 0 ? Amber : Empty)}"/>""");
            Text(sb, boxC1.X + 300 + bw + 6, y + 4, $"{cnt.ToString(Inv)} ({Pct((double)cnt / count)})", 12, TxtDim);
        }

        Frame(sb, boxC2, "Mapa bitów - symulacja 1");
        DrawBitmap(sb, boxC2, runs[0].Bits);

        sb.AppendLine("</svg>");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Rysuje ramkę wykresu z osiami, siatką (log po iteracjach) i zwraca funkcje mapujące.</summary>
    private static (Func<double, double> X, Func<double, double> Y, Box Plot) Axes(
        StringBuilder sb, Box b, string title, double yMax, int iterations)
    {
        Frame(sb, b, title);
        yMax = Math.Max(yMax, 1e-9);
        var plot = new Box(b.X + 56, b.Y + 40, b.W - 72, b.H - 40 - 30);
        double logMax = Math.Log10(1 + iterations);

        double X(double it) => plot.X + Math.Log10(1 + it) / logMax * plot.W;
        double Y(double v) => plot.Y + plot.H - v / yMax * plot.H;

        for (int g = 0; g <= 4; g++)
        {
            double v = yMax * g / 4.0;
            double y = Y(v);
            sb.AppendLine($"""<line x1="{F(plot.X)}" y1="{F(y)}" x2="{F(plot.X + plot.W)}" y2="{F(y)}" stroke="{Grid}"/>""");
            Text(sb, plot.X - 6, y + 4, Human(v), 11, TxtDim, anchor: "end");
        }

        for (int p = 0; p <= 7; p++)
        {
            double it = Math.Pow(10, p) - 1;
            if (it > iterations) break;
            double x = X(it);
            sb.AppendLine($"""<line x1="{F(x)}" y1="{F(plot.Y)}" x2="{F(x)}" y2="{F(plot.Y + plot.H)}" stroke="{Grid}"/>""");
            Text(sb, x, plot.Y + plot.H + 16, Human(Math.Pow(10, p)), 11, TxtDim, anchor: "middle");
        }

        sb.AppendLine($"""<line x1="{F(plot.X)}" y1="{F(plot.Y + plot.H)}" x2="{F(plot.X + plot.W)}" y2="{F(plot.Y + plot.H)}" stroke="{Slate}"/>""");
        Text(sb, plot.X + plot.W / 2, b.Y + b.H - 8, "iteracja", 11, TxtDim, anchor: "middle");

        Func<double, double> fx = X;
        Func<double, double> fy = Y;
        return (fx, fy, plot);
    }

    private static void DrawBitmap(StringBuilder sb, Box b, bool[] bits)
    {
        var area = new Box(b.X + 14, b.Y + 40, b.W - 28, b.H - 56);
        int n = bits.Length;
        int cols = (int)Math.Ceiling(Math.Sqrt(n));
        int rows = (int)Math.Ceiling((double)n / cols);
        double cell = Math.Min(area.W / cols, area.H / rows);
        double ox = area.X + (area.W - cols * cell) / 2;
        double oy = area.Y + (area.H - rows * cell) / 2;
        double size = cell > 3 ? cell - 1 : cell;

        for (int i = 0; i < n; i++)
        {
            string fill = bits[i] ? Green : Empty;
            sb.AppendLine($"""<rect x="{F(ox + (i % cols) * cell)}" y="{F(oy + (i / cols) * cell)}" width="{F(size)}" height="{F(size)}" fill="{fill}"/>""");
        }
    }

    private static void DrawHistogram(StringBuilder sb, Box b, float[] volumes, bool[] bits,
                                      double lo, double hi)
    {
        Frame(sb, b, "Rozkład objętości przedmiotów");
        var plot = new Box(b.X + 20, b.Y + 50, b.W - 40, b.H - 50 - 30);
        const int bins = 20;
        var inBag = new int[bins];
        var outBag = new int[bins];

        for (int i = 0; i < volumes.Length; i++)
        {
            int bin = (int)((volumes[i] - lo) / (hi - lo + 1e-9) * bins);
            bin = Math.Clamp(bin, 0, bins - 1);
            if (bits[i]) inBag[bin]++;
            else outBag[bin]++;
        }

        int maxCount = Enumerable.Range(0, bins).Max(k => inBag[k] + outBag[k]);
        double bw = plot.W / bins;
        for (int k = 0; k < bins; k++)
        {
            double hIn = (double)inBag[k] / maxCount * plot.H;
            double hOut = (double)outBag[k] / maxCount * plot.H;
            double x = plot.X + k * bw + 1;
            double baseY = plot.Y + plot.H;
            if (hIn > 0)
                sb.AppendLine($"""<rect x="{F(x)}" y="{F(baseY - hIn)}" width="{F(bw - 2)}" height="{F(hIn)}" fill="{Green}"/>""");
            if (hOut > 0)
                sb.AppendLine($"""<rect x="{F(x)}" y="{F(baseY - hIn - hOut)}" width="{F(bw - 2)}" height="{F(hOut)}" fill="#475569"/>""");
        }

        sb.AppendLine($"""<line x1="{F(plot.X)}" y1="{F(plot.Y + plot.H)}" x2="{F(plot.X + plot.W)}" y2="{F(plot.Y + plot.H)}" stroke="{Slate}"/>""");
        Text(sb, plot.X, plot.Y + plot.H + 16, F(lo), 11, TxtDim);
        Text(sb, plot.X + plot.W, plot.Y + plot.H + 16, F(hi), 11, TxtDim, anchor: "end");

        // legenda
        double lx = b.X + b.W - 190, ly = b.Y + 16;
        sb.AppendLine($"""<rect x="{F(lx)}" y="{F(ly)}" width="10" height="10" fill="{Green}"/>""");
        Text(sb, lx + 16, ly + 10, "w plecaku", 11, TxtDim);
        sb.AppendLine($"""<rect x="{F(lx + 90)}" y="{F(ly)}" width="10" height="10" fill="#475569"/>""");
        Text(sb, lx + 106, ly + 10, "poza", 11, TxtDim);
    }

    private static void DrawParams(StringBuilder sb, Box b, int n, double lo, double hi, double capacity,
                                   int iters, int sampleEvery, int accepted, int bestIteration,
                                   Evaluation first, Evaluation last)
    {
        Frame(sb, b, "Parametry i przebieg");
        string startState = first.Feasible ? "dopuszczalne" : "niedopuszczalne";
        string endState = last.Feasible ? "dopuszczalne" : "niedopuszczalne";
        var lines = new[]
        {
            $"Przedmioty: {n}, objętości z [{F(lo)}, {F(hi)}]",
            $"Pojemność plecaka: {F(capacity)}",
            $"Iteracje: {iters.ToString(Inv)}, zapis co {sampleEvery.ToString(Inv)}",
            $"Zaakceptowane mutacje: {accepted.ToString(Inv)}",
            $"Najlepsze na kroku: {bestIteration.ToString(Inv)}",
            $"Start: obj. {F(first.Volume)}, wart. {F(first.Value)} ({startState})",
            $"Koniec: obj. {F(last.Volume)}, wart. {F(last.Value)} ({endState})",
            "Reguły: oba dopuszczalne -> większa wartość",
            "jedno dopuszczalne -> ono wygrywa",
            "oba niedopuszczalne -> mniejsze przekroczenie",
        };
        for (int i = 0; i < lines.Length; i++)
            Text(sb, b.X + 16, b.Y + 56 + i * 20, lines[i], 12, i >= 7 ? TxtDim : Txt);
    }

    private static void Frame(StringBuilder sb, Box b, string title)
    {
        sb.AppendLine($"""<rect x="{F(b.X)}" y="{F(b.Y)}" width="{F(b.W)}" height="{F(b.H)}" rx="10" fill="{Card}" stroke="{Slate}"/>""");
        Text(sb, b.X + 16, b.Y + 24, title, 14, Txt, bold: true);
    }

    private static void Text(StringBuilder sb, double x, double y, string s, int size, string fill,
                             bool bold = false, string anchor = "start")
    {
        string weight = bold ? " font-weight=\"bold\"" : "";
        sb.AppendLine($"""<text x="{F(x)}" y="{F(y)}" font-size="{size}" fill="{fill}" text-anchor="{anchor}"{weight}>{s}</text>""");
    }

    private static void Polyline(StringBuilder sb, IEnumerable<string> pts, string stroke, double width)
    {
        sb.AppendLine($"""<polyline points="{string.Join(" ", pts)}" fill="none" stroke="{stroke}" stroke-width="{F(width)}" stroke-linejoin="round"/>""");
    }

    private static void Polygon(StringBuilder sb, IEnumerable<string> pts, string fill, double opacity)
    {
        sb.AppendLine($"""<polygon points="{string.Join(" ", pts)}" fill="{fill}" fill-opacity="{F(opacity)}"/>""");
    }

    private static void Circle(StringBuilder sb, double cx, double cy, double r, string fill)
    {
        sb.AppendLine($"""<circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{fill}"/>""");
    }

    private static string Human(double v)
    {
        if (v >= 1e6) return (v / 1e6).ToString("0.#", Inv) + "M";
        if (v >= 1e3) return (v / 1e3).ToString("0.#", Inv) + "k";
        return F(v);
    }

    private static string Pct(double r) => (r * 100).ToString("0.0", Inv) + "%";

    private static string F(double v) => v.ToString("0.##", Inv);
}
