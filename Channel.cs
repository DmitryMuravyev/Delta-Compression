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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeltaComp
{
    // Main types of channels (later the list can be completed with audio channels, etc.)
    // It is important that the Red, Green and Blue channels go exactly in this sequence starting from 0
    // (e.g. see GrayscaleMixerChannel Class implementation).
    public enum ChannelType
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Alpha = 3,
        Grayscale = 4,
        Data = 5,
        Unknown = 1000
    }


    // Сhannel order in pixel
    public enum ChannelOrder
    {
        ChannelOrderBGRA = 0,
        ChannelOrderRGBA = 1
    }


    // An abstract class representing an image/audio/data channel (component).
    public abstract class Channel
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public attributes/methods
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Сhannel indexes in pixel depending on channel order.
        // They must match the values declared in ChannelOrder and ChannelType enumerated types.
        public static readonly int[][] colorComponentPosition = [ [ 2, 1, 0, 3, 0, 0, 0 ],
                                                                  [ 0, 1, 2, 3, 0, 0, 0 ] ];

        // Channel bit depth
        public int BitsPerChannel = 0;


        // Get the maximum number that can be written to a specified number of bits
        public static double GetMaxNumberForBitDepth(int bitDepth)
        {
            int maxNumber = (1 << bitDepth) - 1;
            return ((double)maxNumber);
        }


        // Channel type attribute
        public ChannelType ChannelType { get; set; } = ChannelType.Unknown;


        // The name of the channel derived from its type.
        public string Name
        {
            get
            {
                return (ChannelType.ToString());
            }
        }


        // The list of acceptable bit depth for this channel.
        public List<int>? BitDepthList { get; set; } = null;


        // Target bit depth to which the data format should be reduced.
        public int TargetBitDepth { get; set; } = 0;


        // An abstract method for getting channel value for a given pixel/frame/symbol, etc.
        public abstract UInt32 GetOriginalValueForFrame(int frameNumber);


        // The same, but returns the double result 0.0 - 1.0
        public virtual double GetFloatValueForFrame(int frameNumber)
        {
            return ((double)GetOriginalValueForFrame(frameNumber) /
                           GetMaxNumberForBitDepth(BitsPerChannel));
        }


        // The same, but returns scaled to the target bit depth result.
        public virtual UInt32 GetTargetValueForFrame(int frameNumber)
        {
            if (BitsPerChannel == TargetBitDepth)
            {
                return (GetOriginalValueForFrame(frameNumber));
            }

            return ((UInt32)Math.Round(GetFloatValueForFrame(frameNumber) * 
                                       GetMaxNumberForBitDepth(TargetBitDepth)));
        }


        // The same, but returns scaled to any specified bit depth result.
        public virtual UInt32 GetCustomScaledValueForFrame(int frameNumber, int bitDepth)
        {
            if (BitsPerChannel == bitDepth)
            {
                return (GetOriginalValueForFrame(frameNumber));
            }

            return ((UInt32)Math.Round(GetFloatValueForFrame(frameNumber) * 
                                       GetMaxNumberForBitDepth(bitDepth)));
        }


        // Returns the result for previewing the pixel (audio sample or whatever) 
        // value as it should look at the Target bit depth.
        // The result is scaled to any specified bit depth which is selected for the preview image.
        public virtual UInt32 GetPreviewValueForFrame(int frameNumber, int bitDepth)
        {
            if (TargetBitDepth == bitDepth)
            {
                return (GetTargetValueForFrame(frameNumber));
            }

            double value = ((double)GetTargetValueForFrame(frameNumber) /
                                  GetMaxNumberForBitDepth(TargetBitDepth) *
                                  GetMaxNumberForBitDepth(bitDepth));

            return ((UInt32)Math.Round(value));
        }

    }
}

// END-OF-FILE
