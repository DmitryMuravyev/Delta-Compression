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
dotnet run -c Release --project Benchmarks/DeltaComp.Bench

# Skip the slow brute-force RGB888 cases, compare with the frozen reference side by side
dotnet run -c Release --project Benchmarks/DeltaComp.Bench -- --quick --engine both

# With the round-trip check (needs g++; on Windows use MSYS2/WSL)
Benchmarks/Decoder/build.sh
dotnet run -c Release --project Benchmarks/DeltaComp.Bench -- --decoder Benchmarks/Decoder/bin
```

Other options: `--filter logo,text`, `--repeat 3`, `--csv results.csv`,
`--update-baseline` (only together with `--engine reference` or `both`).
The exit code is 1 if any output differs from the baseline or fails to decode.

## Test cases

Defined in `DeltaComp.Bench/BenchCase.cs`. Settings follow the sprint experiments:
grayscale 8-bit, text as 8-bit data, RGB565 and RGB888, with and without 2×2 squares,
all four delta options, brute-force and fast block search. Sources are in `TestData/`.
