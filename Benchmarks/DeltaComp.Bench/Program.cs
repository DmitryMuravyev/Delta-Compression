using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DeltaComp;
using DeltaComp.Bench;

// DeltaComp benchmark and regression check.
//
//   dotnet run -c Release --project Benchmarks/DeltaComp.Bench -- [options]
//
// Options:
//   --filter a,b        run only cases whose name contains any of the given substrings
//   --quick             skip the slow cases (brute force on RGB888)
//   --engine E          current (default) | reference | both
//   --repeat N          run every case N times and report the best time (default 1)
//   --decoder DIR       round-trip check with the PC build of the C++ decoder (see Benchmarks/Decoder/build.sh)
//   --update-baseline   write the output sizes and hashes of the reference engine to Benchmarks/baseline.json
//   --csv FILE          also write the results as CSV
//
// Exit code is 1 if any output differs from the baseline or a round trip fails.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

string[] filters = [];
bool quick = false, updateBaseline = false;
string engineArg = "current";
int repeat = 1;
string? decoderDir = null, csvPath = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--filter": filters = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries); break;
        case "--quick": quick = true; break;
        case "--engine": engineArg = args[++i].ToLowerInvariant(); break;
        case "--repeat": repeat = int.Parse(args[++i]); break;
        case "--decoder": decoderDir = args[++i]; break;
        case "--update-baseline": updateBaseline = true; break;
        case "--csv": csvPath = args[++i]; break;
        default: Console.Error.WriteLine($"Unknown option: {args[i]}"); return 2;
    }
}

Engine[] engines = engineArg switch
{
    "current" => [Engine.Current],
    "reference" => [Engine.Reference],
    "both" => [Engine.Reference, Engine.Current],
    _ => throw new ArgumentException($"Unknown engine: {engineArg}")
};
if (updateBaseline && !engines.Contains(Engine.Reference))
{
    Console.Error.WriteLine("--update-baseline needs --engine reference or both: the baseline always comes from the frozen reference.");
    return 2;
}

string benchDir = FindBenchmarksDir();
string dataDir = Path.Combine(benchDir, "TestData");
string baselinePath = Path.Combine(benchDir, "baseline.json");
var baseline = File.Exists(baselinePath)
    ? JsonSerializer.Deserialize<Dictionary<string, BaselineEntry>>(File.ReadAllText(baselinePath))!
    : new Dictionary<string, BaselineEntry>();

