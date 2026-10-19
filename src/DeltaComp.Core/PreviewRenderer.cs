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
using System.Text;
using System.Threading.Tasks;

namespace DeltaComp
{
    // Builds previews that show how the data will look at the selected (target) bit depths.
    public static class PreviewRenderer
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Image preview
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Render the channels into 8-bit BGRA pixels (not premultiplied).
        // A Grayscale channel is shown in all three color components; missing alpha is opaque.
        public static void RenderBgra32(IReadOnlyList<Channel> channels, int width, int height, byte[] destination, int stride)
        {
            if (destination.Length < stride * height) throw new ArgumentException("Destination buffer is too small.", nameof(destination));

            Channel?[] map = new Channel?[5];
            foreach (Channel c in channels)
            {
                if ((int)c.ChannelType < map.Length) map[(int)c.ChannelType] = c;
            }

            Channel? red = map[(int)ChannelType.Red] ?? map[(int)ChannelType.Grayscale];
            Channel? green = map[(int)ChannelType.Green] ?? map[(int)ChannelType.Grayscale];
            Channel? blue = map[(int)ChannelType.Blue] ?? map[(int)ChannelType.Grayscale];
            Channel? alpha = map[(int)ChannelType.Alpha];

            Parallel.For(0, height, y =>
            {
                int frame = y * width;
                int o = y * stride;
                for (int x = 0; x < width; x++, frame++, o += 4)
                {
                    byte r = Preview8(red, frame, 0);
                    destination[o] = (blue == red) ? r : Preview8(blue, frame, 0);
                    destination[o + 1] = (green == red) ? r : Preview8(green, frame, 0);
                    destination[o + 2] = r;
                    destination[o + 3] = Preview8(alpha, frame, 255);
                }
            });
        }


        // Preview value of a channel scaled to 8 bits
        private static byte Preview8(Channel? channel, int frame, byte defaultValue)
        {
            return (channel != null) ? (byte)channel.GetPreviewValueForFrame(frame, 8) : defaultValue;
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Data preview
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Text representation of streaming data divided into frames and channels in each frame:
        // channel values are hexadecimal numbers at the target bit depth, frames are separated by "; ".
        public static string BuildDataText(IReadOnlyList<Channel> channels, int framesCount, int maxFrames)
        {
            StringBuilder builder = new StringBuilder();
            int frames = Math.Min(framesCount, maxFrames);

            string[] formats = new string[channels.Count];
            for (int i = 0; i < channels.Count; i++)
            {
                int digits = ((channels[i].TargetBitDepth + 7) >> 3) * 2;
                formats[i] = "0x{0:X" + digits.ToString() + "}";
            }

            for (int frame = 0; frame < frames; frame++)
            {
                for (int i = 0; i < channels.Count; i++)
                {
                    if (i != 0) builder.Append(' '); // Non-breaking space, so a frame is never wrapped
                    builder.AppendFormat(formats[i], channels[i].GetTargetValueForFrame(frame));
                }
                builder.Append(";  "); // Regular space for wrapping by frames (not by channels)
            }

            if (frames < framesCount) builder.Append("...");

            return builder.ToString();
        }

    }
}

// END-OF-FILE
