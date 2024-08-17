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
    // This class is able to extract the value of any specified pixel from a BitmapSource,
    // regardless of the pixel format. In addition, it uses extra buffering to improve performance.
    public class BitmapDataSource : DataSource
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Buffer parameters
        private readonly byte[] _buffer;
        private readonly int _bufferHeight = 0;
        private readonly int _bufferLength = 0;
        private int _firstBufferPixel = 0;
        private int _lastBufferPixel = 0;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Source parameters
        public BitmapSource Source;
        public readonly PixelFormat Format;
        public readonly int Width = 0;
        public readonly int Height = 0;
        public readonly int Stride = 0;

        // Multithreading attributes
        // ID of the main thread for the case if multithreading is used
        public int? MainThreadId = null;
        // If the buffer size is enough to fit the entire image, then
        // reading pixels becomes possible from several parallel threads.
        public readonly bool MultithreadedReadingAllowed = false;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Constructor
        public BitmapDataSource(BitmapSource theSource, int maxBufferSize)
        {
            Debug.Assert((theSource != null) && (maxBufferSize > 0));

            // Init base variables
            Source = theSource;
            Format = Source.Format;
            BitsPerFrame = Format.BitsPerPixel;
            ChannelsCount = Format.Masks.Count;
            Width = Source.PixelWidth;
            Height = Source.PixelHeight;
            Stride = (BitsPerFrame * Width + 7) >> 3; // Get full number of bytes per 1 image line

            // Check BPP. We will use UInt64 type for pixel data.
            // If you want BPP = 128, feel free to change GetPixel() function
            // and this Assert to UInt128 / 128.
            Debug.Assert(BitsPerFrame <= 64, "PixelDataSource, wrong BPP: " + BitsPerFrame.ToString());

            // Init buffer
            // Don't make the buffer too small. If the buffer height in pixels is less
            // than the maximum square width (when splitting into squares used),
            // then it will lead to a loss of performance.
            // Also, if the entire image does not fit into the buffer,
            // it becomes impossible to read pixels from parallel threads.
            _bufferHeight = maxBufferSize / Stride;
            if (_bufferHeight >= Height)
            {
                _bufferHeight = Height;
                MultithreadedReadingAllowed = true;
            }
            _buffer = new byte[_bufferHeight * Stride];
            _bufferLength = Source.PixelWidth * _bufferHeight;

            // Read the entire image
            if (MultithreadedReadingAllowed)
            {
                var rect = new Int32Rect(0, 0, Width, Height);
                Source.CopyPixels(rect, _buffer, Stride, 0);
            }

        }


        // Destructor
        /*
        ~PixelDataSource()
        {
        }
        */


        // Main method
        public UInt64 GetPixel(int pixelNumber)
        {
            // Check if the pixel is out of the buffer
            if (!MultithreadedReadingAllowed && ((pixelNumber < _firstBufferPixel) || (pixelNumber >= _lastBufferPixel)))
            {
                // If so, then update the buffer
                int y = pixelNumber / Width;
                if ((y + _bufferHeight) > Height) y = Height - _bufferHeight;

                var rect = new Int32Rect(0, y, Width, _bufferHeight);

                // Check if call is not from the main thread
                if ((MainThreadId != null) && (Thread.CurrentThread.ManagedThreadId != MainThreadId)) {
                    Application.Current.Dispatcher.Invoke(new Action(() => Source.CopyPixels(rect, _buffer, Stride, 0)));
                } 
                else
                {
                    Source.CopyPixels(rect, _buffer, Stride, 0);
                }
                _firstBufferPixel = y * Width;
                _lastBufferPixel = _firstBufferPixel + _bufferLength;
            }

            // Offset relative to the beginning of the buffer
            int bitsBeforePixel = (pixelNumber - _firstBufferPixel) * BitsPerFrame;
            int bytesBeforePixel = bitsBeforePixel >> 3;

            UInt64 pixel = 0;
            int bitsCopied = 0;


            /*
            // Reading in direct byte order (big-endian)
            // This is not our case at all, but I'll leave the code here if it ever comes in handy.

            bitsBeforePixel -= (bytesBeforePixel << 3);

            if (bitsBeforePixel > 0)
            {
                pixel = (UInt64)(_buffer[bytesBeforePixel++] & (0xFF >> bitsBeforePixel));
                bitsCopied += (8 - bitsBeforePixel);
            }

            while (bitsCopied < BitsPerPixel)
            {
                pixel <<= 8;
                pixel |= _buffer[bytesBeforePixel++];
                bitsCopied += 8;
            }

            pixel >>= (bitsCopied - BitsPerPixel);
            */


            // Reading in reverse byte order (little-endian)
            // A simplified version for the case when the not-multiple-of-byte BPP
            // can only be if it is less than a byte.
            
            if (BitsPerFrame < 8)
            {
                // BPP is not multiple of a byte
                bitsBeforePixel -= (bytesBeforePixel << 3);
                pixel = (UInt64)(_buffer[bytesBeforePixel] & (0xFF >> bitsBeforePixel));
                bitsCopied = (8 - bitsBeforePixel);
                pixel >>= (bitsCopied - BitsPerFrame);
            }
            else
            {
                // Read little-endian bytes
                while (bitsCopied < BitsPerFrame)
                {
                    UInt64 nextByte = _buffer[bytesBeforePixel++];
                    pixel |= (nextByte << bitsCopied);
                    bitsCopied += 8;
                }
            }

            return (pixel);
        }

    }

}

// END-OF-FILE
