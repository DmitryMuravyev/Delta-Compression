# DeltaComp 2.0: what changed, how it works and why

DeltaComp 2.0 makes files 0–24% smaller and compresses about 19× faster than version 1. The compressed stream format did not change, so every file decodes with the same microcontroller decoder from [`Decompression/`](../Decompression), and the decoder's flash and RAM use is exactly what it was.

The changes come in five layers:

1. **Speed refactor.** Same output byte for byte, 3.3× faster, memory allocated per run down from tens of GB to tens of MB.
2. **All window lengths in one scan.** The length of every window width is found in a single pass over the data.
3. **Exact block search.** The best common block is found by a formula instead of trying every method combination. Same output; Logo RGB888 went from 17.5 s to 0.19 s, so the Fast search mode was no longer needed.
4. **Optimal chain of blocks.** The chain is chosen to minimize the total file size, not each block's efficiency. This is provably the smallest file the decoder can read for the chosen settings, and it is 0–5% smaller than in version 1.
5. **Auto settings.** The block size field, Δ option and square size are searched per image, giving up to 24% smaller files.

The main reason for the gains: the old algorithm optimized each block locally, and a series of locally best blocks is not the smallest file. The new one minimizes the total directly.

The mathematics behind all of this is in the [Sprint 4 deck](Sprint_4_Mathematical_foundation.pdf).

## Project structure

| Part | What it is |
| --- | --- |
| `src/DeltaComp.Core` | The UI-free compression library (`net10.0`): compressor, channels, data sources, PNG decoder, previews and `.h` export. |
| `src/DeltaComp.App` | The cross-platform desktop app (Avalonia), Windows and Linux. |
| `tests/DeltaComp.Bench` | Console harness with 16 test cases. It times each case and compares the output with a frozen copy of the original compressor and with saved SHA-256 baselines. |
| `tests/Decoder` | Builds the unmodified `Decompression` sources with g++ in all four Δ-option variants and decodes every output back to the source frames. |
| `tests/DeltaComp.App.Tests` | Headless UI tests: open an image or a file, preview, compress and export, without a display. |
| `tests/TestData` | Test images and the MIT/Lorem Ipsum text. |
| `docs` | The sprint decks (1–4) and this document. |

## 1. Speed refactor

The refactor made compression 3.3× faster with byte-identical output. Allocated memory for Logo RGB888 fell from 41 GB to 28 MB.

- **Frame cache.** The source is read once into a `uint[channel][frame]` array, already in compression order with squares applied. Before, every length check called the channel's `GetTargetValueForFrame` and recomputed the square mapping, for every frame, method and start position. That was the biggest cost.
- **Span and stackalloc.** Per-block arrays of methods, lengths, costs and initial values live on the stack (up to 256 methods, then they fall back to the heap). Nothing is allocated per frame, so the garbage collector has almost nothing to do.
- **Multithreading.** The legacy best-block pass uses one long-running worker per CPU core. Each worker takes the next frame with `Interlocked.Increment`, instead of one task per frame.
- **No UI in the compressor.** Progress goes through a plain delegate; the app marshals it to the UI thread. This is what makes the Core library usable from Avalonia or a console.

**Why byte-identical matters:** it proves the refactor changed nothing in the algorithm. The harness checks the SHA-256 of every output against the original compressor on all 16 cases.

## 2. All window lengths in one scan

One scan of the data now finds how long every window width lasts from a given frame, instead of one scan per width. This works because a wider window always lasts at least as long as a narrower one.

**Fixed window.** A block fits width *w* when the range of its frames satisfies

$$\max_{p \le i < p+l} x_i - \min_{p \le i < p+l} x_i \le 2^w - 1$$

The range only grows as the scan moves forward. When it passes 2^w − 1, width *w* ends there, and the scan goes on for *w* + 1. So the lengths come out in order *w* = 0, 1, 2, … in one pass.

**Adaptive floating window.** The window around the previous frame *y* starts at

$$s_w(y) = \min\left(\max\left(y - 2^{w-1},\ 0\right),\ 2^k - 2^w\right)$$

For the same *y*, the window of width 2^(w+1) contains the window of width 2^w, including when clamping hits 0 or the top. This was proved by cases and also checked for every *y* and *w* with *k* ≤ 12. The previous frame is the source value, which is the same for every *w* because compression is lossless. So a frame that breaks width *w* + 1 also breaks *w*, and the narrowest width that still fits only moves up during the scan.

**Why it matters:** method lengths are the innermost loop of every search. The cost per channel drops from O(k · 2^b) to O(2^b + k).

## 3. Exact best-block search

The best common block is now computed by a formula in O((ΣM)²) steps instead of trying all ∏M method combinations, with the same result.

