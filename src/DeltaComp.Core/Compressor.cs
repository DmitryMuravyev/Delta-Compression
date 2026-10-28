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

Sprint 4 (the optimal chain of blocks and the automatic settings) is described in
docs/Sprint_4_Mathematical_foundation.pdf and docs/v2-changes.md in the repository.

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
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;


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
            public const int MaxStackallocMethods = 256;
            public const int MaxBitsPerChannel = 32;
            public const int AutoSearchGrowingSteps = 3;
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

        // Source data, read once and stored in the compression order (already split into squares, if used).
        // Reading through the channels is relatively slow, and each frame is read many times during the analysis.
        private UInt32[][] _frames = null!;

        // The trials of AutoSelectSettings() share the frames in the linear order (instead of a copy per trial),
        // so a trial that splits the image into squares maps every frame number to its pixel.
        private bool _framesInLinearOrder = false;

        // Number of methods per channel, cached for the duration of the compression
        private byte[] _methodsCount = null!;

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

        // Build the chain of blocks with the minimum total size (dynamic programming).
        // If false, the frame-by-frame efficiency analysis (sprint 2) is used. The output format is the same.
        public bool UseOptimalBlockChain = true;
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

            // The best blocks buffer is allocated when (and if) the frame-by-frame analysis runs
            _bestBlocksArrayUsed = multithreadedRead;
            
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
            
            // The best blocks buffer is allocated when (and if) the frame-by-frame analysis runs
            _bestBlocksArrayUsed = multithreadedRead;
            
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


        // Compression settings chosen by AutoSelectSettings()
        public sealed record AutoSettings(byte BlockSizeBits, DeltaOption DeltaCalculationOption, byte WidthOfSquare, UInt64 CompressedBits);


        // Try the combinations of the given settings and apply the one that gives the smallest output
        // (with the optimal chain of blocks). Squares widths are ignored for non-image data; 1 means no squares.
        // All combinations of square sizes and delta options are tried (they interact strongly, so choosing
        // them one after another gives noticeably worse results), each with its own block size search.
        // The block size search stops early once the size has grown a few times in a row
        // (the size as a function of the block size field has a single minimum in practice).
        public AutoSettings AutoSelectSettings(IReadOnlyList<byte> blockSizeBitsOptions, IReadOnlyList<DeltaOption> deltaOptions,
            IReadOnlyList<byte> squareWidths, CancellationToken? token)
        {
            _cancellationToken = token;
            UpdateStatus(CompressionStage.Preparation, 0);

            // Read the source data once (in the linear order)
            bool splitToSquares = SplitToSquares;
            SplitToSquares = false;
            PrepareFrames();
            SplitToSquares = splitToSquares;
            UInt32[][] linearFrames = _frames;

            bool isImage = (ImageWidth > 0) && (ImageHeight > 0);
            var squares = isImage ? squareWidths.Where(w => (w > 0) && (ImageWidth % w == 0) && (ImageHeight % w == 0)).Distinct().ToList() : [];
            if (squares.Count == 0) squares.Add(1);
            var bitsOptions = blockSizeBitsOptions.OrderBy(b => b).ToList();
            int trialsDone = 0;
            object progressLock = new object();
            var trials = (from square in squares from delta in deltaOptions select (square, delta)).ToList();
            var results = RunAutoTrials(trials, linearFrames, bitsOptions, token, () =>
            {
                lock (progressLock) UpdateStatus(CompressionStage.Preparation, (UInt32)((UInt64)TotalFrames * (UInt64)(++trialsDone) / (UInt64)trials.Count));
            });

            // The smallest result (the first one in the order of the trials in case of a tie)
            int best = 0;
            for (int t = 1; t < trials.Count; t++) if (results[t].bits < results[best].bits) best = t;

            byte bestSquare = trials[best].square;
            if (isImage)
            {
                SplitToSquares = bestSquare > 1;
                WidthOfSquare = bestSquare;
            }
            DeltaCalculationOption = trials[best].delta;
            BlockSizeBits = results[best].blockSizeBits;
            _frames = null!;

            return (new AutoSettings(BlockSizeBits, DeltaCalculationOption, bestSquare, results[best].bits));
        }


        // Run the given trials (in parallel) and return the smallest size and its block size field for each
        private (UInt64 bits, byte blockSizeBits)[] RunAutoTrials(List<(byte square, DeltaOption delta)> trials,
            UInt32[][] linearFrames, List<byte> bitsOptions, CancellationToken? token, Action trialDone)
        {
            var results = new (UInt64 bits, byte blockSizeBits)[trials.Count];

            // "Both options, Adaptive first" has the same methods as "Both options, Fixed first" (only their numbers
            // differ), so its sizes are the same: it is calculated once if both are tried.
            static DeltaOption SameSizeAs(DeltaOption delta) =>
                (delta == DeltaOption.AdaptiveFloatingWindowFirst) ? DeltaOption.FixedWindowFirst : delta;
            var unique = trials.Select(t => (t.square, delta: SameSizeAs(t.delta))).Distinct().ToList();
            int[] uniqueIndex = trials.Select(t => unique.IndexOf((t.square, SameSizeAs(t.delta)))).ToArray();
            var uniqueResults = new (UInt64 bits, byte blockSizeBits)[unique.Count];

            try
            {
                Parallel.For(0, unique.Count, t =>
                {
                    var (square, delta) = unique[t];
                    Compressor trial = CreateTrial(linearFrames, square, delta, token);

                    // The block size search stops once the size has grown a few times in a row. The next few
                    // block sizes are calculated together (they share the lengths of the methods), but never more
                    // than the ones that would be calculated one by one.
                    UInt64 minBits = UInt64.MaxValue;
                    byte minBlockSizeBits = 0;
                    int growing = 0, next = 0;
                    while ((next < bitsOptions.Count) && (growing < Constants.AutoSearchGrowingSteps))
                    {
                        int count = Math.Min(Constants.AutoSearchGrowingSteps - growing, bitsOptions.Count - next);
                        List<byte> round = bitsOptions.GetRange(next, count);
                        next += count;

                        UInt64[] sizes = trial.FindOptimalChains(round, null, false);
                        for (int i = 0; i < count; i++)
                        {
                            if (sizes[i] < minBits)
                            {
                                minBits = sizes[i];
                                minBlockSizeBits = round[i];
                                growing = 0;
                            }
                            else if (++growing >= Constants.AutoSearchGrowingSteps) break;
                        }
                    }
                    uniqueResults[t] = (minBits, minBlockSizeBits);
                    for (int i = 0; i < uniqueIndex.Length; i++) if (uniqueIndex[i] == t) trialDone();
                });
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
            {
                throw ex.InnerExceptions[0];
            }

            for (int t = 0; t < trials.Count; t++) results[t] = uniqueResults[uniqueIndex[t]];

            return (results);
        }


        // A lightweight copy of this compressor for one combination of settings in AutoSelectSettings()
        private Compressor CreateTrial(UInt32[][] linearFrames, byte square, DeltaOption delta, CancellationToken? token)
        {
            Compressor trial = (ImageWidth > 0)
                ? new Compressor(_sourceChannels, ImageWidth, ImageHeight, false, _ => { }, null!)
                : new Compressor(_sourceChannels, TotalFrames, false, _ => { }, null!);
            trial._cancellationToken = token;
            trial.DeltaCalculationOption = delta;
            trial.SplitToSquares = square > 1;
            if (ImageWidth > 0) trial.WidthOfSquare = square;

            trial._methodsCount = new byte[ChannelsCount];
            for (byte c = 0; c < ChannelsCount; c++) trial._methodsCount[c] = trial.GetMethodsCount(c);
            trial._frames = linearFrames;
            trial._framesInLinearOrder = trial.SplitToSquares;
            return (trial);
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
            Debug.WriteLine("UseOptimalBlockChain: " + UseOptimalBlockChain.ToString());
            Debug.WriteLine("SplitToSquares: " + SplitToSquares.ToString());
            Debug.WriteLine("WidthOfSquare: " + WidthOfSquare.ToString());
            Debug.WriteLine("Header bits: {0}", _headerBits);
            */

            _cancellationToken = token;
            _cancellationToken?.ThrowIfCancellationRequested();

            // Let's start from the Preparation stage
            UpdateStatus(CompressionStage.Preparation, 0);

            // Read the source data and cache the parameters that don't change during the compression
            PrepareFrames();
            CheckCompressionCanceled();
            if (CheckCompressionError()) return;

            // I used this while optimizing the performance of the algorithms
            //var watch = Stopwatch.StartNew();

            // Multithreaded search for the best blocks
            if (_bestBlocksArrayUsed && !UseOptimalBlockChain)
            {
                _bestBlocks = new DistributedBlock[TotalFrames];
                _bestBlocksBufferEWH = new EventWaitHandle(false, EventResetMode.AutoReset);

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

            var mainBlockChain = UseOptimalBlockChain ? BuildOptimalBlocks() : SmartBuildBlocks(0, TotalFrames);

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
            // The header of the Method contains only its description w/o any real data:
            // the block length field plus, for every channel, the number of the method.
            // The initial values are not counted here, they belong to the cost of each method
            // (see GetCompressedBitsForMethod()).

            _headerBits = _blockSizeBits;
            for (byte c = 0; c < ChannelsCount; c++)
            {
                // The number of bits needed to write the largest method number (methods count - 1), at least 1
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


        // Get the number of methods depending on the delta calculation options used.
        // For a channel of K bits the method number m means (W = window width in bits):
        //   Fixed Window only:           m = 0 ~ K-1 Fixed W = m,                             m = K no compression;
        //   Adaptive Floating only:      m = 0 Fixed W = 0, m = 1 ~ K-1 Adaptive W = m,        m = K no compression;
        //   Both options, Fixed first:   m = 0 ~ K-1 Fixed W = m, m = K no compression, m = K+1 ~ 2K-1 Adaptive W = m - K;
        //   Both options, Adaptive first: the same, but 1 ~ K-1 are Adaptive and K+1 ~ 2K-1 are Fixed.
        // W = 0 means that all the frames of the block are equal to the initial value (no data at all).
        // The Adaptive window of width 0 doesn't exist, it would be the same as the Fixed one.
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
                // The delegate is responsible for marshalling the call to the UI thread (if needed)
                _updateProgressDelegate?.Invoke(Stage, Progress);
            }
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////
        // Operations with methods and blocks

        // Run a multithreaded search for the most effective blocks
        // One worker per processor takes the next unprocessed position in turn, so the blocks
        // are ready roughly in order and the chain building (which runs in parallel) rarely waits.
        private void FillBestBlocksArray()
        {
            Int64 lastTakenPosition = -1;
            int workersCount = Environment.ProcessorCount;
            int workersRunning = workersCount;

            for (int w = 0; w < workersCount; w++)
            {
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        while (true)
                        {
                            // We have to check if cancellation was requested
                            if ((_fallIntoErrorRequested) ||
                                ((_cancellationToken != null) && _cancellationToken.Value.IsCancellationRequested)) break;

                            Int64 position = Interlocked.Increment(ref lastTakenPosition);
                            if (position >= TotalFrames) break;

                            FindBestBlock((UInt32)position);
                        }
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref workersRunning) == 0) _waitForTasksFinished = false;
                    }
                }, TaskCreationOptions.LongRunning);
            }
        }


        // Build blocks for a frames range using serial efficiency analysis.
        // This one is a little bit complicated, so I'm referring you once again
        // to the links at the top of file.
        //
        // In short: the main block is the most efficient block starting at the current position. While we walk
        // through it, a more efficient block may start inside it; then we try the alternative chain
        // "head of the main block + that block (+ the tail of the main block)" and remember the head of the
        // most efficient alternative. When the main block ends, it is either taken as it is, or (if an
        // alternative was better) the range of the head is rebuilt recursively and the walk continues from
        // the end of the head.
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
                        CheckCompressionCanceled();
                    }
                    currentBlock = _bestBlocks[currentPosition].Block;
                }
                else
                {
                    currentBlock = GetBestBlock(currentPosition, boundary);
                }


                // A new main block starts here (the efficiency to beat is its own)
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

                // The main block is over: take it or rebuild the head of the best alternative
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


        // Build the chain of blocks with the minimum total size in bits.
        //
        // Let Best(p) be the smallest size of the data from frame p to the end. Then
        //   Best(p) = min over l of [ Bits(p, l) + Best(p + l) ],
        // where Bits(p, l) is the smallest block of exactly l frames starting at p. Going backwards
        // from the end gives the optimal chain for the chosen block size field and square size.
        //
        // Bits(p, l) is cheap: for each channel the cheapest method that lasts at least l frames is
        // the narrowest Fixed window that still fits, the narrowest Adaptive window that still fits,
        // or no compression, and the narrowest fitting window only grows with l.
        // Since Bits(p, l) never decreases with l, the search over l stops as soon as Bits(p, l)
        // alone is not smaller than the best total found so far.
        private List<Block> BuildOptimalBlocks()
        {
            UInt32[] bestLength = new UInt32[TotalFrames];
            FindOptimalChain(bestLength, true);
            var lengths = new ChannelMethodLengths(this);

            // Restore the chain
            var chain = new List<Block>();
            for (UInt32 position = 0; position < TotalFrames; position += bestLength[position])
            {
                UInt32 length = bestLength[position];
                lengths.Calculate(position);
                lengths.ResetCursors();

                Block block = new Block()
                {
                    StartFrame = position,
                    Length = length,
                    Methods = new byte[ChannelsCount],
                    InitialValues = new UInt32[ChannelsCount]
                };
                for (byte c = 0; c < ChannelsCount; c++)
                {
                    lengths.GetMinCost(c, length, out byte method);
                    block.Methods[c] = method;
                    block.InitialValues[c] = lengths.GetInitialValue(c, method);
                }
                block.Efficiency = GetBlockEfficiency(length, block.Methods);
                chain.Add(block);
            }

            return (chain);
        }


        // The dynamic programming pass of BuildOptimalBlocks(): returns the minimum total size of the
        // compressed data in bits and (optionally) the length of the first block of the optimal chain
        // for every position.
        private UInt64 FindOptimalChain(UInt32[]? bestLength, bool reportProgress)
        {
            return (FindOptimalChains([ _blockSizeBits ], bestLength, reportProgress)[0]);
        }


        // The same for several block size fields at once: the lengths of the methods are found once per
        // position and shared by all of them (only the costs of the blocks depend on the block size field).
        private UInt64[] FindOptimalChains(IReadOnlyList<byte> blockSizeBitsList, UInt32[]? bestLength, bool reportProgress)
        {
            if (_frames == null) PrepareFrames();

            // The lengths are found for the longest block size, shorter ones just limit them
            byte blockSizeBits = _blockSizeBits;
            BlockSizeBits = blockSizeBitsList.Max();
            var lengths = new ChannelMethodLengths(this);
            var searches = new ChainSearch[blockSizeBitsList.Count];
            for (int i = 0; i < searches.Length; i++)
            {
                searches[i] = new ChainSearch(this, blockSizeBitsList[i], bestLength);
            }

            try
            {
                for (Int64 p = (Int64)TotalFrames - 1; p >= 0; p--)
                {
                    if ((p & 0xFFF) == 0)
                    {
                        CheckCompressionCanceled();
                        if (reportProgress) UpdateStatus(CompressionStage.Analysis, (UInt32)(TotalFrames - p));
                    }

                    UInt32 position = (UInt32)p;
                    lengths.CalculateBackward(position);
                    foreach (ChainSearch search in searches) search.Step(position, lengths);
                }
            }
            finally
            {
                BlockSizeBits = blockSizeBits;
            }

            return (searches.Select(search => search.TotalBits).ToArray());
        }


        // The dynamic programming of the optimal chain for one block size field: Step() must be called
        // for TotalFrames - 1, TotalFrames - 2, ..., 0 in turn, after the lengths have been found for the position.
        private sealed class ChainSearch
        {
            // Chunks of block lengths that are checked as a whole before the lengths inside them (see Step())
            private const UInt32 BigChunk = 256, SmallChunk = 8;

            private readonly Compressor _compressor;
            private readonly UInt64 _headerBits;
            private readonly UInt32 _maxBlockSize;
            private readonly UInt64[] _best; // a ring buffer of the minimum size of the rest of the data
            private readonly UInt64 _ringMask;
            private readonly UInt32[]? _bestLength;
            private readonly byte[] _fixedCursor, _adaptiveCursor;

            public UInt64 TotalBits => _best[0];

            public ChainSearch(Compressor compressor, byte blockSizeBits, UInt32[]? bestLength)
            {
                _compressor = compressor;
                _headerBits = (UInt64)compressor._headerBits - compressor._blockSizeBits + blockSizeBits;
                _maxBlockSize = 1u << blockSizeBits;
                _bestLength = bestLength;
                _fixedCursor = new byte[compressor.ChannelsCount];
                _adaptiveCursor = new byte[compressor.ChannelsCount];

                // A block is never longer than _maxBlockSize, so only the next _maxBlockSize results are needed
                _ringMask = Math.Min(BitOperations.RoundUpToPowerOf2((UInt64)_maxBlockSize + 1),
                                     BitOperations.RoundUpToPowerOf2((UInt64)compressor.TotalFrames + 1)) - 1;
                _best = new UInt64[_ringMask + 1];
                _best[compressor.TotalFrames & _ringMask] = 0;
            }

            public void Step(UInt32 position, ChannelMethodLengths lengths)
            {
                UInt64[] best = _best;
                UInt64 ringMask = _ringMask;
                byte channels = _compressor.ChannelsCount;
                UInt32 maxLength = (UInt32)Math.Min((UInt64)_maxBlockSize, (UInt64)_compressor.TotalFrames - position);
                UInt64 bestBits = UInt64.MaxValue;
                UInt32 bestBlockLength = 0;
                // The narrowest windows to start from: Fixed 0, Adaptive 1 (see GetMethodsCount())
                Array.Clear(_fixedCursor);
                Array.Fill(_adaptiveCursor, (byte)1);

                // Bits(p, l) is piecewise linear in l, so we go segment by segment: within a segment
                // every channel keeps its cheapest method and Bits(p, l) = a + s * l.
                UInt32 l = 1;
                bool done = false;
                while (!done && (l <= maxLength))
                {
                    UInt64 a = _headerBits, slope = 0;
                    UInt32 segmentEnd = maxLength;
                    for (byte c = 0; c < channels; c++)
                    {
                        lengths.GetCostLine(c, l, maxLength, _fixedCursor, _adaptiveCursor,
                            out UInt64 channelA, out UInt64 channelSlope, out UInt32 channelEnd);
                        a += channelA;
                        slope += channelSlope;
                        if (channelEnd < segmentEnd) segmentEnd = channelEnd;
                    }

                    while (l <= segmentEnd)
                    {
                        // The block alone is already not smaller than the best total, and it only grows with l
                        UInt64 bits = a + slope * l;
                        if (bits >= bestBits) { done = true; break; }

                        // The rest of the data is never more expensive when it starts later (dropping the first
                        // frame of a block never makes the block longer), so the end of a chunk gives the smallest
                        // possible rest: if even that can't beat the best total, the whole chunk is skipped.
                        // Large chunks first, then small ones inside a large chunk that can't be skipped.
                        UInt32 chunkEnd = Math.Min(segmentEnd, l + (BigChunk - 1));
                        if (bits + best[((UInt64)position + chunkEnd) & ringMask] >= bestBits)
                        {
                            l = chunkEnd + 1;
                            continue;
                        }

                        while (l <= chunkEnd)
                        {
                            bits = a + slope * l;
                            if (bits >= bestBits) { done = true; break; }
                            UInt32 smallEnd = Math.Min(chunkEnd, l + (SmallChunk - 1));
                            if (bits + best[((UInt64)position + smallEnd) & ringMask] >= bestBits)
                            {
                                l = smallEnd + 1;
                                continue;
                            }

                            UInt64 index = position + l;
                            for (; l <= smallEnd; l++, index++, bits += slope)
                            {
                                if (bits >= bestBits) { done = true; break; }

                                // Strictly smaller: of the equal totals the shortest first block is kept
                                UInt64 total = bits + best[index & ringMask];
                                if (total < bestBits)
                                {
                                    bestBits = total;
                                    bestBlockLength = l;
                                }
                            }
                            if (done) break;
                        }
                        if (done) break;
                    }
                }

                best[position & ringMask] = bestBits;
                if (_bestLength != null) _bestLength[position] = bestBlockLength;
            }
        }


        // Lengths of all methods of all channels for one start position, plus the cheapest
        // method lookup for a growing block length (see BuildOptimalBlocks()).
        private sealed class ChannelMethodLengths
        {
            private readonly Compressor _compressor;
            private readonly UInt32[][] _fixedLengths, _fixedInitialValues, _adaptiveLengths;
            private readonly sbyte[][] _fixedMethodIndex, _adaptiveMethodIndex; // real window -> method index (-1 if unused)
            private readonly byte[] _fixedCursor, _adaptiveCursor;
            private UInt32 _position;

            public ChannelMethodLengths(Compressor compressor)
            {
                _compressor = compressor;
                int channels = compressor.ChannelsCount;
                _fixedLengths = new UInt32[channels][];
                _fixedInitialValues = new UInt32[channels][];
                _adaptiveLengths = new UInt32[channels][];
                _fixedMethodIndex = new sbyte[channels][];
                _adaptiveMethodIndex = new sbyte[channels][];
                _fixedCursor = new byte[channels];
                _adaptiveCursor = new byte[channels];

                for (byte c = 0; c < channels; c++)
                {
                    byte bitDepth = compressor.BitsPerChannel[c];
                    _fixedLengths[c] = new UInt32[Constants.MaxBitsPerChannel];
                    _fixedInitialValues[c] = new UInt32[Constants.MaxBitsPerChannel];
                    _adaptiveLengths[c] = new UInt32[Constants.MaxBitsPerChannel];
                    _fixedMethodIndex[c] = Enumerable.Repeat((sbyte)-1, bitDepth).ToArray();
                    _adaptiveMethodIndex[c] = Enumerable.Repeat((sbyte)-1, bitDepth).ToArray();

                    for (byte m = 0; m < compressor._methodsCount[c]; m++)
                    {
                        bool afw = false;
                        byte realMethod = compressor.GetRealMethod(m, bitDepth, ref afw);
                        if (realMethod == bitDepth) continue; // no compression
                        if (afw) _adaptiveMethodIndex[c][realMethod] = (sbyte)m;
                        else _fixedMethodIndex[c][realMethod] = (sbyte)m;
                    }
                }
            }

            // Incremental version of Calculate() for the backward pass: must be called for
            // TotalFrames - 1, TotalFrames - 2, ..., 0 in turn. O(number of methods) per position (amortized)
            // instead of scanning the data:
            //  - a Fixed window block [p, end) stays valid when p moves left only if the range still fits,
            //    so 'end' only moves left too, and the min/max of the block are kept in monotonic deques;
            //  - an Adaptive window block breaks at the first pair of neighboring frames that doesn't fit,
            //    so we only need to know where the next such pair is.
            public void CalculateBackward(UInt32 position)
            {
                _position = position;
                UInt32 totalFrames = _compressor.TotalFrames;
                UInt32 maxBlockSize = _compressor._maxBlockSize;

                if (_backward == null)
                {
                    _backward = new BackwardState[_compressor.ChannelsCount];
                    for (byte c = 0; c < _compressor.ChannelsCount; c++) _backward[c] = new BackwardState(_compressor, c);
                }

                for (byte c = 0; c < _compressor.ChannelsCount; c++)
                {
                    BackwardState state = _backward[c];
                    byte bitDepth = _compressor.BitsPerChannel[c];
                    UInt32[] frames = _compressor._frames[c];
                    UInt32 currentFrame = frames[_compressor._framesInLinearOrder ? _compressor.GetPixelForSquares(position) : position];
                    UInt32 nextFrame = state.NextFrame; // the frame at position + 1 (read in the previous step)
                    state.NextFrame = currentFrame;

                    for (byte w = 0; w < bitDepth; w++)
                    {
                        if (_fixedMethodIndex[c][w] < 0) continue;

                        MonotonicDeque min = state.Min[w], max = state.Max[w];
                        min.PushFront(position, currentFrame, true);
                        max.PushFront(position, currentFrame, false);

                        // Shrink the block [position, end) from the back until it is not longer than the maximum
                        // block size and its range (max - min) fits into the window. The deques always hold the
                        // minimum and the maximum of the current block at their back.
                        UInt32 end = state.FixedEnd[w];
                        UInt32 maxDelta = (w < 32) ? ((1u << w) - 1) : UInt32.MaxValue;
                        UInt64 maxEnd = Math.Min((UInt64)position + maxBlockSize, totalFrames);
                        while ((end > maxEnd) || (max.BackValue - min.BackValue > maxDelta))
                        {
                            end--;
                            min.RemoveBackIndex(end);
                            max.RemoveBackIndex(end);
                        }
                        state.FixedEnd[w] = end;
                        _fixedLengths[c][w] = end - position;
                        _fixedInitialValues[c][w] = min.BackValue;
                    }

                    // Adaptive windows: does the next frame fit into the window around this one?
                    UInt32 frameWidth = Pow2(bitDepth);
                    for (byte w = 1; w < bitDepth; w++)
                    {
                        if (_adaptiveMethodIndex[c][w] < 0) continue;

                        if (position + 1 < totalFrames)
                        {
                            UInt32 windowWidth = 1u << w;
                            UInt32 halfWindowWidth = windowWidth >> 1;
                            UInt32 maxFrameStart = frameWidth - windowWidth;
                            UInt32 windowStart;
                            if (halfWindowWidth < currentFrame)
                            {
                                windowStart = currentFrame - halfWindowWidth;
                                if (windowStart > maxFrameStart) windowStart = maxFrameStart;
                            }
                            else
                            {
                                windowStart = 0;
                            }
                            // If the next frame doesn't fit, a block starting here can't go beyond this frame;
                            // otherwise it lasts as long as the block starting at the next frame (+1)
                            if (!((nextFrame >= windowStart) && ((nextFrame - windowStart) < windowWidth)))
                                state.AdaptiveBreak[w] = position + 1;
                        }
                        else
                        {
                            state.AdaptiveBreak[w] = totalFrames;
                        }

                        _adaptiveLengths[c][w] = (UInt32)Math.Min(Math.Min((UInt64)state.AdaptiveBreak[w] - position, maxBlockSize),
                                                                  (UInt64)totalFrames - position);
                    }
                }
            }


            private BackwardState[]? _backward;

            private sealed class BackwardState
            {
                public readonly MonotonicDeque[] Min, Max;
                public readonly UInt32[] FixedEnd;      // exclusive end of the longest Fixed window block
                public readonly UInt32[] AdaptiveBreak; // first frame that doesn't fit the Adaptive window around the previous one
                public UInt32 NextFrame;                // value of the frame after the current position

                public BackwardState(Compressor compressor, byte channel)
                {
                    byte bitDepth = compressor.BitsPerChannel[channel];
                    // The deques grow when needed: usually they hold only a few elements
                    int capacity = (int)Math.Min(Math.Min((UInt64)compressor._maxBlockSize + 2, (UInt64)compressor.TotalFrames + 2), 64);
                    Min = new MonotonicDeque[bitDepth];
                    Max = new MonotonicDeque[bitDepth];
                    for (int w = 0; w < bitDepth; w++)
                    {
                        Min[w] = new MonotonicDeque(capacity);
                        Max[w] = new MonotonicDeque(capacity);
                    }
                    FixedEnd = Enumerable.Repeat(compressor.TotalFrames, bitDepth).ToArray();
                    AdaptiveBreak = Enumerable.Repeat(compressor.TotalFrames, bitDepth).ToArray();
                }
            }


            // A sliding window minimum/maximum for a window that grows at the front (lower indexes)
            // and shrinks at the back (higher indexes). The minimum (or maximum) is at the back.
            private sealed class MonotonicDeque
            {
                private UInt32[] _indexes, _values;
                private int _front, _count;

                public MonotonicDeque(int capacity)
                {
                    _indexes = new UInt32[capacity];
                    _values = new UInt32[capacity];
                }

                public UInt32 BackValue => _values[(_front + _count - 1) % _values.Length];

                public void PushFront(UInt32 index, UInt32 value, bool isMin)
                {
                    // Drop the front elements that can never be the extremum while the new one is in the window
                    // (they leave the window before the new one, since the window shrinks from the back)
                    while (_count > 0)
                    {
                        UInt32 frontValue = _values[_front];
                        if (isMin ? (frontValue >= value) : (frontValue <= value)) { _front = (_front + 1) % _values.Length; _count--; }
                        else break;
                    }
                    if (_count == _values.Length) Grow();
                    _front = (_front - 1 + _values.Length) % _values.Length;
                    _indexes[_front] = index;
                    _values[_front] = value;
                    _count++;
                }

                // Double the capacity keeping the order of the elements (the front goes to the start)
                private void Grow()
                {
                    UInt32[] indexes = new UInt32[_values.Length * 2], values = new UInt32[_values.Length * 2];
                    for (int i = 0; i < _count; i++)
                    {
                        indexes[i] = _indexes[(_front + i) % _indexes.Length];
                        values[i] = _values[(_front + i) % _values.Length];
                    }
                    _indexes = indexes;
                    _values = values;
                    _front = 0;
                }

                public void RemoveBackIndex(UInt32 index)
                {
                    if ((_count > 0) && (_indexes[(_front + _count - 1) % _indexes.Length] == index)) _count--;
                }
            }


            public void Calculate(UInt32 position)
            {
                _position = position;
                for (byte c = 0; c < _compressor.ChannelsCount; c++)
                {
                    _compressor.GetMaxBlockLengths(position, c, _fixedLengths[c], _fixedInitialValues[c]);
                    _compressor.GetMaxBlockLengths3(position, c, _adaptiveLengths[c]);
                }
            }

            public void ResetCursors()
            {
                Array.Clear(_fixedCursor);
                Array.Fill(_adaptiveCursor, (byte)1);
            }

            // The cheapest method of the channel that lasts at least 'length' frames.
            // Must be called with non-decreasing lengths after ResetCursors().
            // A narrower window is cheaper but never lasts longer, so as the length grows the narrowest window
            // that still lasts long enough only gets wider: the cursors remember it and only move forward.
            // In case of a tie no compression wins over Fixed, and Fixed over Adaptive.
            public UInt64 GetMinCost(byte channel, UInt32 length, out byte method)
            {
                byte bitDepth = _compressor.BitsPerChannel[channel];
                sbyte[] fixedIndex = _fixedMethodIndex[channel], adaptiveIndex = _adaptiveMethodIndex[channel];

                // No compression is always possible
                method = bitDepth;
                UInt64 minCost = (UInt64)length * bitDepth;

                // The narrowest Fixed window that lasts long enough (wider ones only cost more)
                byte w = _fixedCursor[channel];
                while ((w < bitDepth) && ((fixedIndex[w] < 0) || (_fixedLengths[channel][w] < length))) w++;
                _fixedCursor[channel] = w;
                if (w < bitDepth)
                {
                    UInt64 cost = bitDepth + (UInt64)length * w;
                    if (cost < minCost) { minCost = cost; method = (byte)fixedIndex[w]; }
                }

                // The same for the Adaptive Floating window
                w = _adaptiveCursor[channel];
                while ((w < bitDepth) && ((adaptiveIndex[w] < 0) || (_adaptiveLengths[channel][w] < length))) w++;
                _adaptiveCursor[channel] = w;
                if (w < bitDepth)
                {
                    UInt64 cost = bitDepth + (UInt64)(length - 1) * w;
                    if (cost < minCost) { minCost = cost; method = (byte)adaptiveIndex[w]; }
                }

                return (minCost);
            }

            // The cost of the cheapest method of the channel for the block length 'length' as a line a + slope * length,
            // and the last length ('end') up to which this line stays the cheapest.
            // Must be called with non-decreasing lengths after the cursors are reset (see ChainSearch.Step()).
            public void GetCostLine(byte channel, UInt32 length, UInt32 maxLength, byte[] fixedCursor, byte[] adaptiveCursor,
                out UInt64 a, out UInt64 slope, out UInt32 end)
            {
                byte bitDepth = _compressor.BitsPerChannel[channel];
                sbyte[] fixedIndex = _fixedMethodIndex[channel], adaptiveIndex = _adaptiveMethodIndex[channel];

                // Candidates: the narrowest Fixed and Adaptive windows that last long enough, and no compression.
                // (A wider window of the same kind would cost more at this length, so it isn't needed until
                // the narrower one ends.)
                // Lines: Fixed k + w * l, Adaptive (k - w) + w * l, no compression k * l.
                Span<UInt64> lineA = stackalloc UInt64[3];
                Span<UInt64> lineSlope = stackalloc UInt64[3];
                Span<UInt32> lineEnd = stackalloc UInt32[3];
                int lines = 0;

                lineA[lines] = 0; lineSlope[lines] = bitDepth; lineEnd[lines] = maxLength; lines++;

                byte w = fixedCursor[channel];
                while ((w < bitDepth) && ((fixedIndex[w] < 0) || (_fixedLengths[channel][w] < length))) w++;
                fixedCursor[channel] = w;
                if (w < bitDepth)
                {
                    lineA[lines] = bitDepth; lineSlope[lines] = w; lineEnd[lines] = Math.Min(_fixedLengths[channel][w], maxLength); lines++;
                }

                w = adaptiveCursor[channel];
                while ((w < bitDepth) && ((adaptiveIndex[w] < 0) || (_adaptiveLengths[channel][w] < length))) w++;
                adaptiveCursor[channel] = w;
                if (w < bitDepth)
                {
                    lineA[lines] = (UInt64)(bitDepth - w); lineSlope[lines] = w; lineEnd[lines] = Math.Min(_adaptiveLengths[channel][w], maxLength); lines++;
                }

                // The cheapest line at 'length'
                int current = 0;
                UInt64 minCost = UInt64.MaxValue;
                for (int i = 0; i < lines; i++)
                {
                    UInt64 cost = lineA[i] + lineSlope[i] * length;
                    if (cost < minCost) { minCost = cost; current = i; }
                }

                a = lineA[current];
                slope = lineSlope[current];

                // It stays the cheapest until one of the candidates ends (then the candidates change)
                // or a line with a smaller slope becomes cheaper.
                end = maxLength;
                for (int i = 0; i < lines; i++)
                {
                    if (lineEnd[i] < end) end = lineEnd[i];
                    if (lineSlope[i] < slope)
                    {
                        // The first l with lineA[i] + lineSlope[i] * l < a + slope * l
                        UInt64 crossing = (lineA[i] - a) / (slope - lineSlope[i]) + 1;
                        if (crossing - 1 < end) end = (UInt32)(crossing - 1);
                    }
                }
                // The current line is the cheapest at 'length' itself, so the segment is never empty
                if (end < length) end = length;
            }


            public UInt32 GetInitialValue(byte channel, byte method)
            {
                byte bitDepth = _compressor.BitsPerChannel[channel];
                if (method == bitDepth) return (0);
                bool afw = false;
                byte realMethod = _compressor.GetRealMethod(method, bitDepth, ref afw);
                return (afw ? _compressor._frames[channel][_position] : _fixedInitialValues[channel][realMethod]);
            }
        }


        // This one is used for multithreaded search of the best blocks for each frame.
        private void FindBestBlock(UInt32 position)
        {
            _bestBlocks[position].Block = GetBestBlock(position, TotalFrames);
            _bestBlocks[position].Ready = true;
            _bestBlocksBufferEWH.Set();
        }


        // Search for the best block (the former "Fast" search is gone: the exact search is now as quick)
        private Block GetBestBlock(UInt32 position, UInt32 boundary)
        {
            return (GetBestBlockBruteForce(position, boundary));
        }


        // Exact search for the best block (the same result as trying every combination of methods).
        //
        // Brute force over all combinations costs M^C (4096 for RGB888), but for a fixed block length L
        // the block size is just the header plus independent per-channel costs. So for every candidate L
        // (the distinct method lengths) we only need, per channel:
        //   A(c) - the cheapest method that lasts at least L frames,
        //   B(c) - the cheapest method that lasts exactly L frames (the combination length is the
        //          shortest method, so at least one channel has to end exactly at L).
        // The smallest block of length L is then: header + Sum(A) + min over c of (B(c) - A(c)).
        // This is O((C*M)^2) instead of M^C.
        //
        // To keep the output byte-identical with the original brute force, ties are resolved the same way:
        // the brute force keeps the first combination (in lexicographic order of methods) with the
        // maximum efficiency, so we reconstruct exactly that combination.
        private Block GetBestBlockBruteForce(UInt32 position, UInt32 boundary)
        {
            Block bestBlock = new Block();
            bestBlock.StartFrame = position;
            bestBlock.Efficiency = double.MinValue;

            // Methods of all channels are stored in one flat buffer: [channel * stride + method]
            int stride = 0;
            for (byte c = 0; c < ChannelsCount; c++) stride = Math.Max(stride, _methodsCount[c]);
            int allMethodsCount = ChannelsCount * stride;
            Span<Method> allMethods = (allMethodsCount <= Constants.MaxStackallocMethods) ?
                stackalloc Method[allMethodsCount] : new Method[allMethodsCount];
            Span<UInt32> candidates = (allMethodsCount <= Constants.MaxStackallocMethods) ?
                stackalloc UInt32[allMethodsCount] : new UInt32[allMethodsCount];
            int candidatesCount = 0;
            UInt32 maxLength = boundary - position;

            // Lengths of all Fixed Window methods of a channel are found in a single pass
            Span<UInt32> fixedLengths = stackalloc UInt32[Constants.MaxBitsPerChannel];
            Span<UInt32> fixedInitialValues = stackalloc UInt32[Constants.MaxBitsPerChannel];
            Span<UInt32> adaptiveLengths = stackalloc UInt32[Constants.MaxBitsPerChannel];

            // Calculate in advance the applicability (length) of all possible methods for all channels
            for (byte c = 0; c < ChannelsCount; c++)
            {
                byte methodsCount = _methodsCount[c];
                Span<Method> channelMethods = allMethods.Slice(c * stride, methodsCount);
                bool fixedLengthsReady = false;
                bool adaptiveLengthsReady = false;

                for (byte m = 0; m < methodsCount; m++)
                {
                    bool afw = false;
                    byte realMethod = GetRealMethod(m, BitsPerChannel[c], ref afw);

                    if (realMethod != BitsPerChannel[c])
                    {
                        if (afw)
                        {
                            if (!adaptiveLengthsReady)
                            {
                                GetMaxBlockLengths3(position, c, adaptiveLengths);
                                adaptiveLengthsReady = true;
                            }
                            channelMethods[m].Length = adaptiveLengths[realMethod];
                            channelMethods[m].InitialValue = _frames[c][position];
                        }
                        else
                        {
                            if (!fixedLengthsReady)
                            {
                                GetMaxBlockLengths(position, c, fixedLengths, fixedInitialValues);
                                fixedLengthsReady = true;
                            }
                            channelMethods[m].Length = fixedLengths[realMethod];
                            channelMethods[m].InitialValue = fixedInitialValues[realMethod];
                        }
                    }
                    else
                    {
                        // No compression lasts as long as needed
                        channelMethods[m].Length = _maxBlockSize;
                        channelMethods[m].InitialValue = 0;
                    }

                    // The block can't go beyond the boundary
                    if (channelMethods[m].Length > maxLength) channelMethods[m].Length = maxLength;
                    channelMethods[m].Index = m;

                    // Collect distinct lengths as the candidate block lengths
                    UInt32 length = channelMethods[m].Length;
                    if (!candidates.Slice(0, candidatesCount).Contains(length)) candidates[candidatesCount++] = length;
                }
            }

            Span<UInt64> minCosts = stackalloc UInt64[ChannelsCount];       // A(c)
            Span<UInt64> minExactCosts = stackalloc UInt64[ChannelsCount];  // B(c)
            Span<byte> currentMethods = stackalloc byte[ChannelsCount];
            Span<byte> bestMethods = stackalloc byte[ChannelsCount];
            UInt64 noCost = UInt64.MaxValue;

            // Pass 1: the maximum efficiency (1 - block bits / raw bits) among all candidate lengths.
            // The frame-by-frame analysis compares blocks of different lengths, so it needs the efficiency, not the size.
            Span<double> efficiencies = (allMethodsCount <= Constants.MaxStackallocMethods) ?
                stackalloc double[candidatesCount] : new double[candidatesCount];
            double maxEfficiency = double.MinValue;

            for (int i = 0; i < candidatesCount; i++)
            {
                UInt32 length = candidates[i];
                UInt64 bits = GetMinBitsForLength(length, allMethods, stride, minCosts, minExactCosts);
                efficiencies[i] = (bits == noCost) ? double.MinValue :
                    1.0d - (double)bits / ((double)length * (double)BitsPerFrame);
                if (efficiencies[i] > maxEfficiency) maxEfficiency = efficiencies[i];
            }

            // Pass 2: the lexicographically first combination with the maximum efficiency
            bool found = false;
            for (int i = 0; i < candidatesCount; i++)
            {
                if (efficiencies[i] != maxEfficiency) continue;

                UInt32 length = candidates[i];
                UInt64 bits = GetMinBitsForLength(length, allMethods, stride, minCosts, minExactCosts);
                GetFirstCombinationForLength(length, bits, allMethods, stride, minCosts, minExactCosts, currentMethods);

                if (!found || (currentMethods.SequenceCompareTo(bestMethods) < 0))
                {
                    currentMethods.CopyTo(bestMethods);
                    bestBlock.Length = length;
                    found = true;
                }
            }

            UInt32[] initialValues = new UInt32[ChannelsCount];
            for (byte c = 0; c < ChannelsCount; c++)
            {
                initialValues[c] = allMethods[c * stride + bestMethods[c]].InitialValue;
            }
            bestBlock.Methods = bestMethods.ToArray();
            bestBlock.InitialValues = initialValues;
            bestBlock.Efficiency = maxEfficiency;

            return (bestBlock);
        }


        // The smallest block size (bits) among the combinations of methods with the length of exactly 'length' frames.
        // Also fills A(c) and B(c) for this length (see GetBestBlockBruteForce).
        private UInt64 GetMinBitsForLength(UInt32 length, ReadOnlySpan<Method> allMethods, int stride,
            Span<UInt64> minCosts, Span<UInt64> minExactCosts)
        {
            UInt64 bits = _headerBits;

            // Taking A(c) for every channel gives the cheapest block that lasts AT LEAST 'length' frames.
            // To make it last EXACTLY 'length' frames, at least one channel has to switch to B(c), so the
            // cheapest way is to switch the channel where this costs the least extra: minDelta = min(B(c) - A(c)).
            // If no channel has a method that ends exactly at 'length', there is no such block at all.
            UInt64 minDelta = UInt64.MaxValue;

            for (byte c = 0; c < ChannelsCount; c++)
            {
                UInt64 minCost = UInt64.MaxValue;
                UInt64 minExactCost = UInt64.MaxValue;
                ReadOnlySpan<Method> channelMethods = allMethods.Slice(c * stride, _methodsCount[c]);

                for (byte m = 0; m < channelMethods.Length; m++)
                {
                    if (channelMethods[m].Length < length) continue; // the method doesn't last long enough
                    UInt64 cost = GetCompressedBitsForMethod(length, m, BitsPerChannel[c]);
                    if (cost < minCost) minCost = cost;
                    if ((channelMethods[m].Length == length) && (cost < minExactCost)) minExactCost = cost;
                }

                minCosts[c] = minCost;
                minExactCosts[c] = minExactCost;
                if (minCost == UInt64.MaxValue) return (UInt64.MaxValue);
                bits += minCost;
                if ((minExactCost != UInt64.MaxValue) && ((minExactCost - minCost) < minDelta)) minDelta = minExactCost - minCost;
            }

            if (minDelta == UInt64.MaxValue) return (UInt64.MaxValue);
            return (bits + minDelta);
        }


        // Reconstruct the lexicographically first combination of methods with the length
        // of exactly 'length' frames and the size of 'bits' (which is the minimum for this length).
        // Uses A(c) and B(c) calculated by GetMinBitsForLength() for the same length.
        //
        // The channels are fixed one by one: for each channel we take the smallest method number that can still
        // be completed (by the remaining channels) to a block of exactly 'bits' bits. Since 'bits' is the minimum,
        // a choice is good exactly when the cheapest completion reaches 'bits'; the cheapest completion is A(cc)
        // for every remaining channel, plus the cheapest switch to B(cc) if no channel chosen so far ends at 'length'.
        private void GetFirstCombinationForLength(UInt32 length, UInt64 bits, ReadOnlySpan<Method> allMethods, int stride,
            ReadOnlySpan<UInt64> minCosts, ReadOnlySpan<UInt64> minExactCosts, Span<byte> methods)
        {
            UInt64 prefixBits = _headerBits;
            bool lengthReached = false; // whether some channel of the prefix ends exactly at 'length'

            for (byte c = 0; c < ChannelsCount; c++)
            {
                // The minimum the remaining channels can add (restBits), and the extra cost (restMinDelta)
                // if one of them still has to provide the "exact length" (as minDelta in GetMinBitsForLength())
                UInt64 restBits = 0;
                UInt64 restMinDelta = UInt64.MaxValue;
                for (int cc = c + 1; cc < ChannelsCount; cc++)
                {
                    restBits += minCosts[cc];
                    if ((minExactCosts[cc] != UInt64.MaxValue) && ((minExactCosts[cc] - minCosts[cc]) < restMinDelta))
                        restMinDelta = minExactCosts[cc] - minCosts[cc];
                }

                ReadOnlySpan<Method> channelMethods = allMethods.Slice(c * stride, _methodsCount[c]);
                for (byte m = 0; m < channelMethods.Length; m++)
                {
                    if (channelMethods[m].Length < length) continue;

                    UInt64 cost = GetCompressedBitsForMethod(length, m, BitsPerChannel[c]);
                    bool reached = lengthReached || (channelMethods[m].Length == length);
                    // Nobody else can end the block exactly at 'length', so this method can't be used here
                    if (!reached && (restMinDelta == UInt64.MaxValue)) continue;

                    UInt64 total = prefixBits + cost + restBits + (reached ? 0 : restMinDelta);
                    if (total == bits)
                    {
                        methods[c] = m;
                        prefixBits += cost;
                        lengthReached = reached;
                        break;
                    }
                }
            }
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
            UInt32 maxDelta = (method < 32) ? ((1u << method) - 1) : UInt32.MaxValue;

            ReadOnlySpan<UInt32> frames = _frames[channel];
            UInt32 maxLength = (UInt32)Math.Min((UInt64)_maxBlockSize, (UInt64)TotalFrames - position);

            while (length < maxLength)
            {
                UInt32 currentFrame = frames[(int)(position + length)];
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
                }
            }

            return (length);
        }


        // The same as GetMaxBlockLength(), but for all Fixed Window methods (0 ~ bits per channel - 1) at once.
        // The range (max - min) of the block only grows with the length, so a wider window always lasts
        // at least as long as a narrower one, and one scan is enough to find where each window gets exceeded.
        private void GetMaxBlockLengths(UInt32 position, byte channel, Span<UInt32> lengths, Span<UInt32> initialValues)
        {
            byte bitDepth = BitsPerChannel[channel];
            ReadOnlySpan<UInt32> frames = _frames[channel];
            UInt32 maxLength = (UInt32)Math.Min((UInt64)_maxBlockSize, (UInt64)TotalFrames - position);

            UInt32 min = UInt32.MaxValue;
            UInt32 max = 0;
            byte method = 0; // the narrowest window that still fits all frames so far
            UInt32 length = 0;

            while (length < maxLength)
            {
                UInt32 currentFrame = frames[(int)(position + length)];
                UInt32 newMin = (currentFrame < min) ? currentFrame : min;
                UInt32 newMax = (currentFrame > max) ? currentFrame : max;
                UInt32 range = newMax - newMin;

                // All the windows that the new frame doesn't fit into end here: the window W holds the values
                // from the minimum of the block to the minimum + 2^W - 1 (the deltas are written relative to the minimum)
                while ((method < bitDepth) && (range > ((1u << method) - 1)))
                {
                    lengths[method] = length;
                    initialValues[method] = (length > 0) ? min : 0;
                    method++;
                }
                if (method >= bitDepth) return;

                min = newMin;
                max = newMax;
                length++;
            }

            // The remaining windows last until the end of the data or the maximum block size
            for (; method < bitDepth; method++)
            {
                lengths[method] = length;
                initialValues[method] = (length > 0) ? min : 0;
            }
        }


        // The same as GetMaxBlockLength3(), but for all Adaptive Floating Window methods (1 ~ bits per channel - 1) at once.
        // For the same previous frame, the window of width 2^(w+1) always contains the window of width 2^w
        // (both are centered on the previous frame and clamped to the same range), and the previous frame
        // is just the source data, so a wider window lasts at least as long and one scan is enough.
        private void GetMaxBlockLengths3(UInt32 position, byte channel, Span<UInt32> lengths)
        {
            byte bitDepth = BitsPerChannel[channel];
            ReadOnlySpan<UInt32> frames = _frames[channel];
            UInt32 frameWidth = Pow2(bitDepth);
            UInt32 maxLength = (UInt32)Math.Min((UInt64)_maxBlockSize, (UInt64)TotalFrames - position);

            UInt32 lastFrame = frames[(int)position];
            byte method = 1; // the narrowest window that still fits all frames so far
            UInt32 length = 1;

            while (length < maxLength)
            {
                UInt32 currentFrame = frames[(int)(position + length)];

                // All the windows that the new frame doesn't fit into end here. The Adaptive window of 2^W values
                // is centered on the previous frame and moved back inside the range of the channel (0 ~ 2^K - 1)
                // if it sticks out; the frame is written as its offset from the start of the window.
                // The decoder repeats the same calculation, so the window is never stored.
                while (method < bitDepth)
                {
                    UInt32 windowWidth = 1u << method;
                    UInt32 halfWindowWidth = windowWidth >> 1;
                    UInt32 maxFrameStart = frameWidth - windowWidth;
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

                    if ((currentFrame >= windowStart) && ((currentFrame - windowStart) < windowWidth)) break;

                    lengths[method] = length;
                    method++;
                }
                if (method >= bitDepth) return;

                lastFrame = currentFrame;
                length++;
            }

            // The remaining windows last until the end of the data or the maximum block size
            for (; method < bitDepth; method++) lengths[method] = length;
        }


        // Get maximum Adaptive Floating Window block length
        // (I gave a detailed description in the 1st sprint,
        // see the link at the beginning of the file)
        private UInt32 GetMaxBlockLength3(UInt32 position, byte method, byte channel, out UInt32 initialValue)
        {
            ReadOnlySpan<UInt32> frames = _frames[channel];
            UInt32 lastFrame = frames[(int)position++];
            initialValue = lastFrame;

            UInt32 windowWidth = Pow2(method);
            UInt32 halfWindowWidth = windowWidth >> 1;
            UInt32 frameWidth = Pow2(BitsPerChannel[channel]);
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

                UInt32 currentFrame = frames[(int)position];
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

        // Get the compressed data bits amount after compression via specified method:
        //   no compression     - length * K bits (K = bits per channel);
        //   Fixed window W     - K bits of the initial value (the minimum of the block) + length * W bits of deltas;
        //   Adaptive window W  - K bits of the initial value (the first frame itself) + (length - 1) * W bits.
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
        private UInt64 GetBitsPerBlock(UInt32 length, ReadOnlySpan<byte> methods)
        {
            UInt64 bitsPerBlock = _headerBits;

            for (byte c = 0; c < ChannelsCount; c++)
            {
                bitsPerBlock += GetCompressedBitsForMethod(length, methods[c], BitsPerChannel[c]);
            }

            return (bitsPerBlock);
        }


        // Get block efficiency (1 - block bits / raw bits)
        private double GetBlockEfficiency(UInt32 length, ReadOnlySpan<byte> methods)
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

            Span<byte> methods = stackalloc byte[ChannelsCount];
            Span<UInt64> windows = stackalloc UInt64[ChannelsCount];
            Span<UInt32> halfWindows = stackalloc UInt32[ChannelsCount];
            Span<UInt32> lastValues = stackalloc UInt32[ChannelsCount];
            Span<UInt64> ranges = stackalloc UInt64[ChannelsCount];
            Span<bool> useInitialValue = stackalloc bool[ChannelsCount];

            // Initialize arrays
            for (byte c = 0; c < ChannelsCount; c++)
            {
                windows[c] = 0; // Will use this value as Adaptive Floating window flag
                ranges[c] = Pow2(BitsPerChannel[c]);
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
            // defining SEQUENTIAL_METHODS_AND_INITIAL_VALUES_DECLARATION symbol (see the top of the file).

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
                    methods[c] = GetRealMethod(methods[c], BitsPerChannel[c], ref afw);

                    if (afw)
                    {
                        windows[c] = 1UL << methods[c];
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
                        windows[c] = 1UL << methods[c];
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
                    // If we have to write some data (not the 0th method: the window of width 0 means that all
                    // the frames are equal to the initial value, so there is nothing to write)
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
        // The squares go left to right, then top to bottom, and the pixels inside a square go row by row,
        // so frame number = square number * square size + row * square width + column.
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


        // Read all source data into the frames cache
        private void PrepareFrames()
        {
            _methodsCount = new byte[ChannelsCount];
            _frames = new UInt32[ChannelsCount][];

            for (byte c = 0; c < ChannelsCount; c++)
            {
                _methodsCount[c] = GetMethodsCount(c);
                _frames[c] = new UInt32[TotalFrames];
            }

            // Read frame by frame (rather than channel by channel) to stay friendly to the data source buffers
            for (UInt32 f = 0; f < TotalFrames; f++)
            {
                UInt32 pixel = SplitToSquares ? GetPixelForSquares(f) : f;
                for (byte c = 0; c < ChannelsCount; c++)
                {
                    _frames[c][f] = _sourceChannels[c].GetTargetValueForFrame((int)pixel);
                }
            }
        }


        // Reading raw data (from the cache)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UInt32 GetFrameForChannel(UInt32 frameNumber, byte channel)
        {
            return (_frames[channel][frameNumber]);
        }


        // 2 to the power of n as UInt32 (for n < 32 this is exactly what (UInt32)Math.Pow(2, n) returns)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static UInt32 Pow2(int n)
        {
            return ((n < 32) ? (1u << n) : (UInt32)Math.Pow(2, n));
        }


        // Write compressed data: the bits are packed most significant bit first, and every complete byte is
        // passed on at once (the bits that don't fill a byte yet stay in the buffer)
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