var cases = BenchCase.All
    .Where(c => filters.Length == 0 || filters.Any(f => c.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
    .Where(c => !quick || !c.Slow)
    .ToList();

Console.WriteLine($"DeltaComp benchmark: {cases.Count} case(s), engines: {string.Join(", ", engines)}, " +
                  $"repeat: {repeat}, CPUs: {Environment.ProcessorCount}, .NET {Environment.Version}");
Console.WriteLine();
Console.WriteLine($"{"case",-22} {"engine",-9} {"bytes",8} {"eff %",7} {"time ms",9} {"alloc MB",9}  {"sha256",-12} {"baseline",-8} {"decode",-6}");
Console.WriteLine(new string('-', 100));

var csv = new StringBuilder("case,engine,bytes,efficiency,time_ms,alloc_mb,sha256,baseline,decode\n");
bool failed = false;

foreach (BenchCase c in cases)
{
    LoadedSource source = LoadedSource.Load(c, dataDir);
    ulong[]? expectedFrames = decoderDir != null ? Runner.ExpectedFrames(c, source) : null;

    foreach (Engine engine in engines)
    {
        RunResult? best = null;
        for (int r = 0; r < repeat; r++)
        {
            GC.Collect();
            RunResult result = Runner.Run(c, source, engine);
            if ((best != null) && (result.Sha256 != best.Sha256))
                throw new InvalidOperationException($"{c.Name}: non-deterministic output between repeats");
            if ((best == null) || (result.Milliseconds < best.Milliseconds)) best = result;
        }
        RunResult res = best!;

        string baselineStatus = "-";
        if (baseline.TryGetValue(c.Name, out BaselineEntry? entry))
        {
            baselineStatus = entry.Sha256 == res.Sha256 ? "same" : "DIFF";
            if (baselineStatus == "DIFF") failed = true;
        }

        string decodeStatus = "-";
        if (expectedFrames != null)
        {
            decodeStatus = RoundTrip(decoderDir!, c, res, expectedFrames) ? "ok" : "FAIL";
            if (decodeStatus == "FAIL") failed = true;
        }

        if (updateBaseline && engine == Engine.Reference)
        {
            baseline[c.Name] = new BaselineEntry(res.Output.Length, res.Sha256, Math.Round(res.Efficiency * 100, 2));
        }

        Console.WriteLine($"{c.Name,-22} {engine,-9} {res.Output.Length,8} {res.Efficiency * 100,7:F2} {res.Milliseconds,9:F0} " +
                          $"{res.AllocatedBytes / 1048576.0,9:F1}  {res.Sha256[..12],-12} {baselineStatus,-8} {decodeStatus,-6}");
        csv.AppendLine($"{c.Name},{engine},{res.Output.Length},{res.Efficiency * 100:F2},{res.Milliseconds:F1}," +
                       $"{res.AllocatedBytes / 1048576.0:F1},{res.Sha256},{baselineStatus},{decodeStatus}");
    }
}

if (updateBaseline)
{
    var sorted = baseline.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value);
    File.WriteAllText(baselinePath, JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine($"\nBaseline written to {baselinePath}");
}
if (csvPath != null) File.WriteAllText(csvPath, csv.ToString());

Console.WriteLine(failed ? "\nFAILED: some outputs differ from the baseline or did not decode correctly." : "\nAll checks passed.");
return failed ? 1 : 0;


static string FindBenchmarksDir()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
    {
        string candidate = Path.Combine(dir.FullName, "Benchmarks", "TestData");
        if (Directory.Exists(candidate)) return Path.Combine(dir.FullName, "Benchmarks");
    }
    throw new DirectoryNotFoundException("Benchmarks/TestData not found above " + AppContext.BaseDirectory);
}


static bool RoundTrip(string decoderDir, BenchCase c, RunResult res, ulong[] expected)
{
    string mode = c.Delta switch
    {
        DeltaOption.FixedWindowOnly => "fixed-window-only",
        DeltaOption.AdaptiveFloatingWindowOnly => "adaptive-floating-window-only",
        DeltaOption.FixedWindowFirst => "fixed-window-first",
        _ => "adaptive-floating-window-first"
    };
    string exe = Path.Combine(decoderDir, "roundtrip-" + mode + (OperatingSystem.IsWindows() ? ".exe" : ""));
    string input = Path.GetTempFileName(), output = Path.GetTempFileName();
    try
    {
        File.WriteAllBytes(input, res.Output);
        var psi = new ProcessStartInfo(exe)
        {
            ArgumentList =
            {
                input, res.TotalFrames.ToString(), c.BlockSizeBits.ToString(),
                string.Join(",", res.BitsPerChannel), string.Join(",", res.BitsPerMethodDeclaration), output
            },
            RedirectStandardError = true
        };
        using var process = Process.Start(psi)!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) { Console.Error.WriteLine($"{c.Name}: decoder failed: {error}"); return false; }

        byte[] raw = File.ReadAllBytes(output);
        if (raw.Length != expected.Length * 8) { Console.Error.WriteLine($"{c.Name}: decoded {raw.Length / 8} frames, expected {expected.Length}"); return false; }
        for (int i = 0; i < expected.Length; i++)
        {
            ulong frame = BitConverter.ToUInt64(raw, i * 8);
            if (frame != expected[i]) { Console.Error.WriteLine($"{c.Name}: frame {i} decoded as {frame}, expected {expected[i]}"); return false; }
        }
        return true;
    }
    finally
    {
        File.Delete(input);
        File.Delete(output);
    }
}


record BaselineEntry(int Bytes, string Sha256, double Efficiency);
