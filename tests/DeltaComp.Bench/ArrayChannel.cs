namespace DeltaComp.Bench
{
    // A channel backed by a plain array of original values.
    // Scaling to the target bit depth is done by the Channel base class, exactly as in the desktop app.
    public sealed class ArrayChannel : Channel
    {
        private readonly uint[] _values;

        public ArrayChannel(ChannelType type, uint[] values, int bitsPerChannel, int targetBitDepth)
        {
            ChannelType = type;
            _values = values;
            BitsPerChannel = bitsPerChannel;
            TargetBitDepth = targetBitDepth;
        }

        public override uint GetOriginalValueForFrame(int frameNumber) => _values[frameNumber];
    }
}
