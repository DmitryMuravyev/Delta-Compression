using System.Diagnostics;
using System.Security.Cryptography;
using DeltaComp.Bench.Reference;

namespace DeltaComp.Bench
{
    public enum Engine
    {
        Current,    // DeltaComp.Core Compressor (the code being optimized)
        Reference   // Frozen copy of the original compressor
    }


    // Result of a single compression run.
    public sealed class RunResult
    {
        public required BenchCase Case { get; init; }
        public required Engine Engine { get; init; }
        public required byte[] Output { get; init; }
        public required double Milliseconds { get; init; }
        public required long AllocatedBytes { get; init; }
        public required uint TotalFrames { get; init; }
        public required byte BitsPerFrame { get; init; }
        public required byte[] BitsPerChannel { get; init; }
        public required byte[] BitsPerMethodDeclaration { get; init; }

        public string Sha256 => Convert.ToHexString(SHA256.HashData(Output)).ToLowerInvariant();

        // Efficiency as defined in the sprints: 1 - compressed bits / raw bits (raw at target bit depth)
        public double Efficiency => 1.0 - (Output.Length * 8.0) / ((double)TotalFrames * BitsPerFrame);
    }


    public static class Runner
    {
        // Build the chain of blocks with the optimal chain builder (the default of the compressor).
        // When false, the frame-by-frame analysis is used, which must match the frozen reference byte for byte.
        public static bool OptimalChain = false;

        public static RunResult Run(BenchCase c, LoadedSource source, Engine engine)
        {
            var output = new List<byte>(1 << 16);
            void Write(byte b) => output.Add(b);
            static void Progress(CompressionStage stage, double progress) { }

            long allocatedBefore = GC.GetTotalAllocatedBytes(true);
            var watch = Stopwatch.StartNew();

            byte bitsPerFrame;
            byte[] bitsPerChannel, bitsPerMethodDeclaration;

            if (engine == Engine.Current)
            {
                Compressor compressor = source.IsImage
                    ? new Compressor(source.Channels, (ushort)source.Width, (ushort)source.Height, true, Write, Progress)
                    : new Compressor(source.Channels, source.TotalFrames, true, Write, Progress);
                if (source.IsImage)
                {
                    compressor.SplitToSquares = c.SquareWidth > 1;
                    compressor.WidthOfSquare = c.SquareWidth;
                }
                compressor.BlockSizeBits = c.BlockSizeBits;
                compressor.DeltaCalculationOption = c.Delta;
                compressor.UseOptimalBlockChain = OptimalChain;
                compressor.Compress(null);
                if (compressor.Stage != CompressionStage.Done) throw new InvalidOperationException($"{c.Name}: stage {compressor.Stage}");
                bitsPerFrame = compressor.BitsPerFrame;
                bitsPerChannel = compressor.BitsPerChannel;
                bitsPerMethodDeclaration = compressor.BitsPerMethodDeclaration;
            }
            else
            {
                ReferenceCompressor compressor = source.IsImage
                    ? new ReferenceCompressor(source.Channels, (ushort)source.Width, (ushort)source.Height, true, Write, Progress)
                    : new ReferenceCompressor(source.Channels, source.TotalFrames, true, Write, Progress);
                if (source.IsImage)
                {
                    compressor.SplitToSquares = c.SquareWidth > 1;
                    compressor.WidthOfSquare = c.SquareWidth;
                }
                compressor.BlockSizeBits = c.BlockSizeBits;
                compressor.DeltaCalculationOption = c.Delta;
                compressor.UseBruteForceBestBlockSearch = true;
                compressor.Compress(null);
                if (compressor.Stage != CompressionStage.Done) throw new InvalidOperationException($"{c.Name}: stage {compressor.Stage}");
                bitsPerFrame = compressor.BitsPerFrame;
                bitsPerChannel = compressor.BitsPerChannel;
                bitsPerMethodDeclaration = compressor.BitsPerMethodDeclaration;
            }

            watch.Stop();
            long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;

            return new RunResult
            {
                Case = c, Engine = engine, Output = output.ToArray(),
                Milliseconds = watch.Elapsed.TotalMilliseconds, AllocatedBytes = allocated,
                TotalFrames = source.TotalFrames, BitsPerFrame = bitsPerFrame,
                BitsPerChannel = (byte[])bitsPerChannel.Clone(),
                BitsPerMethodDeclaration = (byte[])bitsPerMethodDeclaration.Clone()
            };
        }


        // Frames as the decoder emits them (in compression order, channel 0 in the most significant bits).
        public static ulong[] ExpectedFrames(BenchCase c, LoadedSource source)
        {
            var frames = new ulong[source.TotalFrames];
            int squareWidth = source.IsImage && c.SquareWidth > 1 ? c.SquareWidth : 1;
            int squareSize = squareWidth * squareWidth;
            int squaresPerWidth = source.IsImage ? source.Width / squareWidth : 0;

            for (uint i = 0; i < source.TotalFrames; i++)
            {
                uint pixel = i;
                if (squareWidth > 1)
                {
                    // Same mapping as Compressor.GetPixelForSquares()
                    uint square = i / (uint)squareSize;
                    uint squaresLine = square / (uint)squaresPerWidth;
                    uint firstPixel = (uint)(squaresLine * squareSize * squaresPerWidth + (square % squaresPerWidth) * squareWidth);
                    uint offset = i % (uint)squareSize;
                    uint row = offset / (uint)squareWidth;
                    uint column = offset - row * (uint)squareWidth;
                    pixel = firstPixel + row * (uint)source.Width + column;
                }

                ulong frame = 0;
                foreach (Channel channel in source.Channels)
                {
                    frame <<= channel.TargetBitDepth;
                    frame |= channel.GetTargetValueForFrame((int)pixel);
                }
                frames[i] = frame;
            }
            return frames;
        }
    }
}