**Before.** For every combination (m₀, …, m_{C−1}) the block length was the shortest of the chosen methods' lengths, and the block with the best efficiency won. RGB888 with two Δ options is 16³ = 4096 combinations per frame.

**Now.** Combinations are grouped by their length. A block length can only be one of the methods' own lengths, so there are at most ΣM candidate lengths. For a candidate length *l*, each channel *c* gets two numbers:

$$A_c(l) = \min_{m:\ L_{c,m} \ge l} \mathrm{cost}_c(m, l), \qquad B_c(l) = \min_{m:\ L_{c,m} = l} \mathrm{cost}_c(m, l)$$

*A* is the cheapest method that lasts at least *l* frames; *B* is the cheapest that ends exactly at *l*. A block is exactly *l* frames long when every channel lasts at least *l* and at least one channel ends at *l*. So one channel takes its *B*, the rest take their *A*, and the best choice of that channel is the one with the smallest extra cost:

$$\mathrm{Bits}^*(l) = H + \sum_c A_c(l) + \min_c \left(B_c(l) - A_c(l)\right)$$

Then the efficiency 1 − Bits\*(l) / (l · F) is compared across the candidate lengths.

**Tie-breaking.** The old loop kept the first combination in its order, which is the lexicographically smallest (m₀, …, m_{C−1}). The new code rebuilds exactly that one, channel by channel, picking the smallest method that still lets the remaining channels reach the minimum. That is why the output stays byte-identical.

**Why:** the Fast mode existed only because brute force was slow. With the exact search just as fast, the Fast mode was removed.

## 4. Optimal chain of blocks

The chain is now built by dynamic programming to minimize the total file size. For the chosen block size field, Δ option and squares, no stream the decoder can read is smaller. It is 0–5% smaller than version 1 at the same settings.

**Why the old way falls short.** Frame-by-frame analysis and "best block at each step" maximize the efficiency of each block. A very efficient long block can leave a tail that compresses badly, and a short, weak block can open the way for a much better one. Example with *k* = 8, *b* = 3 and frames 97 97 117 117 117 116 116 117 117 117: the greedy chain takes 70 bits, the optimal one 38 bits (two equal frames, then 8 frames in a 1-bit Fixed window).

**How it works.**

1. **Cheapest block of each length.** For a start frame *p* and length *l*, each channel independently takes its cheapest method that lasts at least *l* frames. Any method that works for *L* frames also works for every shorter prefix, and within a family the narrowest window is cheapest:

   $$\mathrm{Bits}(p,l) = H + \sum_c \min\left(l\,k_c,\ k_c + l\,w^F_c(l),\ k_c + (l-1)\,w^A_c(l)\right)$$

2. **Recurrence.** Best(p) is the smallest size of frames *p* to *N* − 1:

   $$\mathrm{Best}(N) = 0, \qquad \mathrm{Best}(p) = \min_{1 \le l \le \min(2^b,\, N-p)} \left(\mathrm{Bits}(p,l) + \mathrm{Best}(p+l)\right)$$

3. **Backward, then forward.** Best is computed from the last frame back to frame 0, storing the best length at each position. Then the chain is read from frame 0 forward and written with the usual `CompressBlock`.

**Why it is optimal.** Every stream the decoder accepts is a sequence of blocks, and each block is at least Bits(p, l). By induction from the end, any stream is at least Best(0), and the restored chain is exactly Best(0).

**Making it fast enough.**

- **Pruning.** Bits(p, l) never decreases as *l* grows, and Best is never negative. So once Bits(p, l) alone reaches the best total found so far, no longer block can win, and the scan stops. Usually only a few dozen lengths are checked, even with *b* = 15.
- **Linear segments.** Over a stretch of lengths each channel keeps the same cheapest method, so the block cost is *a* + *s* · *l*. The inner loop just adds the slope per length.
- **Incremental lengths.** Moving backward, the lengths at *p* come from those at *p* + 1. For Fixed windows the block end only moves left, with min and max kept in monotonic deques. For Adaptive windows it remembers where the next break is. This is O(Σk) per frame instead of rescanning.

A whole 240 × 240 image takes about 0.1 s; the 32 KB text with *b* = 15 takes about 0.9 s. On the PC side it needs 12 bytes per frame for the two arrays. The decoder is not affected.

## 5. Auto settings

The **Settings** combo (Manual or Auto, Auto by default) picks the block size field, Δ option and square size that give the smallest file. Then it shows the chosen values in their combos and compresses as usual.

**How Auto searches.** The optimal chain gives the exact size in bits without writing the stream, so settings can be compared cheaply:

