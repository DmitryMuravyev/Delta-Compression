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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace DeltaComp
{
    // Source channel display options: one-to-one or Grayscale+Alpha to BGRA
    public enum ChannelMapping
    {
        OneToOne = 0,
        GrayscaleAlphaToColorAlpha = 1,
        Unknown = 1000
    }


    // A class that generates a preview image, which can be used to evaluate
    // the effect of the color channels bit depth on image quality.
    internal class BitmapPreviewDestination : PreviewDestination
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Source parameters and buffer
        private readonly List<Channel> _sourceChannels;
        private readonly ChannelOrder _order;
        private readonly ChannelMapping _mapping;
        private readonly List<UInt64> _masks;
        private readonly List<int> _shifts;
        private readonly List<int> _depths;

        // Preview pixels buffer
        private readonly byte[] _buffer;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Preview image parameters
        public WriteableBitmap? Bitmap;
        public readonly PixelFormat Format;
        public readonly int BitsPerPixel = 0;
        public readonly int ChannelsCount = 0;
        public readonly int Width = 0;
        public readonly int Height = 0;
        public readonly int Stride = 0;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        
        // Constructor
        public BitmapPreviewDestination(int width, int height, double dpiX, double dpiY,
                                        List<Channel> sourceChannels,
                                        PixelFormat pixelFormat, ChannelOrder order, ChannelMapping mapping)
        {
            Trace.Assert(sourceChannels != null);
            Trace.Assert((width > 0) && (height > 0) && (dpiX > 0.0d) && (dpiY > 0.0d));

            // Init base variables
            Width = width;
            Height = height;
            Format = pixelFormat;
            BitsPerPixel = Format.BitsPerPixel;
            _order = order;
            _sourceChannels = sourceChannels;
            _mapping = mapping;

            // Init WriteableBitmap
            try
            {
                Bitmap = new WriteableBitmap(Width, Height, dpiX, dpiY, Format, null);
            }
            catch
            {
                Trace.TraceError("Unable to create WriteableBitmap.");
            }

            // Init buffer
            Stride = (BitsPerPixel * Width + 7) >> 3; // Get full number of bytes per 1 image line
            _buffer = new byte[Stride];

            // Obtain pixel format attributes
            _masks = new List<UInt64>();
            _shifts = new List<int>();
            _depths = new List<int>();

            // Get channel mask list
            IList<PixelFormatChannelMask> formatMaskCollection = Format.Masks;
            ChannelsCount = formatMaskCollection.Count;

            int numberOfPreviewChannels = 0;
            foreach (PixelFormatChannelMask channelMask in formatMaskCollection)
            {
                numberOfPreviewChannels++;
                IList<byte> maskBytesCollection = channelMask.Mask;

                // Get mask
                UInt64 currentMask = 0;
                foreach (byte myByte in maskBytesCollection.Reverse())
                {
                    currentMask <<= 8;
                    currentMask |= myByte;
                }
                _masks.Add(currentMask);

                // Calculate the channel shift inside a pixel
                int currentShift = 0;
                while ((currentMask & 1) == 0)
                {
                    currentShift++;
                    currentMask >>= 1;
                }
                _shifts.Add(currentShift);

                // Calculate channel bit depth (not used in this class,
                // but I'll leave the code just in case).
                int currentDepth = 0;
                while ((currentMask & 1) != 0)
                {
                    currentDepth++;
                    currentMask >>= 1;
                }
                _depths.Add(currentDepth);
            }
        }


        // Destructor
        /*
        ~PreviewBitmapDestination()
        {
        }
        */


        // Construct specified pixel from the source channels data
        private UInt64 GetPixel(int pixelNumber)
        {
            UInt64 pixel = 0;
            // Iterate through the source channels
            for (int i = 0; i < _sourceChannels.Count; i++)
            {
                Channel currentChannel = _sourceChannels[i];
                int currentChannelComponentIndex;

                switch (_mapping)
                {
                    case ChannelMapping.OneToOne:

                        // Save component to a pixel. If the number and types of source channels
                        // do not match the preview pixel format, then the image may be distorted.
                        currentChannelComponentIndex = Channel.colorComponentPosition[(int)_order][(int)currentChannel.ChannelType];

                        pixel |= ((UInt64)currentChannel.GetPreviewValueForFrame(pixelNumber,
                                          _depths[currentChannelComponentIndex]) << _shifts[currentChannelComponentIndex]);

                        break;

                    case ChannelMapping.GrayscaleAlphaToColorAlpha:

                        // Get all color components from the Grayscale channel and save them to a pixel.
                        if (currentChannel.ChannelType == ChannelType.Grayscale)
                        {
                            for (int n = (int)ChannelType.Red; n <= (int)ChannelType.Blue; n++)
                            {
                                pixel |= ((UInt64)currentChannel.GetPreviewValueForFrame(pixelNumber, _depths[n]) << _shifts[n]);
                            }
                        }
                        // Save Alpha component to a pixel.
                        else if (currentChannel.ChannelType == ChannelType.Alpha)
                        {
                            currentChannelComponentIndex = Channel.colorComponentPosition[(int)_order][(int)ChannelType.Alpha];

                            pixel |= ((UInt64)currentChannel.GetPreviewValueForFrame(pixelNumber,
                                              _depths[currentChannelComponentIndex]) << _shifts[currentChannelComponentIndex]);
                        }

                        break;

                    default:
                        break;
                }
            }
            return pixel;
        }



        // Update the preview image according to the bit depth of the components.
        public override void UpdatePreview()
        {
            if (Bitmap == null) return;

            Bitmap.Lock(); // Lock() and Unlock() could be moved to the WritePixels() method.

            int pixelNumber = 0;
            int y = 0;

            // Image processing by strides (lines).
            while (y < Height)
            {
                int currentByte = 0;
                int currentBit = 0; // Reverse bit numbering. This path is easier
                                    // to understand and shorter in terms of code size.

                // Go through the pixel line
                while (currentByte < Stride)
                {
                    UInt64 pixel = GetPixel(pixelNumber++);

                    if (BitsPerPixel < 8)
                    {
                        // BPP is less than a byte so fill the byte with
                        // pixels and only then move on to the next one.
                        if (currentBit == 0) _buffer[currentByte] = 0;
                        _buffer[currentByte] |= (byte)(pixel << (8 - currentBit - BitsPerPixel));
                        currentBit += BitsPerPixel;
                        if (currentBit >= 8)
                        {
                            currentByte++;
                            currentBit = 0;
                        }
                    }
                    else
                    {
                        // Write little-endian bytes
                        int bitsCopied = 0;
                        while (bitsCopied < BitsPerPixel)
                        {
                            byte nextByte = (byte)(pixel & 0xFF);
                            _buffer[currentByte++] = nextByte;
                            pixel >>= 8;
                            bitsCopied += 8;
                        }
                    }
                }

                // Copy buffer to the image
                var rect = new Int32Rect(0, y, Width, 1);
                Bitmap.WritePixels(rect, _buffer, Stride, 0);
                y++;

            }

            Bitmap.Unlock(); // Lock() and Unlock() could be moved to the WritePixels() method.
        }


    }
}

// END-OF-FILE
