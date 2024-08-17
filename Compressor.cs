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


/*

The description in the code body will be quite brief, so if you want more detailed information,
then please refer to the following links, where I talk about how this compression method works,
about problems that arose during the development and how I solved them:

Boosty (Russian):
Sprint 1 - https://boosty.to/muravyev/posts/9241e5a2-a490-4530-a376-e9cae0f9e9bf?share=post_link
Sprint 2 - https://boosty.to/muravyev/posts/dfdd0b1d-f27d-446a-ab38-d55cbc7cd580?share=post_link
Sprint 3 - https://boosty.to/muravyev/posts/bc58a9af-f075-4004-a98e-6535edab9925?share=post_link

Patreon (Eng subs):
Sprint 1 - https://www.patreon.com/posts/delta-encoding-1-106850934
Sprint 2 - https://www.patreon.com/posts/delta-encoding-2-108056837
Sprint 3 - https://www.patreon.com/posts/delta-encoding-3-108236064

*/


////////////////////////////////////////////////////////////////////////////////////////////////////
// Symbol definitions
////////////////////////////////////////////////////////////////////////////////////////////////////

// Depending on the microcontroller firmware compiler optimization parameters,
// this option may be more CPU-efficient.
// See the CompressBlock() method below.
#define SEQUENTIAL_METHODS_AND_INITIAL_VALUES_DECLARATION


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using System.Windows.Media;
using System.Runtime.Intrinsics.X86;
using System.Windows.Documents;


namespace DeltaComp
{

    // Delta calculation options (please refer to the links in the header).
    public enum DeltaOption
    {
        FixedWindowOnly = 0,
        AdaptiveFloatingWindowOnly = 1,
        FixedWindowFirst = 2,
        AdaptiveFloatingWindowFirst = 3
    }

    // Stages of the compression process
    public enum CompressionStage
    {
        None = 0,
        Preparation = 1,
        Analysis = 2,
        Compression = 3,
        Done = 4,
        Cancelled = 5,
        Failed = 6
    }


    // This class is designed for data compression using delta encoding.
    public class Compressor
    {

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private constants/attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Some static constants
        private static class Constants
        {
            public const byte DefaultBlockSizeBits = 8;
            public const byte DefaultWidthOfSquare = 1;
            public const int WaitForTasksSleepDuration = 100;
            public const int WaitForBestBlockReadyEventTimeout = 1000;
            public const double MinProgressIcrease = 0.1d;
        }


        // ATTENTION!!!
        // If we want to use a block length of 32 bits, then we need to change the type
        // of all variables associated with the length to UInt64! If we use the UInt32 type,
        // then the maximum block length field should be no more than 31 bits!

        // The structure defining a single compression method
        private struct Method
        {
            public UInt32 Length;
            public byte Index;
            public UInt32 InitialValue;
        }

        // A structure defining a single block combining compression methods for all channels
        private struct Block
        {
            public UInt32 StartFrame;
            public UInt32 Length;
            public byte[] Methods;
            public UInt32[] InitialValues; // 0 for uncompressed data
            public double Efficiency;
        }

        // This one is used to find the best blocks and analyze them in parallel
        private struct DistributedBlock
        {
            public Block Block;
            public volatile bool Ready;
        }

        // Source data channels list
        private readonly List<Channel> _sourceChannels;

        // Compression parameters
        private byte _headerBits; // We'll set it in the Constructor
        private byte _blockSizeBits; // We'll set it in the Constructor
        private UInt32 _maxBlockSize = 0;
        private DeltaOption _deltaCalculationOption = DeltaOption.FixedWindowFirst;
        private byte _widthOfSquare = Constants.DefaultWidthOfSquare;
        private UInt16 _squaresPerImageWidth = 0;
        private UInt16 _squareSize = Constants.DefaultWidthOfSquare;
        private readonly bool _bestBlocksArrayUsed = false;

        // Compression global variables
        private volatile DistributedBlock[] _bestBlocks = null!;
        private UInt128 _bitsBuffer = 0;
        private byte _bitsBufferCount = 0;

        // Handling multithreading and workflow operations
        private EventWaitHandle _bestBlocksBufferEWH = null!;
        private CancellationToken? _cancellationToken = null;
        private bool _waitForTasksFinished = false;
        private bool _fallIntoErrorRequested = false;

        // Delegate methods
        private readonly WriteCompressedByteDelegate _writeCompressedByteDelegate;
        private readonly UpdateProgressDelegate _updateProgressDelegate;


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public attributes/variables
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        // Source data parameters
        public byte ChannelsCount = 0;
        public byte BitsPerFrame = 0;
        public byte[] BitsPerChannel;
        public readonly UInt32 TotalFrames = 0;
        public readonly UInt16 ImageWidth = 0;
        public readonly UInt16 ImageHeight = 0;


        // Compression parameters
        public byte BlockSizeBits
        {
            get { return _blockSizeBits; }
            set
            {
                _blockSizeBits = value;
                _maxBlockSize = (UInt32)Math.Pow(2, _blockSizeBits);
                CalculateHeaderBits();
            }
        }

        public byte[] BitsPerMethodDeclaration;

        public DeltaOption DeltaCalculationOption
        {
            get { return _deltaCalculationOption; }
            set
            {
                _deltaCalculationOption = value;
                CalculateHeaderBits();
            }
        }

