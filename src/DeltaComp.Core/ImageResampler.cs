/*

MIT License

Copyright (c) 2024 Dmitry Muravyev (youtube.com/@DmitryMuravyev)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DeltaComp
{
    // A channel of a downscaled image. The values are calculated once by ImageResampler and kept
    // at the bit depth of the source channel; the target bit depth is the source channel's one,
    // so the bit depth reduction is applied to the downscaled values.
    public sealed class ResampledChannel : Channel
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Downscaled data
        private readonly ImagePlane _plane;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // The channel this one was made from
        public Channel Source { get; }


        // Constructor
        internal ResampledChannel(Channel source, ImagePlane plane)
        {
            Source = source;
            _plane = plane;
            ChannelType = source.ChannelType;
            BitsPerChannel = source.BitsPerChannel;
            BitDepthList = source.BitDepthList;
        }


        // The bit depth is selected for the source channel
        public override int TargetBitDepth
        {
            get => Source.TargetBitDepth;
            set => Source.TargetBitDepth = value;
        }


        // Base class method's implementation
        public override UInt32 GetOriginalValueForFrame(int frameNumber)
        {
            return _plane[frameNumber];
        }
    }


    // Downscales the channels of an image by area averaging: every pixel of the result is the average of the
    // source area it covers, with the partly covered source pixels taken in proportion to the covered part.
    public static class ImageResampler
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private types
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // The source pixels covered by every pixel of the result along one axis.
        // Weights are the covered parts in 1/destinationSize of a source pixel, so they are exact integers,
        // and the weights of one result pixel always add up to sourceSize.
        private sealed class AxisMap
        {
            public readonly int[] Start;    // The first source pixel of each result pixel
            public readonly int[] Offset;   // Index of its first weight in Weights (Offset[i + 1] is the end)
            public readonly long[] Weights;

            public AxisMap(int sourceSize, int destinationSize)
            {
                Start = new int[destinationSize];
                Offset = new int[destinationSize + 1];
                List<long> weights = [];
                for (int i = 0; i < destinationSize; i++)
                {
                    // The result pixel covers [i * sourceSize, (i + 1) * sourceSize) and the source pixel j
                    // covers [j * destinationSize, (j + 1) * destinationSize), both in the same units.
                    long from = (long)i * sourceSize;
                    long to = from + sourceSize;
                    int first = (int)(from / destinationSize);
                    int last = (int)((to - 1) / destinationSize);
                    Start[i] = first;
                    Offset[i] = weights.Count;
                    for (int j = first; j <= last; j++)
                    {
                        weights.Add(Math.Min(to, (long)(j + 1) * destinationSize) - Math.Max(from, (long)j * destinationSize));
                    }
                }
                Offset[destinationSize] = weights.Count;
                Weights = weights.ToArray();
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Downscale the channels of a width x height image to destinationWidth x destinationHeight.
        // If there is an alpha channel among them, the color is averaged with the alpha as the weight
        // (a fully transparent pixel has no color to give); the alpha itself is a plain average.
        public static List<ResampledChannel> Resample(IReadOnlyList<Channel> channels, int width, int height,
            int destinationWidth, int destinationHeight)
        {
            if ((destinationWidth <= 0) || (destinationWidth > width) || (destinationHeight <= 0) || (destinationHeight > height))
                throw new ArgumentOutOfRangeException(nameof(destinationWidth), "The destination size must not exceed the image size.");

            int count = channels.Count;
            Channel? alpha = channels.FirstOrDefault(c => c.ChannelType == ChannelType.Alpha);
            double alphaMax = (alpha != null) ? Channel.GetMaxNumberForBitDepth(alpha.BitsPerChannel) : 1.0d;

            ImagePlane[] planes = new ImagePlane[count];
            double[] maxValues = new double[count];
            for (int c = 0; c < count; c++)
            {
                Trace.Assert(channels[c].BitsPerChannel <= 16, "ImageResampler, wrong bit depth: " + channels[c].BitsPerChannel.ToString());
                planes[c] = new ImagePlane(channels[c].ChannelType, channels[c].BitsPerChannel, destinationWidth * destinationHeight);
                maxValues[c] = Channel.GetMaxNumberForBitDepth(channels[c].BitsPerChannel);
            }

            AxisMap columns = new AxisMap(width, destinationWidth);
            AxisMap rows = new AxisMap(height, destinationHeight);
            double totalWeight = (double)width * height; // The weights of every result pixel add up to this

            // One row of the result at a time; each worker keeps its own sums
            Parallel.For(0, destinationHeight,
                () => (sums: new double[count * destinationWidth], alphaSums: new double[count * destinationWidth],
                       alphaWeights: new double[destinationWidth]),
                (y, _, buffers) =>
                {
                    (double[] sums, double[] alphaSums, double[] alphaWeights) = buffers;
                    Array.Clear(sums);
                    Array.Clear(alphaSums);
                    Array.Clear(alphaWeights);

                    for (int k = rows.Offset[y], sourceY = rows.Start[y]; k < rows.Offset[y + 1]; k++, sourceY++)
                    {
                        double rowWeight = rows.Weights[k];
                        int rowStart = sourceY * width;

                        for (int x = 0; x < destinationWidth; x++)
                        {
                            for (int m = columns.Offset[x], sourceX = columns.Start[x]; m < columns.Offset[x + 1]; m++, sourceX++)
                            {
                                int frame = rowStart + sourceX;
                                double weight = rowWeight * columns.Weights[m];
                                double alphaWeight = (alpha != null) ? weight * alpha.GetOriginalValueForFrame(frame) / alphaMax : weight;
                                alphaWeights[x] += alphaWeight;

                                for (int c = 0, i = x; c < count; c++, i += destinationWidth)
                                {
                                    double value = channels[c].GetOriginalValueForFrame(frame);
                                    sums[i] += weight * value;
                                    alphaSums[i] += alphaWeight * value;
                                }
                            }
                        }
                    }

                    int resultRow = y * destinationWidth;
                    for (int c = 0; c < count; c++)
                    {
                        // The alpha weighting applies to the color only, and only if the area isn't fully transparent
                        bool weighted = (alpha != null) && (channels[c] != alpha);
                        for (int x = 0, i = c * destinationWidth; x < destinationWidth; x++, i++)
                        {
                            double value = (weighted && (alphaWeights[x] > 0)) ? alphaSums[i] / alphaWeights[x] : sums[i] / totalWeight;
                            planes[c][resultRow + x] = (UInt32)Math.Min(maxValues[c], Math.Round(value));
                        }
                    }
                    return buffers;
                },
                _ => { });

            List<ResampledChannel> result = new List<ResampledChannel>(count);
            for (int c = 0; c < count; c++) result.Add(new ResampledChannel(channels[c], planes[c]));
            return result;
        }

    }
}

// END-OF-FILE
