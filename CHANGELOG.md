# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

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

[2.0.0]: https://github.com/DmitryMuravyev/Delta-Compression/releases/tag/v2.0.0