        public bool UseBruteForceBestBlockSearch = true;
        public bool SplitToSquares = false;
        public byte WidthOfSquare
        {
            get { return _widthOfSquare; }
            set
            {
                _widthOfSquare = value;
                _squaresPerImageWidth = (UInt16)(ImageWidth / _widthOfSquare);
                _squareSize = (UInt16)(_widthOfSquare * _widthOfSquare);
            }
        }

        // Current status
        public CompressionStage Stage = CompressionStage.None;
        public double Progress = 0.0d;

        // Delegate methods
        public delegate void WriteCompressedByteDelegate(byte compressedByte);
        public delegate void UpdateProgressDelegate(CompressionStage stage, double progress);


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Implementation
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Public methods

        // Constructor for initialization in the context of an image
        public Compressor(List<Channel> channels, UInt16 imageWidth, UInt16 imageHeight, bool multithreadedRead,
             WriteCompressedByteDelegate writeCompressedByteDelegate, UpdateProgressDelegate updateProgressDelegate)
        {
            _sourceChannels = channels;

            ChannelsUpdated();

            ImageWidth = imageWidth;
            ImageHeight = imageHeight;

            TotalFrames = (UInt32)(ImageWidth * ImageHeight);

            if (multithreadedRead)
            {
                _bestBlocksArrayUsed = true;
                _bestBlocks = new DistributedBlock[TotalFrames];
                for(UInt32 i = 0; i < TotalFrames; i++) _bestBlocks[i].Ready = false;
                _bestBlocksBufferEWH = new EventWaitHandle(false, EventResetMode.AutoReset);
            }
            
            BlockSizeBits = Constants.DefaultBlockSizeBits;

            _writeCompressedByteDelegate = writeCompressedByteDelegate;
            _updateProgressDelegate = updateProgressDelegate;
        }


        // Constructor for initialization in the context of a custom data
        public Compressor(List<Channel> channels, UInt32 totalFrames, bool multithreadedRead,
             WriteCompressedByteDelegate writeCompressedByteDelegate, UpdateProgressDelegate updateProgressDelegate)
        {
            _sourceChannels = channels;

            ChannelsUpdated();

            TotalFrames = totalFrames;
            
            if (multithreadedRead)
            {
                _bestBlocksArrayUsed = true;
                _bestBlocks = new DistributedBlock[TotalFrames];
                for (UInt32 i = 0; i < TotalFrames; i++) _bestBlocks[i].Ready = false;
                _bestBlocksBufferEWH = new EventWaitHandle(false, EventResetMode.AutoReset);
            }
            
            BlockSizeBits = Constants.DefaultBlockSizeBits;

            _writeCompressedByteDelegate = writeCompressedByteDelegate;
            _updateProgressDelegate = updateProgressDelegate;
        }


        // Update the composition of channels and their target bit depth
        // (in case if something changed after initialization).
        [MemberNotNull(nameof(ChannelsCount)), MemberNotNull(nameof(BitsPerMethodDeclaration)), 
            MemberNotNull(nameof(BitsPerChannel)), MemberNotNull(nameof(BitsPerFrame))]
        public void ChannelsUpdated()
        {
            ChannelsCount = (byte)_sourceChannels.Count;

            BitsPerMethodDeclaration = new byte[ChannelsCount];
            BitsPerChannel = new byte[ChannelsCount];
            BitsPerFrame = 0;

            for (byte c = 0; c < ChannelsCount; c++)
            {
                byte currentChannelTargetBitDepth = (byte)_sourceChannels[c].TargetBitDepth;
                BitsPerChannel[c] = currentChannelTargetBitDepth;
                BitsPerFrame += currentChannelTargetBitDepth;
            }
        }


        // Since, in order to improve performance, we do not control
        // read/write errors from the compression class, so external
        // modules should be responsible for this,
        // which may request an error termination.
        public void RequestFallingIntoError()
        {
            _fallIntoErrorRequested = true;
        }