- Every pair of square width and Δ option is tried, in parallel. Square widths are those that divide both the image width and height; non-image data uses no squares.
- For each pair, the block size field goes up from the smallest option and stops after it gets worse 3 times in a row. This early stop is the only heuristic step.
- The smallest result wins; on a tie, the first one in trial order.

It takes 1–13 s per image on 4 cores, and the predicted size always matched the real output.

**Why the full grid.** Choosing the Δ option first and the squares second was up to 36% worse (gradient-gray), because squares and Δ option interact.

**Important for the decoder.** Auto may choose any Δ option, so the decoder must be built with the matching `#define` in `Decompression.h`: `DECOMPRESSION_FIXED_WINDOW_ONLY`, `DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_ONLY`, `DECOMPRESSION_FIXED_WINDOW_FIRST` or `DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST`. Squares need `DECOMPRESSION_USE_SQUARES` and the same square width. The exported `.h` file lists the chosen settings.

## 6. Cross-platform app

The WPF app was replaced with an [Avalonia](https://avaloniaui.net/) app that runs on Windows and Linux with the same layout and behavior.

- **Images.** PNG files are decoded by DeltaComp's own decoder, which keeps the exact bit depth of every component: 1/2/4/8/16-bit grayscale, 8/16-bit RGB and RGBA, palette images (expanded to RGB, plus alpha if the palette has transparency) and Adam7 interlacing. Other formats (JPEG, BMP, GIF, WebP, ICO) are decoded by the platform decoder at 8 bits per component; a component that carries no information is dropped (R = G = B becomes Grayscale, an opaque alpha is removed).
- **Grayscale conversion** produces 8 bits, or 16 bits when the color components have more than 8 bits.
- **Settings** moved from `App.config` to `appsettings.json` next to the executable (`MaxBufferSize`, `Platforms`).
- **Releases** are self-contained, trimmed single-file executables for `win-x64` and `linux-x64`; no .NET installation is needed.

## Verification and results

Every output of every test case decodes back to the exact source frames with the unmodified C++ decoder. The legacy path is byte-identical to the original compressor (SHA-256 per case), and the optimal path is pinned by its own baseline.

Compressed size in bytes. Baseline is the version 1 compressor with the sprint settings; Auto is version 2 with Auto settings (4 cores).

| Case | Version 1 | Version 2 (Auto) | Change | Chosen settings |
| --- | ---: | ---: | ---: | --- |
| logo-gray | 12678 | 10332 | −18.50% | block bits 7, FixedWindowFirst, squares 2×2 |
| logo-gray-2x2 | 10624 | 10332 | −2.75% | block bits 7, FixedWindowFirst, squares 2×2 |
| mattress-gray | 23231 | 22735 | −2.14% | block bits 4, FixedWindowFirst, no squares |
| noise-gray | 50233 | 50073 | −0.32% | block bits 8, FixedWindowFirst, squares 6×6 |
| gradient-gray | 16282 | 16157 | −0.77% | block bits 7, FixedWindowFirst, no squares |
| parrot-gray-2x2 | 23391 | 23037 | −1.51% | block bits 5, FixedWindowFirst, squares 2×2 |
| mit-text | 28431 | 28430 | 0.00% | block bits 15, FixedWindowFirst |
| logo-565-2x2 | 15546 | 14644 | −5.80% | block bits 8, FixedWindowOnly, squares 2×2 |
| logo-565-fw-only | 14942 | 14644 | −1.99% | block bits 8, FixedWindowOnly, squares 2×2 |
| logo-565-afw-only | 15183 | 14644 | −3.55% | block bits 8, FixedWindowOnly, squares 2×2 |
| gradient-565-2x2 | 17468 | 15535 | −11.07% | block bits 7, AdaptiveFloatingWindowOnly, no squares |
| nebula-565-2x2 | 62974 | 60676 | −3.65% | block bits 5, FixedWindowFirst, squares 2×2 |
| parrot-565-2x2 | 41318 | 40095 | −2.96% | block bits 5, FixedWindowFirst, squares 2×2 |
| parrot-565-afw-first | 43097 | 40095 | −6.97% | block bits 5, FixedWindowFirst, squares 2×2 |
| logo-888 | 26734 | 22903 | −14.33% | block bits 9, FixedWindowFirst, squares 2×2 |
| icons-888 | 29382 | 22203 | −24.43% | block bits 8, FixedWindowFirst, squares 2×2 |

The optimal chain alone saves 0–5% at the same settings. Most of the larger gains come from Auto choosing 2×2 squares or a different Δ option. Compression time across the test cases went from 53.7 s to 2.9 s.

## Possible next steps

Format extensions, such as a "same methods as the previous block" flag or a variable-size length field, could make files smaller still. They would change the decoder, so none is implemented.
