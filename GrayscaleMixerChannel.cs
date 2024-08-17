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

namespace DeltaComp
{
    // A class that converts three R/G/B channels to a Grayscale channel
    // (I didn't like how FormatConvertedBitmap does it).
    internal class GrayscaleMixerChannel : Channel
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Grayscale conversion coefficients (I prefer the first set, but you
        // can use the second one, which takes into account gamma compression).
        // Coefficients declared in the same order as in ChannelType enum (Channel class).
        private static readonly double[] _coefficients = [ 0.2126f, 0.7152f, 0.0722f ];
        //private static readonly double[] _coefficients = [ 0.299f, 0.587f, 0.114f ];

        // List of source RGB channels
        private readonly List<ColorComponentChannel> _sourceChannels;

        // Target Grayscale pixel format
        private readonly PixelFormat _targetPixelFormat;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Constructor
        public GrayscaleMixerChannel(List<ColorComponentChannel> sourceChannels, PixelFormat targetPixelFormat)
        {
            // Some Assertions to check if all ok.
            Trace.Assert(sourceChannels.Count >= 3, "GrayscaleMixerChannel, wrong channels count: " + 
                                                     sourceChannels.Count.ToString());

            Trace.Assert( (targetPixelFormat == PixelFormats.BlackWhite) ||
                          (targetPixelFormat == PixelFormats.Gray2) ||
                          (targetPixelFormat == PixelFormats.Gray4) ||
                          (targetPixelFormat == PixelFormats.Gray8) ||
                          (targetPixelFormat == PixelFormats.Gray16),
                          "GrayscaleMixerChannel, wrong pixel format: " + targetPixelFormat.ToString());

            // Init base variables
            ChannelType = ChannelType.Grayscale;
            _sourceChannels = sourceChannels;
            _targetPixelFormat = targetPixelFormat;
            BitsPerChannel = _targetPixelFormat.BitsPerPixel; // We can use BitsPerPixel attribute because
                                                              // there's only 1 channel in Grayscale.

            // Init UI properties
            BitDepthList = Enumerable.Range(1, BitsPerChannel).Select(i => (int)i).ToList();
            TargetBitDepth = BitsPerChannel;
        }


        // Destructor
        /*
        ~GrayscaleMixerChannel()
        {
        }
        */


        // Base class method's implementation
        public override UInt32 GetOriginalValueForFrame(int frameNumber)
        {
            double value = 0;
            foreach (ColorComponentChannel cc in _sourceChannels)
            {
                value += (_coefficients[(int)cc.ChannelType] * cc.GetFloatValueForFrame(frameNumber));
            }

            return ((UInt32)Math.Round(value * GetMaxNumberForBitDepth(BitsPerChannel)));
        }


    }
}

// END-OF-FILE
