# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [2.1.0] - 2026-11-04

The compressed stream format is unchanged. The microcontroller decoder has a bug fix, so replace your copy of the [decompression code](Decompression) (or the mod you use) with the new one.

### Added
- The image can be downscaled before compression: the width and height of the resulting image are entered under the channels (one follows the other by the aspect ratio, up to the original size). The image is downscaled by area averaging after the grayscale conversion and the alpha choice, with the color weighted by alpha, and the bit depth is reduced after downscaling. The square sizes offered and tried by Auto follow the resulting size.
- The preview shows the size and pixel format of the resulting image (for example `75x100, RGBA 5-5-5-5, 20 bpp`).
- [Sprint 4 theory document](docs/Sprint_4_Full_Theory.pdf): the definitions, lemmas and proofs behind version 2 on A4 pages, with references to the code. The [Sprint 4 deck](docs/Sprint_4_Mathematical_foundation.pdf) got a notation slide and math formulas.

### Changed
- The squares option moved to the compression settings at the bottom of the window.

### Fixed
- Decompression: the adaptive window flags of the current block were kept in memory allocated anew on every `decompressNextFrames()` call, so a block that continued into the next call could decode wrong values (depending on the heap). This affects programs that decode an image in several calls, as the examples do with their line buffers, and goes back to version 1. The flags are now part of the decoder state, and the decoder no longer uses `malloc`. Fixed in `Decompression/`, all mods and all examples.
- The decoder round-trip test decodes in chunks of 97 frames, so blocks that span two calls are checked.
- The frame-by-frame chain builder of the library (`Compressor.UseOptimalBlockChain = false`; the application always builds the optimal chain) skipped the block starting one frame after each new main block, a bug present since version 1. Its output is now 0.58% smaller on the test set (up to 4.1%); the reference outputs of the benchmark were regenerated.
- Large images (for example a 30-megapixel photo) made the application pause on every checkbox click and before drop-down lists opened. Images larger than 1024 pixels are now shown downscaled, while compression still uses every pixel.
- Auto settings needed several gigabytes of memory per processor core on large images: every trial kept its own copy of the image and a table for all frames. The trials now share the image and keep only the last block length of results, so memory no longer grows with the number of cores.
- Auto settings are about 3 times faster with exactly the same result: the lengths of the methods are found once for several block sizes, "Both options, Adaptive first" is no longer calculated separately (it always has the same size as "Fixed first"), and the search of the optimal chain skips block lengths that can't give a smaller size.

## [2.0.0] - 2026-10-19

The compressed stream format is unchanged: files produced by version 2 decode with the same [decompression code](Decompression) as before. Details and measurements are in [docs/v2-changes.md](docs/v2-changes.md) and the [Sprint 4 deck](docs/Sprint_4_Mathematical_foundation.pdf).

### Added
- Cross-platform desktop application (Avalonia) for Windows x64 and Linux x64, released as self-contained, trimmed single-file executables.
- Optimal chain of blocks: the chain is built by dynamic programming and is the smallest stream the decoder can read for the chosen settings (0–5% smaller than version 1).
- **Settings: Auto** (the default) selects the block length field, delta option and square size that give the smallest file (up to 24% smaller than version 1 with the sprint settings). **Manual** keeps the previous behavior.
- Native PNG decoding that keeps the exact bit depth: 1/2/4/8/16-bit grayscale, 8/16-bit RGB and RGBA, palette images, transparency and interlacing.
- `appsettings.json` next to the executable for `MaxBufferSize` and the list of `.h` platforms.
- Headless UI tests, a benchmark with byte-exact baselines and a round-trip check through the unmodified microcontroller decoder, run by GitHub Actions.
- Sprint 1–4 presentations in [docs](docs).

### Changed
- The best block is found by an exact formula instead of trying every method combination, with the same result; compression of the test set went from 53.7 s to 2.9 s.
- All window lengths of a channel are found in one scan of the data.
- The compression logic moved into the UI-independent `DeltaComp.Core` library.
- The project targets .NET 10.
- Grayscale conversion produces 8 bits, or 16 bits when the color components have more than 8 bits.

### Removed
- The "faster / more efficient" search option: the exact search is as fast as the old fast mode.
- The Windows-only WPF application and `App.config`.
- Binaries in the repository (`Release/`); downloads are on the [Releases](https://github.com/DmitryMuravyev/Delta-Compression/releases) page.

## [1.0.0] - 2024

The original release: WPF compressor for Windows, decompression code for AVR, ESP32 and STM32, and examples. See Sprints 1–3 in [docs](docs).

[Unreleased]: https://github.com/DmitryMuravyev/Delta-Compression/compare/v2.1.0...HEAD
[2.1.0]: https://github.com/DmitryMuravyev/Delta-Compression/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/DmitryMuravyev/Delta-Compression/releases/tag/v2.0.0
