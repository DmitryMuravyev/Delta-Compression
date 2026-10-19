using StbImageSharp;

namespace DeltaComp.Bench
{
    public enum SourceKind
    {
        Grayscale,  // 1 channel, 8 bits
        Rgb,        // 3 channels, 8 bits each (alpha is dropped)
        Data        // raw file bytes, 1 channel, 8 bits
    }


    // One benchmark case: a source file plus the full set of compression settings.
    public sealed record BenchCase(
        string Name,
        string File,
        SourceKind Kind,
        int[] TargetBits,
        byte BlockSizeBits,
        byte SquareWidth = 1,
        DeltaOption Delta = DeltaOption.FixedWindowFirst,
        bool Slow = false)
    {
        // The default suite. Settings follow the sprint 1-3 experiments (see Materials/*.pdf).
        public static readonly BenchCase[] All =
        [
            // Grayscale 8 bit
            new("logo-gray",          "Logo_240_Grayscale.png",                   SourceKind.Grayscale, [8], 7),
            new("logo-gray-2x2",      "Logo_240_Grayscale.png",                   SourceKind.Grayscale, [8], 7, 2),
            new("mattress-gray",      "Mattress_2_Grayscale_240.png",             SourceKind.Grayscale, [8], 4),
            new("noise-gray",         "Noise_Grayscale_240.png",                  SourceKind.Grayscale, [8], 10),
            new("gradient-gray",      "Gray_Gradient_240.png",                    SourceKind.Grayscale, [8], 7),
            new("parrot-gray-2x2",    "Grayscale_8bits_palette_sample_image.png", SourceKind.Grayscale, [8], 5, 2),

            // Text as 8-bit data
            new("mit-text",           "MIT_Lorem_Ipsum.txt",                      SourceKind.Data, [8], 15),

            // RGB 565
            new("logo-565-2x2",       "Logo_240_Color.png",                       SourceKind.Rgb, [5, 6, 5], 9, 2),
            new("logo-565-fw-only",   "Logo_240_Color.png",                       SourceKind.Rgb, [5, 6, 5], 9, 2, DeltaOption.FixedWindowOnly),
            new("logo-565-afw-only",  "Logo_240_Color.png",                       SourceKind.Rgb, [5, 6, 5], 8, 2, DeltaOption.AdaptiveFloatingWindowOnly),
            new("gradient-565-2x2",   "Color_Gradient_240.png",                   SourceKind.Rgb, [5, 6, 5], 8, 2),
            new("nebula-565-2x2",     "Nebula.png",                               SourceKind.Rgb, [5, 6, 5], 5, 2),
            new("parrot-565-2x2",     "RGB_24bits_palette_sample_image.png",      SourceKind.Rgb, [5, 6, 5], 5, 2),
            new("parrot-565-afw-first","RGB_24bits_palette_sample_image.png",     SourceKind.Rgb, [5, 6, 5], 5, 1, DeltaOption.AdaptiveFloatingWindowFirst),

            // RGB 888
            new("logo-888",           "Logo_240_Color.png",                       SourceKind.Rgb, [8, 8, 8], 8, Slow: true),
            new("icons-888",          "Icons.png",                                SourceKind.Rgb, [8, 8, 8], 7, Slow: true),
        ];
    }


    // Source data loaded into memory: per-channel original values plus geometry.
    public sealed class LoadedSource
    {
        public required List<Channel> Channels { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required uint TotalFrames { get; init; }
        public required bool IsImage { get; init; }

        public static LoadedSource Load(BenchCase c, string dataDir)
        {
            string path = Path.Combine(dataDir, c.File);

            if (c.Kind == SourceKind.Data)
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                uint[] values = Array.ConvertAll(bytes, b => (uint)b);
                return new LoadedSource
                {
                    Channels = [new ArrayChannel(ChannelType.Data, values, 8, c.TargetBits[0])],
                    Width = 0, Height = 0, TotalFrames = (uint)values.Length, IsImage = false
                };
            }

            ImageResult image;
            using (var stream = System.IO.File.OpenRead(path))
            {
                image = ImageResult.FromStream(stream,
                    c.Kind == SourceKind.Grayscale ? ColorComponents.Grey : ColorComponents.RedGreenBlue);
            }
            int n = image.Width * image.Height;

            if (c.Kind == SourceKind.Grayscale)
            {
                uint[] values = Array.ConvertAll(image.Data, v => (uint)v);
                return new LoadedSource
                {
                    Channels = [new ArrayChannel(ChannelType.Grayscale, values, 8, c.TargetBits[0])],
                    Width = image.Width, Height = image.Height, TotalFrames = (uint)n, IsImage = true
                };
            }

            uint[] r = new uint[n], g = new uint[n], b = new uint[n];
            for (int i = 0; i < n; i++)
            {
                r[i] = image.Data[i * 3];
                g[i] = image.Data[i * 3 + 1];
                b[i] = image.Data[i * 3 + 2];
            }
            return new LoadedSource
            {
                Channels =
                [
                    new ArrayChannel(ChannelType.Red, r, 8, c.TargetBits[0]),
                    new ArrayChannel(ChannelType.Green, g, 8, c.TargetBits[1]),
                    new ArrayChannel(ChannelType.Blue, b, 8, c.TargetBits[2]),
                ],
                Width = image.Width, Height = image.Height, TotalFrames = (uint)n, IsImage = true
            };
        }
    }
}
