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
using System.Diagnostics;
using System.Windows.Controls;
using System.Reflection;

namespace DeltaComp
{
    // The class generates a text representation of streaming data
    // divided into frames and channels in each frame.
    // The text is displayed in the textbox.
    public class FilePreviewDestination : PreviewDestination
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Some static constants
        private static class Constants
        {
            // The approximate line height for the 12pt Courier New font.
            public const double DefaultLineHeight = 13.6d;
        }

        // Source and destination
        private readonly List<Channel> _sourceChannels;
        private readonly FileDataSource _fileDataSource;
        private readonly TextBox _textBox;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        
        // Constructor
        public FilePreviewDestination(List<Channel> sourceChannels, FileDataSource fileDataSource, TextBox textBox)
        {
            Trace.Assert((sourceChannels != null) && (textBox != null));

            // Init base variables
            _sourceChannels = sourceChannels;
            _fileDataSource = fileDataSource;
            _textBox = textBox;
            //UpdatePreview(); // There's no need to do it immediately
        }


        // Destructor
        /*
        ~PreviewDataDestination()
        {
        }
        */


        // Update the preview TextBox according to the bit depth of channels.
        public override void UpdatePreview()
        {
            _textBox.Text = "";
            int currentFrame = 0;

            // Since the LineHeight is a private method of TextBox,
            // let's apply some undocumented magic...
            MethodInfo? method = _textBox.GetType().GetMethod("GetLineHeight", BindingFlags.NonPublic | BindingFlags.Instance);
            object? value = method?.Invoke(_textBox, null);
            double lineHeight = (value != null) ? (double)value : Constants.DefaultLineHeight;
            int visibleLines = (int)Math.Truncate(_textBox.ActualHeight / lineHeight);
            int totalFrames = _fileDataSource.FramesCount;

            // Fill our preview TextBox only to the height of the visible lines
            while ((currentFrame < totalFrames) && (_textBox.LineCount < visibleLines))
            {
                string frameString = "";
                for (int i = 0; i < _sourceChannels.Count; i++)
                {
                    Channel channel = _sourceChannels[i];
                    string channelValuePreviewFormat;

                    if (channel.TargetBitDepth <= 8)
                    {
                        channelValuePreviewFormat = "0x{0:X2}";
                    }
                    else if (channel.TargetBitDepth <= 16)
                    {
                        channelValuePreviewFormat = "0x{0:X4}";
                    }
                    else if (channel.TargetBitDepth <= 24)
                    {
                        channelValuePreviewFormat = "0x{0:X6}";
                    }
                    else
                    {
                        channelValuePreviewFormat = "0x{0:X8}";
                    }

                    if (i != 0) frameString += "\u00A0"; // "\xA0" - non-breaking space
                    frameString += String.Format(channelValuePreviewFormat, channel.GetTargetValueForFrame(currentFrame));

                }

                frameString += ";  "; // Regular space for wrapping by frames (not by channels)
                _textBox.AppendText(frameString);

                currentFrame++;
            }
            
            if (currentFrame < totalFrames) _textBox.AppendText("...");

        }
    }
}

// END-OF-FILE
