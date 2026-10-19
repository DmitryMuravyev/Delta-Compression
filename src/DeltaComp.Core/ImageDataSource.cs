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

namespace DeltaComp
{
    // One component (R/G/B/A/Grayscale) of a decoded image, stored as a separate plane
    // at its native bit depth. Up to 8 bits per sample are kept in bytes, deeper samples in words.
    public sealed class ImagePlane
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        private readonly byte[]? _bytes;
        private readonly ushort[]? _words;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public attributes/methods
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public readonly ChannelType Type;
        public readonly int BitDepth;


        // Constructor
        public ImagePlane(ChannelType type, int bitDepth, int pixelsCount)
        {
            Trace.Assert((bitDepth >= 1) && (bitDepth <= 16), "ImagePlane, wrong bit depth: " + bitDepth.ToString());

            Type = type;
            BitDepth = bitDepth;
            if (bitDepth <= 8)
            {
                _bytes = new byte[pixelsCount];
            }
            else
            {
                _words = new ushort[pixelsCount];
            }
        }


        // Sample value of the specified pixel
        public UInt32 this[int pixelNumber]
        {
            get
            {
                return (_bytes != null) ? _bytes[pixelNumber] : _words![pixelNumber];
            }
            set
            {
                if (_bytes != null)
                {
                    _bytes[pixelNumber] = (byte)value;
                }
                else
                {
                    _words![pixelNumber] = (ushort)value;
                }
            }
        }
    }


    // A decoded image kept entirely in memory: one plane per component.
    // Since everything is in memory, pixels can always be read from parallel threads.
    public class ImageDataSource : DataSource
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public readonly int Width = 0;
        public readonly int Height = 0;
        public readonly IReadOnlyList<ImagePlane> Planes;

        // Human-readable description of the source pixel format, e.g. "PNG, Grayscale 16-bit"
        public readonly string FormatDescription;

        // The whole image is in memory
        public bool MultithreadedReadingAllowed => true;

        // The image has R/G/B components (otherwise it is Grayscale)
        public bool IsColor => Planes.Any(p => p.Type == ChannelType.Red);

        // The image has an alpha component
        public bool HasAlpha => Planes.Any(p => p.Type == ChannelType.Alpha);

        // Maximum bit depth of the color (R/G/B) components
        public int MaxColorBitDepth => Planes.Where(p => p.Type <= ChannelType.Blue).Select(p => p.BitDepth).DefaultIfEmpty(0).Max();


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Constructor
        public ImageDataSource(int width, int height, IReadOnlyList<ImagePlane> planes, string formatDescription)
        {
            Trace.Assert((width > 0) && (height > 0) && (planes.Count > 0));

            Width = width;
            Height = height;
            Planes = planes;
            FormatDescription = formatDescription;
            ChannelsCount = planes.Count;
            BitsPerFrame = planes.Sum(p => p.BitDepth);
        }


        // Get the plane of the specified component (or null if the image has no such component)
        public ImagePlane? GetPlane(ChannelType type)
        {
            return Planes.FirstOrDefault(p => p.Type == type);
        }


        // Load an image file. PNG files are decoded natively, preserving their exact bit depth
        // (1/2/4/8/16-bit grayscale, 8/16-bit color, with or without alpha).
        // Any other format is passed to the fallbackDecoder, which must return 8-bit RGBA pixels.
        public static ImageDataSource Load(string filePath, Func<string, Rgba32Image>? fallbackDecoder)
        {
            byte[] data = File.ReadAllBytes(filePath);
            if (PngDecoder.IsPng(data)) return PngDecoder.Decode(data);

            if (fallbackDecoder == null) throw new NotSupportedException("Unsupported image format.");

            Rgba32Image image = fallbackDecoder(filePath);
            string extension = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            return FromRgba32(image, (extension.Length > 0) ? extension : "Image");
        }


        // Build the image from 8-bit RGBA (or BGRA) pixels. Since such pixels usually come
        // from a generic decoder, the components that carry no information are dropped:
        // the image becomes Grayscale if R = G = B everywhere, and the alpha is removed if it is opaque.
        public static ImageDataSource FromRgba32(Rgba32Image image, string formatName)
        {
            int count = image.Width * image.Height;
            ReadOnlySpan<byte> pixels = image.Pixels;
            Trace.Assert(pixels.Length >= count * 4);

            int r = image.IsBgra ? 2 : 0;
            int b = image.IsBgra ? 0 : 2;
            bool isGray = true;
            bool isOpaque = true;
            for (int i = 0, o = 0; i < count; i++, o += 4)
            {
                if ((pixels[o] != pixels[o + 1]) || (pixels[o + 1] != pixels[o + 2])) isGray = false;
                if (pixels[o + 3] != 255) isOpaque = false;
                if (!isGray && !isOpaque) break;
            }

            List<(ChannelType type, int offset)> components = isGray ?
                [ (ChannelType.Grayscale, 1) ] :
                [ (ChannelType.Red, r), (ChannelType.Green, 1), (ChannelType.Blue, b) ];
            if (!isOpaque) components.Add((ChannelType.Alpha, 3));

            List<ImagePlane> planes = [];
            foreach (var (type, offset) in components)
            {
                ImagePlane plane = new ImagePlane(type, 8, count);
                for (int i = 0, o = offset; i < count; i++, o += 4) plane[i] = pixels[o];
                planes.Add(plane);
            }

            string description = String.Format("{0}, {1}{2} 8-bit", formatName,
                isGray ? "Grayscale" : "RGB", isOpaque ? "" : " + Alpha");
            return new ImageDataSource(image.Width, image.Height, planes, description);
        }

    }


    // 8-bit RGBA/BGRA pixels produced by a platform image decoder
    public sealed class Rgba32Image(int width, int height, byte[] pixels, bool isBgra)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public byte[] Pixels { get; } = pixels;
        public bool IsBgra { get; } = isBgra;
    }

}

// END-OF-FILE
