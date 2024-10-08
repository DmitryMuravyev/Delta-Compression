﻿/*

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
    // The class is designed to represent raw file data as a stream
    // of alternating channels (color components/audio channels etc.)
    public class FileDataChannel : Channel
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Source data
        private FileDataSource _source;

        // Channel index in frame
        private readonly int _channelIndex = 0;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Constructor
        public FileDataChannel(FileDataSource theSource, int index)
        {
            Trace.Assert((theSource != null) && (index >= 0));

            // Init base variables
            _source = theSource;
            _channelIndex = index;
            ChannelType = ChannelType.Data;
            UpdateBitDepth();
        }


        // Destructor
        /*
        ~CustomDataChannel()
        {
        }
        */


        // Calculate channel bit depth
        public void UpdateBitDepth()
        {
            BitsPerChannel = _source.BytesPerChannel << 3;
            BitDepthList = new List<int> { BitsPerChannel };
            TargetBitDepth = BitsPerChannel;
        }


        // Base class method's implementation
        public override UInt32 GetOriginalValueForFrame(int frameNumber)
        {
            return (_source.GetFrameForChannel(frameNumber, _channelIndex));
        }


        // And another one
        public override UInt32 GetTargetValueForFrame(int frameNumber)
        {
            return (GetOriginalValueForFrame(frameNumber));
        }

    }
}

// END-OF-FILE