        public void Compress(CancellationToken? token)
        {
            // Some console outupt for the debugging purposes
            /*
            Debug.WriteLine("ChannelsCount: " + ChannelsCount.ToString());
            Debug.WriteLine("BitsPerFrame: " + BitsPerFrame.ToString());
            Debug.WriteLine("ImageWidth: " + ImageWidth.ToString());
            Debug.WriteLine("ImageHeight: " + ImageHeight.ToString());
            Debug.WriteLine("TotalFrames: " + TotalFrames.ToString());
            Debug.WriteLine("_bestBlocksArrayUsed: " + _bestBlocksArrayUsed.ToString());

            Debug.WriteLine("BlockSizeBits: " + BlockSizeBits.ToString());
            Debug.WriteLine("DeltaOption: " + DeltaCalculationOption.ToString());
            Debug.WriteLine("UseBruteForceBestBlockSearch: " + UseBruteForceBestBlockSearch.ToString());
            Debug.WriteLine("SplitToSquares: " + SplitToSquares.ToString());
            Debug.WriteLine("WidthOfSquare: " + WidthOfSquare.ToString());
            Debug.WriteLine("Header bits: {0}", _headerBits);
            */

            _cancellationToken = token;
            _cancellationToken?.ThrowIfCancellationRequested();

            // Let's start from the Preparation stage
            UpdateStatus(CompressionStage.Preparation, 0);

            // I used this while optimizing the performance of the algorithms
            //var watch = Stopwatch.StartNew();

            // Multithreaded search for the best blocks
            if (_bestBlocksArrayUsed)
            {
                // We could use Parallel.For for this purpose...

                /*
                UInt32 framesDone = 0;
                List<Task> tasks = new List<Task>();

                ParallelOptions parallelOptions = new ParallelOptions();
                // We can limit the number of threads, but we don't have to:
                //parallelOptions.MaxDegreeOfParallelism = Environment.ProcessorCount;

                Parallel.For(0, TotalFrames, parallelOptions, i =>
                {
                    FindBestBlock((UInt32)i);

                    //Interlocked.Increment(ref framesDone);
                    framesDone++;
                    UpdateStatus(CompressionStage.Preparation, framesDone);
                });
                */


                // But it is much more interesting (and a little faster) to run
                // an async multithreaded search procedure and start building a chain of blocks in parallel!
                _waitForTasksFinished = true;
                FillBestBlocksArray();

            }


            // Сheck if the last operation was canceled or an error occurred
            CheckCompressionCanceled();
            if (CheckCompressionError()) return;


            // Process data analysis (building a chain of blocks).
            UpdateStatus(CompressionStage.Analysis, 0);

            var mainBlockChain = SmartBuildBlocks(0, TotalFrames);

            // Compressing stage
            UpdateStatus(CompressionStage.Compression, 0);

            for (int i = 0; i < mainBlockChain.Count; i++)
            {
                // First, check whether the compression cancellation has been requested
                CheckCompressionCanceled();
                if (CheckCompressionError()) return; // Or some read/write error happened

                // Use this for debugging purposes
                /*
                for (int j = 0; j < ChannelsCount; j++)
                {
                    bool afw = false;
                    byte realMethod = GetRealMethod(mainBlockChain[i].Methods[j], BitsPerChannel[j], ref afw);
                    Debug.Write(String.Format("{0}{1}, {3}, ", afw ? 'A' : 'F', realMethod, 
                        mainBlockChain[i].Methods[j], mainBlockChain[i].InitialValues[j]));
                }
                Debug.WriteLine("Start: {0}, End: {1}, Length: {2}", mainBlockChain[i].StartFrame, 
                    mainBlockChain[i].StartFrame + mainBlockChain[i].Length, mainBlockChain[i].Length);
                */

                CompressBlock(mainBlockChain[i]);
                UpdateStatus(CompressionStage.Compression, mainBlockChain[i].StartFrame + mainBlockChain[i].Length);
            }

            // Check if some read/write error happened
            if (CheckCompressionError()) return;

            // Flush the bits buffer
            FinalizeCompressedData();

            // Performance check
            //Debug.WriteLine("\nTotal duration: {0:0.00}s", (double)watch.ElapsedMilliseconds / 1000.0d);

            // Done!
            UpdateStatus(CompressionStage.Done, TotalFrames);
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Private methods
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Auxiliary functions

        // Check if cancellation was requested and throw exception if so
        private void CheckCompressionCanceled()
        {
            if ((_cancellationToken != null) && _cancellationToken.Value.IsCancellationRequested)
            {
                UpdateStatus(CompressionStage.Cancelled, 0);

                // Let's wait until the tasks of searching for the best blocks are finished
                while (_waitForTasksFinished)
                {
                    Thread.Sleep(Constants.WaitForTasksSleepDuration);
                }
                _cancellationToken?.ThrowIfCancellationRequested();
            }
        }


        private bool CheckCompressionError()
        {
            if (_fallIntoErrorRequested)
            {
                UpdateStatus(CompressionStage.Failed, 0);
                return (true);
            }
            return (false);
        }


        // Calculate the size of the header fields in bits
        private void CalculateHeaderBits()
        {
            // The header of the Method contains only its description w/o any real data

            _headerBits = _blockSizeBits;
            for (byte c = 0; c < ChannelsCount; c++)
            {
                BitsPerMethodDeclaration[c] = 0;
                byte currentChannelMethodsCount = (byte)(GetMethodsCount(c) - 1);
                do
                {
                    BitsPerMethodDeclaration[c]++;
                }
                while ((currentChannelMethodsCount >>= 1) > 0);

                _headerBits += BitsPerMethodDeclaration[c];
            }
        }


        // Get the number of methods depending on the delta calculation options used
        private byte GetMethodsCount(byte channel)
        {
            if ((DeltaCalculationOption == DeltaOption.FixedWindowFirst) || 
                (DeltaCalculationOption == DeltaOption.AdaptiveFloatingWindowFirst))
            {
                return (byte)(BitsPerChannel[channel] * 2);
            } else
            {
                return (byte)(BitsPerChannel[channel] + 1);
            }
        }


        // Get the real method (windows width) and delta option
        // depending on the delta calculation options used
        // (please refer to the links in the header).
        private byte GetRealMethod(byte method, byte bitDepth, ref bool afw)
        {
            afw = false;

            if (method > bitDepth)
            {
                method -= bitDepth;
                if (DeltaCalculationOption == DeltaOption.FixedWindowFirst) afw = true;
            }
            else if ((method > 0) && (method < bitDepth))
            {
                if ((DeltaCalculationOption == DeltaOption.AdaptiveFloatingWindowOnly) ||
                    (DeltaCalculationOption == DeltaOption.AdaptiveFloatingWindowFirst)) afw = true;
            }
            return (method);
        }


        // Update data analysis progress
        private void UpdateStatus(CompressionStage currentStage, UInt32 frameProgress)
        {
            double newProgress = (double)frameProgress * 100.0d / (double)TotalFrames;
            if ((Stage != currentStage) || ((newProgress - Progress) > Constants.MinProgressIcrease) || (frameProgress == TotalFrames))
            {
                Stage = currentStage;
                if ((currentStage != CompressionStage.Cancelled) &&
                    (currentStage != CompressionStage.Failed)) Progress = newProgress;
                if (_updateProgressDelegate != null)
                {
                    // Since the method will update the UI, we have to call it from the main thread
                    Application.Current.Dispatcher.Invoke(new Action(() => _updateProgressDelegate(Stage, Progress)));
                }
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Operations with methods and blocks

        // Run a multithreaded search for the most effective blocks
        private async void FillBestBlocksArray()
        {
            List<Task> tasks = new List<Task>();
            for (UInt32 i = 0; i < TotalFrames; i++)
            {
                UInt32 position = i;
                tasks.Add(Task.Run(() => { FindBestBlock(position); }));

                // Let's limit the number of parallel tasks by the number of processors
                if (tasks.Count >= Environment.ProcessorCount)
                {
                    tasks.Remove(await Task.WhenAny(tasks.ToArray()));
                }

                // And we have to check if cancellation was requested
                if ((_fallIntoErrorRequested) || 
                    ((_cancellationToken != null) && _cancellationToken.Value.IsCancellationRequested)) break;
            }
            await Task.WhenAll(tasks.ToArray());
            _waitForTasksFinished = false;
        }


        // Build blocks for a frames range using serial efficiency analysis.
        // This one is a little bit complicated, so I'm referring you once again
        // to the links at the top of file.
        private List<Block> SmartBuildBlocks(UInt32 startFrame, UInt32 boundary)
        {
            var mainBlockChain = new List<Block>();
            var alterBlockChain = new List<Block>();

            double alterBlockChainEfficiency = double.MinValue;
            UInt32 currentPosition = startFrame;
            Block mainBlock = new Block() { StartFrame = 0, Length = 0, Efficiency = double.MinValue };
            Block alterBlock = mainBlock;
            bool lastBlockFinished = true;
            bool alterBlockFound = false;

            while (currentPosition < boundary)
            {
                // First, check whether the compression cancellation has been requested
                CheckCompressionCanceled();

                // Instead of searching for the best block every time
                //Block currentBlock = GetBestBlock(currentPosition, boundary);

                // We will better use the best blocks prepared earlier (if any)
                // Single-threaded version
                //Block currentBlock = ((boundary == TotalFrames) && _bestBlocksArrayUsed) ? 
                //    _bestBlocks[currentPosition].Block : GetBestBlock(currentPosition, boundary);

                // Multithreaded version
                Block currentBlock;
                if ((boundary == TotalFrames) && (_bestBlocksArrayUsed))
                {
                    while (!_bestBlocks[currentPosition].Ready)
                    {
                        _bestBlocksBufferEWH.WaitOne(Constants.WaitForBestBlockReadyEventTimeout);
                    }
                    currentBlock = _bestBlocks[currentPosition].Block;
                }
                else
                {
                    currentBlock = GetBestBlock(currentPosition, boundary);
                }


                if (lastBlockFinished)
                {
                    mainBlock = currentBlock;
                    alterBlockChainEfficiency = currentBlock.Efficiency;
                    currentPosition++;
                    lastBlockFinished = false;
                }
                else
                if (mainBlock.Efficiency < currentBlock.Efficiency)
                {
                    alterBlockChain.Clear();

                    Block mainBlockHead = mainBlock;
                    mainBlockHead.Length = currentPosition - mainBlock.StartFrame;
                    alterBlockChain.Add(mainBlockHead);
                    alterBlockChain.Add(currentBlock);

                    if ((mainBlock.StartFrame + mainBlock.Length) > (currentBlock.StartFrame + currentBlock.Length))
                    {
                        Block mainBlockTail = mainBlock;
                        mainBlockTail.StartFrame = currentBlock.StartFrame + currentBlock.Length;
                        mainBlockTail.Length = mainBlock.StartFrame + mainBlock.Length - mainBlockTail.StartFrame;
                        alterBlockChain.Add(mainBlockTail);
                        /*
                        // Searching for a new block instead of the tail of the base block is a bad idea
                        Block bestTailBlock = GetBestBlock(currentBlock.StartFrame + currentBlock.Length, 
                            mainBlock.StartFrame + mainBlock.Length);
                        alterBlockChain.Add(bestTailBlock);
                        */
                    }
                    else
                    {   // This option also makes things worse.
                        //currentBlock.Length = mainBlock.StartFrame + mainBlock.Length - currentBlock.StartFrame;
                        //alterBlockChain[1] = currentBlock;
                    }


                    double currentBlockChainEfficiency = GetChainEfficiency(alterBlockChain);

                    if (currentBlockChainEfficiency > alterBlockChainEfficiency)
                    {
                        alterBlockChainEfficiency = currentBlockChainEfficiency;
                        alterBlock = alterBlockChain[0];
                        alterBlockFound = true;
                    }
                }

                currentPosition++;

                if (currentPosition >= mainBlock.StartFrame + mainBlock.Length)
                {
                    if (alterBlockFound)
                    {
                        currentPosition = alterBlock.StartFrame + alterBlock.Length;
                        mainBlockChain.AddRange(SmartBuildBlocks(alterBlock.StartFrame, currentPosition));
                    }
                    else
                    {
                        mainBlockChain.Add(mainBlock);
                    }

                    alterBlockFound = false;
                    lastBlockFinished = true;

                    UpdateStatus(CompressionStage.Analysis, currentPosition);
                }

            }

            return (mainBlockChain);
        }


        // This one is used for multithreaded search of the best blocks for each frame.
        private void FindBestBlock(UInt32 position)
        {
            _bestBlocks[position].Block = GetBestBlock(position, TotalFrames);
            _bestBlocks[position].Ready = true;
            _bestBlocksBufferEWH.Set();
        }


        // Choosing one of two algorithms for searching the best block
        private Block GetBestBlock(UInt32 position, UInt32 boundary)
        {
            if (UseBruteForceBestBlockSearch)
            {
                return (GetBestBlockBruteForce(position, boundary));
            }

            return (GetBestBlockFast(position, boundary));
        }


        // This one selects channels one by one, finds the most effective method for selected channel, 
        // and for the other channels finds the most effective methods of exactly the same length 
        // as the best method for the selected channel.
        // In total, we get the number of blocks (with different combinations of methods per block)
        // equal to the number of channels, from which the best one is selected.
        // This algorithm is relatively fast and gives good results.
        private Block GetBestBlockFast(UInt32 position, UInt32 boundary)
        {
            Block bestBlock = new Block();
            bestBlock.StartFrame = position;
            bestBlock.Efficiency = double.MinValue;

            byte[] bestMethods = new byte[ChannelsCount];
            UInt32[] bestInitialValues = new UInt32[ChannelsCount];
            UInt32[] bestLengths = new UInt32[ChannelsCount];

            // It is better to find the most effective methods in advance.
            // This will allow, in some cases, not to search,
            // but to reuse these methods for other channels and improve performance.
            Method bestMethod;
            for (byte c = 0; c < ChannelsCount; c++)
            {
                bestMethod = GetBestMethod(position, c, boundary, false);
                bestMethods[c] = bestMethod.Index;
                bestInitialValues[c] = bestMethod.InitialValue;
                bestLengths[c] = bestMethod.Length;
            }

            // Go through the channels
            for (byte c = 0; c < ChannelsCount; c++)
            {
                UInt32 currentLength = bestLengths[c];

                byte[] currentMethods = new byte[ChannelsCount];
                UInt32[] currentInitialValues = new UInt32[ChannelsCount];

                currentMethods[c] = bestMethods[c];
                currentInitialValues[c] = bestInitialValues[c];

                for (byte cc = 0; cc < ChannelsCount; cc++)
                {
                    if (cc != c)
                    {
                        // And find the best methods for all channels except the current one
                        if (bestLengths[cc] != currentLength)
                        {
                            Method currentMethod = GetBestMethod(position, cc, position + currentLength, true);
                            currentMethods[cc] = currentMethod.Index;
                            currentInitialValues[cc] = currentMethod.InitialValue;
                        } else
                        {
                            currentMethods[cc] = bestMethods[cc];
                            currentInitialValues[cc] = bestInitialValues[cc];
                        }
                    }
                }

                // Save the most efficient block
                double efficiency = GetBlockEfficiency(currentLength, currentMethods);

                if (efficiency > bestBlock.Efficiency)
                {
                    bestBlock.Methods = currentMethods;
                    bestBlock.InitialValues = currentInitialValues;
                    bestBlock.Length = currentLength;
                    bestBlock.Efficiency = efficiency;
                }
            }

            return (bestBlock);
        }
        

        // Initialize and start recursive search for the best block
        private Block GetBestBlockBruteForce(UInt32 position, UInt32 boundary)
        {
            Block bestBlock = new Block();
            Method[] currentMethods = new Method[ChannelsCount];
            bestBlock.StartFrame = position;
            bestBlock.Efficiency = double.MinValue;

            // Calculate in advance and save the applicability of all possible methods for all channels
            Method[][] allMethods = new Method[ChannelsCount][];
            for (byte c = 0; c < ChannelsCount; c++)
            {
                byte methodsCount = GetMethodsCount(c);
                allMethods[c] = new Method[methodsCount];

                for (byte m = 0; m < methodsCount; m++)
                {
                    bool afw = false;
                    byte realMethod = GetRealMethod(m, BitsPerChannel[c], ref afw);

                    if (realMethod != BitsPerChannel[c])
                    {
                        if (afw) allMethods[c][m].Length = GetMaxBlockLength3(position, realMethod, c, 
                            out allMethods[c][m].InitialValue);
                        else
                            allMethods[c][m].Length = GetMaxBlockLength(position, realMethod, c, 
                                out allMethods[c][m].InitialValue);
                    }
                    else
                    {
                        allMethods[c][m].Length = _maxBlockSize;
                    }

                    allMethods[c][m].Index = m;
                }
            }

            // Run recursive combinations search
            SearchBestBlockRecursive(position, boundary, 0, ref currentMethods, ref allMethods, ref bestBlock);
            return (bestBlock);
        }


        // Recursive search for the best block by sequentially iterating through all methods of all channels
        private void SearchBestBlockRecursive(UInt32 position, UInt32 boundary, byte currentChannel, 
            ref Method[] currentMethods, ref Method[][] allMethods, ref Block bestBlock)
        {
            for (byte m = 0; m < GetMethodsCount(currentChannel); m++)
            {
                currentMethods[currentChannel] = allMethods[currentChannel][m];

                byte nextChannel = (byte)(currentChannel + 1);
                if (nextChannel < ChannelsCount)
                {
                    // If current channel is not the last one then run recursive search for the next one.
                    SearchBestBlockRecursive(position, boundary, nextChannel, ref currentMethods, ref allMethods, ref bestBlock);
                } else
                {
                    // If the channel is the last one, then trim the length to the shortest method,
                    // this is OK here, since we will eventually go through all the combinations.
                    UInt32 minLength = boundary - position;
                    byte[] methods = new byte[ChannelsCount];
                    UInt32[] initialValues = new UInt32[ChannelsCount];
                    for (byte c = 0; c < ChannelsCount; c++)
                    {
                        if (minLength > currentMethods[c].Length) minLength = currentMethods[c].Length;
                        methods[c] = currentMethods[c].Index;
                        initialValues[c] = currentMethods[c].InitialValue;
                    }

                    // Calculate the efficiency and if it is better than previously found, then save this combination.
                    double efficiency = GetBlockEfficiency(minLength, methods);

                    if (efficiency > bestBlock.Efficiency)
                    {
                        bestBlock.Methods = methods;
                        bestBlock.InitialValues = initialValues;
                        bestBlock.Length = minLength;
                        bestBlock.Efficiency = efficiency;
                    }
                }
            }
        }


        // Find the best compression method for the specified channel
        private Method GetBestMethod(UInt32 position, byte channel, UInt32 boundary, bool fill)
        {
            Method bestMethod = new Method();
            double bestEfficiency = double.MinValue;

            // Unfortunately, I managed to get the most effective method only by
            // sequentially iterating through all the methods.
            for (byte m = 0; m < GetMethodsCount(channel); m++)
            {
                UInt32 initialValue = 0;
                UInt32 length;

                bool afw = false;
                byte realMethod = GetRealMethod(m, BitsPerChannel[channel], ref afw);

                // Let's speed up a bit, find out if the current method
                // can theoretically give greater efficiency than the one found earlier.
                double maxTheoreticalEfficiency = 1.0d - ((double)m / (double)BitsPerChannel[channel]);// GetMethodEfficiency(boundary, m, channel);
                if (maxTheoreticalEfficiency < bestEfficiency)
                {
                    continue;
                }

                if (realMethod != BitsPerChannel[channel])
                {
                    if (afw) length = GetMaxBlockLength3(position, realMethod, channel, out initialValue);
                    else
                        length = GetMaxBlockLength(position, realMethod, channel, out initialValue);
                }
                else
                {
                    length = _maxBlockSize;
                }

                // Check if the method length matches the specified conditions
                UInt32 endOfBlock = position + length;
                if (fill && (endOfBlock < boundary)) continue;
                if (endOfBlock > boundary) length = boundary - position;

                // And compare its effectiveness with the most effective method from the previous iterations
                double efficiency = GetMethodEfficiency(length, m, channel);

                if (efficiency > bestEfficiency)
                {
                    bestEfficiency = efficiency;
                    bestMethod.Index = m;
                    bestMethod.InitialValue = initialValue;
                    bestMethod.Length = length;
                }
            }

            return (bestMethod);
        }


        // Get maximum Fixed Window block length
        // (I gave a detailed description in the 1st sprint,
        // see the link at the beginning of the file)
        private UInt32 GetMaxBlockLength(UInt32 position, byte method, byte channel, out UInt32 initialValue)
        {
            initialValue = 0;
            UInt32 length = 0;
            UInt32 min = UInt32.MaxValue;
            UInt32 max = 0;
            UInt32 maxDelta = (UInt32)(Math.Pow(2, method) - 1);

            while ((length < _maxBlockSize) && (position < TotalFrames))
            {
                UInt32 currentFrame = GetFrameForChannel(position, channel);
                if (currentFrame < min) min = currentFrame;
                if (currentFrame > max) max = currentFrame;
                if ((max - min) > maxDelta)
                {
                    break;
                }
                else
                {
                    initialValue = min;
                    length++;
                    position++;
                }
            }

            return (length);
        }


        // Get maximum Adaptive Floating Window block length
        // (I gave a detailed description in the 1st sprint,
        // see the link at the beginning of the file)
        private UInt32 GetMaxBlockLength3(UInt32 position, byte method, byte channel, out UInt32 initialValue)
        {
            UInt32 lastFrame = GetFrameForChannel(position++, channel);
            initialValue = lastFrame;

            UInt32 windowWidth = (UInt32)Math.Pow(2, method);
            UInt32 halfWindowWidth = windowWidth >> 1;
            UInt32 frameWidth = (UInt32)Math.Pow(2, BitsPerChannel[channel]);
            UInt32 maxFrameStart = frameWidth - windowWidth;
            UInt32 length = 1;

            while ((length < _maxBlockSize) && (position < TotalFrames))
            {
                UInt32 windowStart;
                if (halfWindowWidth < lastFrame)
                {
                    windowStart = lastFrame - halfWindowWidth;
                    if (windowStart > maxFrameStart) windowStart = maxFrameStart;
                }
                else
                {
                    windowStart = 0;
                }

                UInt32 currentFrame = GetFrameForChannel(position, channel);
                if ((currentFrame >= windowStart) && ((currentFrame - windowStart) < windowWidth))
                {
                    lastFrame = currentFrame;
                    length++;
                    position++;
                }
                else
                {
                    break;
                }
            }

            return (length);
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Efficiency calculations

        // Get the compressed data bits amount after compression via specified method
        private UInt64 GetCompressedBitsForMethod(UInt32 length, byte method, byte bitDepth)
        {
            UInt64 methodBits = 0;

            if (method != bitDepth) methodBits += bitDepth;

            bool afw = false;
            byte realMethod = GetRealMethod(method, bitDepth, ref afw);

            if (afw) length--;
            methodBits += (UInt64)(length * realMethod);

            return (methodBits);
        }


        // Get the number of bits for a block of compressed data
        private UInt64 GetBitsPerBlock(UInt32 length, byte[] methods)
        {
            UInt64 bitsPerBlock = _headerBits;

            for (byte c = 0; c < ChannelsCount; c++)
            {
                bitsPerBlock += GetCompressedBitsForMethod(length, methods[c], BitsPerChannel[c]);
            }

            return (bitsPerBlock);
        }


        // Get method efficiency (1 - method bits / raw bits)
        private double GetMethodEfficiency(UInt32 length, byte method, byte channel)
        {
            byte bitDepth = BitsPerChannel[channel];
            double bitsPerMethod = (double)_blockSizeBits / ChannelsCount + (double)BitsPerMethodDeclaration[channel];
            bitsPerMethod += (double)GetCompressedBitsForMethod(length, method, bitDepth);

            return (1.0d - (double)bitsPerMethod / ((double)length * (double)bitDepth));
        }


        // Get block efficiency (1 - block bits / raw bits)
        private double GetBlockEfficiency(UInt32 length, byte[] methods)
        {
            return (1.0d - (double)GetBitsPerBlock(length, methods) / ((double)length * (double)BitsPerFrame));
        }


        // Get the chain of blocks overall efficiency
        private double GetChainEfficiency(List<Block> chain)
        {
            UInt32 length = 0;
            UInt64 bits = 0;
            for (byte i = 0; i < chain.Count; i++)
            {
                length += chain[i].Length;
                bits += GetBitsPerBlock(chain[i].Length, chain[i].Methods);
            }

            return (1.0d - (double)bits / ((double)length * (double)BitsPerFrame));
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Compression

        // Compress one block into an output data bitstream
        private void CompressBlock(Block block)
        {
            UInt32 length = 0;
            UInt32 position = block.StartFrame;

            byte[] methods = new byte[ChannelsCount];
            UInt64[] windows = new UInt64[ChannelsCount];
            UInt32[] halfWindows = new UInt32[ChannelsCount];
            UInt32[] lastValues = new UInt32[ChannelsCount];
            UInt64[] ranges = new UInt64[ChannelsCount];
            bool[] useInitialValue = new bool[ChannelsCount];

            // Initialize arrays
            for (byte c = 0; c < ChannelsCount; c++)
            {
                windows[c] = 0; // Will use this value as Adaptive Floating window flag
                ranges[c] = (UInt32)Math.Pow(2, BitsPerChannel[c]);
                useInitialValue[c] = true;
            }

            // Write block length
            // Decrease by 1 for larger Efficiency (bit economy)!
            AddBitsToCompressedData(block.Length - 1, _blockSizeBits);


            // An alternative option is to place the initial value in the header
            // immediately after the method definition.
            // Depending on the microcontroller firmware compiler optimization parameters,
            // this option may be more CPU-efficient.
            // You can use this block instead of the next one by
            // defining PLACE_INITIAL_VALUES_AFTER_METHODS_DECLARATIONS symbol.

#if SEQUENTIAL_METHODS_AND_INITIAL_VALUES_DECLARATION
            //--------------------------------------------------------------------------------------------------
            for (byte c = 0; c < ChannelsCount; c++)
            {
                // Write methods definitions
                methods[c] = block.Methods[c];
                AddBitsToCompressedData(methods[c], BitsPerMethodDeclaration[c]);

                if (methods[c] != BitsPerChannel[c])
                {
                    // Write initial values and, if necessary, set Adaptive Floating Window parameters
                    AddBitsToCompressedData(block.InitialValues[c], BitsPerChannel[c]);

                    bool afw = false;
                    byte realMethod = GetRealMethod(methods[c], BitsPerChannel[c], ref afw);

                    if (afw)
                    {
                        methods[c] = realMethod;
                        windows[c] = (UInt64)Math.Pow(2, methods[c]);
                        halfWindows[c] = (UInt32)(windows[c] >> 1);
                        lastValues[c] = block.InitialValues[c];
                        useInitialValue[c] = true;
                    }
                }
            }

            //--------------------------------------------------------------------------------------------------
#else
            //--------------------------------------------------------------------------------------------------

            // Write methods definitions
            for (byte c = 0; c < ChannelsCount; c++)
            {
                methods[c] = block.Methods[c];
                AddBitsToCompressedData(methods[c], BitsPerMethodDeclaration[c]);
            }

            // Write initial values and, if necessary, set Adaptive Floating Window parameters
            for (byte c = 0; c < ChannelsCount; c++)
            {
                if (methods[c] != BitsPerChannel[c])
                {
                    AddBitsToCompressedData(block.InitialValues[c], BitsPerChannel[c]);

                    bool afw = false;
                    methods[c] = GetRealMethod(methods[c], BitsPerChannel[c], ref afw);

                    if (afw)
                    {
                        windows[c] = (UInt64)Math.Pow(2, methods[c]);
                        halfWindows[c] = (UInt32)(windows[c] >> 1);
                        lastValues[c] = block.InitialValues[c];
                        useInitialValue[c] = true;
                    }
                }
            }

            //--------------------------------------------------------------------------------------------------
#endif

            UInt64 bits = 0;

            while (length < block.Length)
            {
                // First, check whether the compression cancellation has been requested
                if ((_fallIntoErrorRequested) || 
                    ((_cancellationToken != null) && _cancellationToken.Value.IsCancellationRequested)) break;

                // Iterate through the channels
                for (byte c = 0; c < ChannelsCount; c++)
                {
                    // If we have to write some data (not the 0th method)
                    if (methods[c] > 0)
                    {
                        UInt32 currentFrame = GetFrameForChannel(position, c);
                        UInt32 delta;

                        if (methods[c] == BitsPerChannel[c])
                        {
                            // If there is no compression
                            delta = currentFrame;
                            AddBitsToCompressedData(delta, methods[c]);
                        }
                        else
                        {
                            // Else if Adaptive window's method used...
                            if (windows[c] > 0)
                            {
                                // Skip first frame because we have already wrote it as initial value
                                if (useInitialValue[c])
                                {
                                    useInitialValue[c] = false;
                                }
                                else
                                {
                                    // Calculate window parameters and save the compressed data
                                    UInt32 windowStart = (halfWindows[c] < lastValues[c]) ? (lastValues[c] - halfWindows[c]) : 0;
                                    if ((windowStart + windows[c]) > ranges[c]) windowStart = (UInt32)(ranges[c] - windows[c]);

                                    delta = currentFrame - windowStart;
                                    lastValues[c] = currentFrame;

                                    AddBitsToCompressedData(delta, methods[c]);
                                    bits += methods[c];
                                }
                            }
                            else
                            {
                                // ...or Fixed window used
                                delta = currentFrame - block.InitialValues[c];
                                AddBitsToCompressedData(delta, methods[c]);
                                bits += methods[c];
                            }
                        }
                    }
                }

                length++;
                position++;
            }

        }


        // Convert linear frame number to pixel within the square
        // (See the 3rd sprint)
        private UInt32 GetPixelForSquares(UInt32 frameNumber)
        {
            UInt32 currentSquareNumber = (UInt32)(frameNumber / _squareSize);
            UInt16 squaresLine = (UInt16)(currentSquareNumber / _squaresPerImageWidth);
            UInt32 firstPixel = (UInt32)(squaresLine * _squareSize * _squaresPerImageWidth + (currentSquareNumber % _squaresPerImageWidth) * WidthOfSquare);
            UInt16 internalOffset = (UInt16)(frameNumber % _squareSize);

            byte row = (byte)(internalOffset / WidthOfSquare);
            byte column = (byte)(internalOffset - row * WidthOfSquare);

            UInt32 pixel = (UInt32)(firstPixel + row * ImageWidth + column);

            return (pixel);
        }


        // Reading raw data
        private UInt32 GetFrameForChannel(UInt32 frameNumber, byte channel)
        {
            if (SplitToSquares) frameNumber = GetPixelForSquares(frameNumber);
            return (_sourceChannels[channel].GetTargetValueForFrame((int)frameNumber));
        }


        // Write compressed data
        private void AddBitsToCompressedData(UInt128 bits, byte count)
        {
            _bitsBuffer <<= count;
            _bitsBuffer |= bits;
            _bitsBufferCount += count;

            while (_bitsBufferCount >= 8)
            {
                byte newByte = (byte)((_bitsBuffer >> (_bitsBufferCount -= 8)) & 0xFF);
                _writeCompressedByteDelegate(newByte);
            }
        }


        // Save the remaining bits as the last data byte
        private void FinalizeCompressedData()
        {
            if (_bitsBufferCount > 0)
            {
                byte newByte = (byte)((_bitsBuffer << (8 - _bitsBufferCount)) & 0xFF);
                _writeCompressedByteDelegate(newByte);
            }

            _bitsBufferCount = 0;
        }

    }
}

// END-OF-FILE



