# Tests

| Folder | What it checks |
| --- | --- |
| `DeltaComp.App.Tests` | Headless UI tests (no display needed): open an image or a data file, preview, compress and export the `.h` file. The exported data must equal what the core library produces with the same settings. Run with `dotnet test tests/DeltaComp.App.Tests`. |
| `DeltaComp.Bench` | Benchmark and byte-exact regression check of the compressor (below). |
| `Decoder` | Round trip through the unmodified microcontroller decoder (below). |
| `TestData` | Test images and data. |

# Benchmarks and regression check

A headless console tool that compresses a fixed set of test images and data with the
compressor from `DeltaComp.Core` and checks three things for every case:

1. **Speed and allocations**: wall-clock time and managed bytes allocated.
2. **Byte-exact output**: the SHA-256 of the compressed stream is compared with
   `baseline.json`, which was produced by a frozen copy of the original compressor
   (`DeltaComp.Bench/Reference/ReferenceCompressor.cs`). Optimizations that must not
   change the output are checked against it.
3. **Round trip** (optional): the stream is decoded by the unmodified microcontroller
   decoder from `/Decompression`, compiled for the PC, and every frame is compared with
   the source data.

## Running

```sh
# Current compressor, all cases
dotnet run -c Release --project tests/DeltaComp.Bench

# Skip the slow brute-force RGB888 cases, compare with the frozen reference side by side
dotnet run -c Release --project tests/DeltaComp.Bench -- --quick --engine both

# With the round-trip check (needs g++; on Windows use MSYS2/WSL)
tests/Decoder/build.sh
dotnet run -c Release --project tests/DeltaComp.Bench -- --decoder tests/Decoder/bin
```

Other options: `--filter logo,text`, `--repeat 3`, `--csv results.csv`.

By default the benchmark runs the **frame-by-frame** chain builder (the sprint 2 algorithm) and
compares it with `baseline.json`, produced by the frozen reference. The compressor's default,
the **optimal chain**, is checked with `--optimal` against `baseline-optimal.json`
(the size change against the frame-by-frame result is shown next to it).

- `--optimal --update-baseline` rewrites `baseline-optimal.json` (do it only when the optimal
  chain output is meant to change).
- `--engine reference --update-baseline` rewrites `baseline.json` from the frozen reference.
- `--auto` runs the automatic selection of block size field, delta option and square size
  (the "Auto" setting in the app) and reports the chosen settings.

The exit code is 1 if any output differs from its baseline or fails to decode.

## Test cases

Defined in `DeltaComp.Bench/BenchCase.cs`. Settings follow the sprint experiments:
grayscale 8-bit, text as 8-bit data, RGB565 and RGB888, with and without 2×2 squares,
all four delta options. Sources are in `tests/TestData/`.
