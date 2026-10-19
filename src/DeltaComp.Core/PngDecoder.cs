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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DeltaComp
{
    // A small PNG decoder that keeps the exact bit depth of every component, which matters a lot
    // for MCU graphics (a 1/2/4-bit grayscale or a 16-bit image must not be widened to 8 bits).
    //
    // Supported: all PNG color types and bit depths, Adam7 interlacing and tRNS transparency.
    // Palette images are expanded to RGB 8-bit (+ alpha if the palette has transparency),
    // a tRNS color key adds an alpha channel. Ancillary chunks (gamma, color profiles, text) are ignored.
    public static class PngDecoder
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private constants
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        private static readonly byte[] _signature = [ 137, 80, 78, 71, 13, 10, 26, 10 ];

        // Adam7 passes: start x, start y, step x, step y
        private static readonly int[][] _adam7 = [ [ 0, 0, 8, 8 ], [ 4, 0, 8, 8 ], [ 0, 4, 4, 8 ], [ 2, 0, 4, 4 ],
                                                   [ 0, 2, 2, 4 ], [ 1, 0, 2, 2 ], [ 0, 1, 1, 2 ] ];

        // PNG color types
        private const int ColorTypeGray = 0;
        private const int ColorTypeRgb = 2;
        private const int ColorTypePalette = 3;
        private const int ColorTypeGrayAlpha = 4;
        private const int ColorTypeRgba = 6;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Check the PNG signature
        public static bool IsPng(ReadOnlySpan<byte> data)
        {
            return data.Length >= _signature.Length && data[.._signature.Length].SequenceEqual(_signature);
        }


        // Decode a PNG file
        public static ImageDataSource Decode(byte[] data)
        {
            if (!IsPng(data)) throw new InvalidDataException("Not a PNG file.");

            int width = 0, height = 0, bitDepth = 0, colorType = -1, interlace = 0;
            byte[]? palette = null;
            byte[]? transparency = null;
            MemoryStream compressed = new MemoryStream();

            // Read chunks
            int pos = _signature.Length;
            while (pos + 12 <= data.Length)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos));
                string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                if ((length < 0) || (pos + 12 + length > data.Length)) throw new InvalidDataException("Corrupted PNG chunk: " + type);
                ReadOnlySpan<byte> chunk = data.AsSpan(pos + 8, length);
                pos += 12 + length;

                if (type == "IHDR")
                {
                    width = BinaryPrimitives.ReadInt32BigEndian(chunk);
                    height = BinaryPrimitives.ReadInt32BigEndian(chunk[4..]);
                    bitDepth = chunk[8];
                    colorType = chunk[9];
                    interlace = chunk[12];
                }
                else if (type == "PLTE") palette = chunk.ToArray();
                else if (type == "tRNS") transparency = chunk.ToArray();
                else if (type == "IDAT") compressed.Write(chunk);
                else if (type == "IEND") break;
            }

            int samplesPerPixel = colorType switch
            {
                ColorTypeGray => 1,
                ColorTypeRgb => 3,
                ColorTypePalette => 1,
                ColorTypeGrayAlpha => 2,
                ColorTypeRgba => 4,
                _ => throw new InvalidDataException("Unsupported PNG color type: " + colorType.ToString())
            };
            if ((width <= 0) || (height <= 0)) throw new InvalidDataException("Wrong PNG image size.");
            if ((bitDepth is not (1 or 2 or 4 or 8 or 16)) || ((colorType == ColorTypePalette) && (palette == null)))
                throw new InvalidDataException("Unsupported PNG format.");

            // Decompress the image data
            compressed.Position = 0;
            byte[] raw;
            using (ZLibStream zlib = new ZLibStream(compressed, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                zlib.CopyTo(output);
                raw = output.ToArray();
            }

            // Create planes
            int count = width * height;
            bool hasAlpha = (colorType == ColorTypeGrayAlpha) || (colorType == ColorTypeRgba) || (transparency != null);
            bool isColor = (colorType == ColorTypeRgb) || (colorType == ColorTypePalette) || (colorType == ColorTypeRgba);
            int colorBitDepth = (colorType == ColorTypePalette) ? 8 : bitDepth;
            int alphaBitDepth = (colorType == ColorTypePalette) ? 8 : Math.Max(bitDepth, 8);

            List<ImagePlane> planes = [];
            if (isColor)
            {
                planes.Add(new ImagePlane(ChannelType.Red, colorBitDepth, count));
                planes.Add(new ImagePlane(ChannelType.Green, colorBitDepth, count));
                planes.Add(new ImagePlane(ChannelType.Blue, colorBitDepth, count));
            }
            else
            {
                planes.Add(new ImagePlane(ChannelType.Grayscale, colorBitDepth, count));
            }
            if (hasAlpha) planes.Add(new ImagePlane(ChannelType.Alpha, alphaBitDepth, count));

            PixelWriter writer = new PixelWriter(planes, colorType, bitDepth, palette, transparency, alphaBitDepth);

            // Unfilter and unpack scanlines
            int bitsPerPixel = samplesPerPixel * bitDepth;
            int filterStep = Math.Max(1, bitsPerPixel >> 3);
            int offset = 0;

            for (int pass = 0; pass < ((interlace == 1) ? 7 : 1); pass++)
            {
                int[] p = (interlace == 1) ? _adam7[pass] : [ 0, 0, 1, 1 ];
                int passWidth = (width - p[0] + p[2] - 1) / p[2];
                int passHeight = (height - p[1] + p[3] - 1) / p[3];
                if ((passWidth <= 0) || (passHeight <= 0)) continue;

                int stride = (passWidth * bitsPerPixel + 7) >> 3;
                byte[] previous = new byte[stride];
                byte[] current = new byte[stride];

                for (int y = 0; y < passHeight; y++)
                {
                    if (offset + 1 + stride > raw.Length) throw new InvalidDataException("PNG image data is truncated.");
                    int filter = raw[offset];
                    Array.Copy(raw, offset + 1, current, 0, stride);
                    offset += 1 + stride;
                    Unfilter(filter, current, previous, filterStep);

                    int pixel = (p[1] + y * p[3]) * width + p[0];
                    for (int x = 0; x < passWidth; x++, pixel += p[2]) writer.Write(current, x, pixel);

                    (previous, current) = (current, previous);
                }
            }

            string description = String.Format("PNG, {0} {1}-bit", colorType switch
            {
                ColorTypeGray => (transparency != null) ? "Grayscale + Alpha" : "Grayscale",
                ColorTypeRgb => (transparency != null) ? "RGB + Alpha" : "RGB",
                ColorTypePalette => (transparency != null) ? "Palette → RGB + Alpha" : "Palette → RGB",
                ColorTypeGrayAlpha => "Grayscale + Alpha",
                _ => "RGBA"
            }, bitDepth);

            return new ImageDataSource(width, height, planes, description);
        }


        // Reverse the scanline filter
        private static void Unfilter(int filter, byte[] line, byte[] previous, int step)
        {
            switch (filter)
            {
                case 0:
                    break;
                case 1: // Sub
                    for (int i = step; i < line.Length; i++) line[i] += line[i - step];
                    break;
                case 2: // Up
                    for (int i = 0; i < line.Length; i++) line[i] += previous[i];
                    break;
                case 3: // Average
                    for (int i = 0; i < line.Length; i++)
                    {
                        int left = (i >= step) ? line[i - step] : 0;
                        line[i] += (byte)((left + previous[i]) >> 1);
                    }
                    break;
                case 4: // Paeth
                    for (int i = 0; i < line.Length; i++)
                    {
                        int a = (i >= step) ? line[i - step] : 0;
                        int b = previous[i];
                        int c = (i >= step) ? previous[i - step] : 0;
                        int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - 2 * c);
                        line[i] += (byte)(((pa <= pb) && (pa <= pc)) ? a : ((pb <= pc) ? b : c));
                    }
                    break;
                default:
                    throw new InvalidDataException("Unknown PNG filter type: " + filter.ToString());
            }
        }


        // Unpacks the samples of one pixel from a scanline into the planes
        private sealed class PixelWriter
        {
            private readonly ImagePlane[] _planes;
            private readonly int _colorType;
            private readonly int _bitDepth;
            private readonly int _samples;
            private readonly byte[]? _palette;
            private readonly byte[]? _transparency;
            private readonly uint _alphaMax;
            private readonly uint[] _sample = new uint[4];

            public PixelWriter(List<ImagePlane> planes, int colorType, int bitDepth, byte[]? palette, byte[]? transparency, int alphaBitDepth)
            {
                _planes = planes.ToArray();
                _colorType = colorType;
                _bitDepth = bitDepth;
                _samples = colorType switch { ColorTypeRgb => 3, ColorTypeGrayAlpha => 2, ColorTypeRgba => 4, _ => 1 };
                _palette = palette;
                _transparency = transparency;
                _alphaMax = (1u << alphaBitDepth) - 1;
            }

            public void Write(byte[] line, int x, int pixel)
            {
                for (int s = 0; s < _samples; s++) _sample[s] = ReadSample(line, x * _samples + s);

                switch (_colorType)
                {
                    case ColorTypePalette:
                        int index = (int)_sample[0];
                        int entry = index * 3;
                        if (entry + 2 >= _palette!.Length) throw new InvalidDataException("PNG palette index is out of range.");
                        _planes[0][pixel] = _palette[entry];
                        _planes[1][pixel] = _palette[entry + 1];
                        _planes[2][pixel] = _palette[entry + 2];
                        if (_transparency != null) _planes[3][pixel] = (index < _transparency.Length) ? _transparency[index] : 255u;
                        break;

                    case ColorTypeGray:
                    case ColorTypeRgb:
                        bool transparent = (_transparency != null);
                        for (int s = 0; s < _samples; s++)
                        {
                            _planes[s][pixel] = _sample[s];
                            if (transparent && (BinaryPrimitives.ReadUInt16BigEndian(_transparency.AsSpan(s * 2)) != _sample[s])) transparent = false;
                        }
                        if (_transparency != null) _planes[_samples][pixel] = transparent ? 0u : _alphaMax;
                        break;

                    default: // Gray + Alpha, RGBA
                        for (int s = 0; s < _samples; s++) _planes[s][pixel] = _sample[s];
                        break;
                }
            }

            private uint ReadSample(byte[] line, int sampleIndex)
            {
                switch (_bitDepth)
                {
                    case 8:
                        return line[sampleIndex];
                    case 16:
                        return (uint)((line[sampleIndex * 2] << 8) | line[sampleIndex * 2 + 1]);
                    default:
                        // Sub-byte samples are packed starting from the most significant bit
                        int bit = sampleIndex * _bitDepth;
                        int shift = 8 - _bitDepth - (bit & 7);
                        return (uint)((line[bit >> 3] >> shift) & ((1 << _bitDepth) - 1));
                }
            }
        }

    }
}

// END-OF-FILE
