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
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeltaComp
{

    // Image component (R/G/B/A/Grayscale channel) class.
    public class ColorComponentChannel : Channel
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Source data
        private BitmapDataSource _source;

        // Component index in pixel
        private readonly int _channelIndex = 0;

        // Component mask in pixel data
        private readonly UInt64 _mask = 0;

        // Component shift in pixel data
        private readonly int _shift = 0;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Constructor
        public ColorComponentChannel(BitmapDataSource theSource, ChannelType channelType, ChannelOrder order)
        {
            Trace.Assert(theSource != null);

            // Init base variables
            ChannelType = channelType;
            _source = theSource;
            _channelIndex = Channel.colorComponentPosition[(int)order][(int)channelType];

            // Get channel mask list
            IList<PixelFormatChannelMask> formatMaskCollection = _source.Format.Masks;
            PixelFormatChannelMask channelMask = formatMaskCollection[_channelIndex];
            IList<byte> maskBytesCollection = channelMask.Mask;

            // Get mask
            foreach (byte myByte in maskBytesCollection.Reverse())
            {
                _mask <<= 8;
                _mask |= myByte;
            }

            // Calculate the channel shift inside a pixel
            UInt64 mask = _mask;
            while ((mask & 1) == 0)
            {
                _shift++;
                mask >>= 1;
            }

            // Calculate channel bit depth
            while ((mask & 1) != 0)
            {
                BitsPerChannel++;
                mask >>= 1;
            }

            // Init UI properties
            BitDepthList = Enumerable.Range(1, BitsPerChannel).Select(i => (int)i).ToList();
            TargetBitDepth = BitsPerChannel;

        }


        // Destructor
        /*
        ~ComponentChannel()
        {
        }
        */


        // Base class method's implementation
        public override UInt32 GetOriginalValueForFrame(int frameNumber)
        {
            return ((UInt32)((_source.GetPixel(frameNumber) & _mask) >> _shift));
        }


    }
}

// END-OF-FILE
