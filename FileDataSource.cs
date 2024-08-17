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
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace DeltaComp
{
    // The class reads data (divided into frames of a specified size)
    // from a file using additional buffering.
    public class FileDataSource : DataSource
    {
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Some static constants
        private static class Constants
        {
            // Error messages
            public const string FileNotOpened = "Source file was not opened.";
        }

        // File stream
        private FileStream? _sourceFileStream = null;

        // Buffer parameters
        private readonly byte[] _buffer = null!;
        private readonly int _bufferSize = 0;
        private long _firstBufferByte = 0;
        private long _lastBufferByte = 0;

        // We will either read the file from a single thread (although not the main one)
        // or, if the whole file fits into the buffer, we will read by many streams
        // but directly from the buffer, so we do not need a lock here.
        //private ReaderWriterLock locker = new ReaderWriterLock();


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Source parameters
        public readonly string Filepath = String.Empty;
        public readonly long FileSize = 0;
        public int BytesPerFrame = 1;
        public int BytesPerChannel = 1;
        public bool IsBigEndian = true;
        public int FramesCount = 0;

        // If the buffer size is enough to fit the entire file data, then
        // reading frames becomes possible from several parallel threads.
        public readonly bool MultithreadedReadingAllowed = false;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Constructor
        public FileDataSource(string filePath, int maxBufferSize)
        {
            Debug.Assert((filePath != null) && (maxBufferSize > 0));

            try
            {
                _sourceFileStream = File.OpenRead(filePath);
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                ErrorOccurred = true;

                // Cleenup if something went wrong
                Cleanup();

                return;
            }


            FileSize = _sourceFileStream.Length;
            UpdateAttributes();

            // Init buffer
            // Don't make the buffer too small.
            // If the entire file does not fit into the buffer,
            // it becomes impossible to read data from parallel threads.
            _bufferSize = maxBufferSize;
            if (_bufferSize >= FileSize)
            {
                _bufferSize = (int)FileSize;
                MultithreadedReadingAllowed = true;
            }
            _buffer = new byte[_bufferSize];

            // Read the etire file
            if (MultithreadedReadingAllowed)
            {
                ReadData(0, 0, _bufferSize);
            }
        }


        // Destructor
        ~FileDataSource()
        {
            Cleanup();
        }


        // Update source parameters
        public void UpdateSourceParameters(int bytesPerFrame, int bytesPerChannel, bool isBigEndian)
        {
            Debug.Assert((bytesPerChannel > 0) && (bytesPerFrame >= bytesPerChannel));

            BytesPerFrame = bytesPerFrame;
            BytesPerChannel = bytesPerChannel;
            IsBigEndian = isBigEndian;
            UpdateAttributes();
        }


        // Main method
        public UInt32 GetFrameForChannel(int frameNumber, int channelIndex)
        {
            // Check if the frame is out of the buffer
            long position = frameNumber * BytesPerFrame + channelIndex * BytesPerChannel;
            if (!MultithreadedReadingAllowed && ((position < _firstBufferByte) || (position >= _lastBufferByte)))
            {
                // If so, then update the buffer
                // Calculate which part of the file will now be placed into the buffer
                long newFirstBufferByte, newLastBufferByte;
                int quarterBuffer = _bufferSize >> 2;
                if (position > quarterBuffer)
                {
                    newFirstBufferByte = position - quarterBuffer;
                }
                else
                {
                    newFirstBufferByte = 0;
                }

                newLastBufferByte = newFirstBufferByte + _bufferSize;
                if (newLastBufferByte > FileSize)
                {
                    newFirstBufferByte = FileSize - _bufferSize;
                    newLastBufferByte = FileSize;
                }

                // Try to save some common part of the buffer to minimize file reading
                long readPosition = newFirstBufferByte;
                int readOffset = 0;
                int readAmount = _bufferSize;

                // If we moved forward in the file
                if (newFirstBufferByte > _firstBufferByte)
                {
                    int commonBlockSize = (int)(_lastBufferByte - newFirstBufferByte);
                    if (commonBlockSize > 0)
                    {
                        Buffer.BlockCopy(_buffer, (int)(newFirstBufferByte - _firstBufferByte), _buffer, 0, commonBlockSize);
                        readPosition = _lastBufferByte;
                        readOffset = commonBlockSize;
                        readAmount -= commonBlockSize;
                    }
                }
                // or if we moved back in the file
                else if (newFirstBufferByte < _firstBufferByte)
                {
                    int commonBlockSize = (int)(newLastBufferByte - _firstBufferByte);
                    if (commonBlockSize > 0)
                    {
                        Buffer.BlockCopy(_buffer, 0, _buffer, (int)(_lastBufferByte - newLastBufferByte), commonBlockSize);
                        readAmount -= commonBlockSize;
                    }
                }

                _firstBufferByte = newFirstBufferByte;
                _lastBufferByte = newLastBufferByte;

                ReadData(readPosition, readOffset, readAmount);
            }


            // Offset relative to the beginning of the buffer
            int bytesBeforeFrame = (int)(position - _firstBufferByte);

            UInt32 frame = 0;
            int bytesCopied = 0;

            // Let's read the bytes as is, and then we'll deal with the endianness in each channel
            while (bytesCopied < BytesPerChannel)
            {
                frame <<= 8;
                frame |= _buffer[bytesBeforeFrame++];
                bytesCopied++;
            }


            if (!IsBigEndian)
            {
                switch (BytesPerChannel)
                {
                    case 2:
                        frame = (((frame & 0xFF) << 8) | ((frame >> 8) & 0xFF));
                        break;
                    case 3:
                        frame = (((frame & 0xFF) << 16) | (frame & 0xFF00) | ((frame >> 16) & 0xFF));
                        break;
                    case 4:
                        frame = (((frame & 0xFF) << 24) | ((frame & 0xFF00) << 8) | 
                            ((frame >> 8) & 0xFF00) | ((frame >> 24) & 0xFF));
                        break;
                    default:
                        break;
                }
            }

            return (frame);
        }


        // Calculate source attributes after updating
        private void UpdateAttributes()
        {
            BitsPerFrame = BytesPerFrame << 3;
            ChannelsCount = BytesPerFrame / BytesPerChannel;
            FramesCount = (int)(FileSize / BytesPerFrame);
        }


        // Read data from file
        private void ReadData(long filePosition, int bufferOffset, int count)
        {
            if (_sourceFileStream == null)
            {
                LastErrorMessage = Constants.FileNotOpened;
                ErrorOccurred = true;
                return;
            }

            try
            {
                //We don't need no locker, let the motherfucker crash...
                //locker.AcquireWriterLock(Constants.WriterLockTimeout);

                _sourceFileStream.Position = filePosition;
                _sourceFileStream.Read(_buffer, bufferOffset, count);

            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                ErrorOccurred = true;
                //Cleanup(); // Let's clean up later to save the real error message
            }

            // Crash motherfucker, crash...
            /*
            finally
            {
                locker.ReleaseWriterLock();
            }
            */
        }


        // Clean the file stream up
        private void Cleanup()
        {
            if (_sourceFileStream != null)
            {
                _sourceFileStream.Close();
                _sourceFileStream.Dispose();
                _sourceFileStream = null;
            }
        }

    }
}

// END-OF-FILE
