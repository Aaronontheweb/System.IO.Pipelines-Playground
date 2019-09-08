using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    /// <summary>
    /// Using length-frame encoding
    /// </summary>
    public sealed class FrameLengthDecoder
    {
        /// <summary>
        ///     We use a 4 byte header
        /// </summary>
        public const int MessageLengthHeaderSize = 4;
        public const int frameLengthHeaderSize = 4;
        public long MaxFrameSize { get; }

        private bool discardingTooLongFrame = false;
        private long bytesToDiscard = 0;
        private long tooLongFrameLength = 0;

        private readonly ILogger _logger;

        public FrameLengthDecoder(ILogger logger, long maxFrameSize = 128000)
        {
            MaxFrameSize = maxFrameSize;
            _logger = logger;
        }

        public SequencePosition Decode(ReadOnlySequence<byte> input, out IEnumerable<ReadOnlySequence<byte>> msgs)
        {
            var buffer = input;
            var decoded = new List<ReadOnlySequence<byte>>();
            msgs = decoded; // so we can exit if there are no processed messages
            SequencePosition position = buffer.Start;

            while(true)
            {
                // check to see if we're already discarding an illegally large frame
                if (discardingTooLongFrame)
                {
                    var localBytesToDiscard = Math.Min(buffer.Length, bytesToDiscard);
                    position = buffer.GetPosition(localBytesToDiscard);
                    buffer = buffer.Slice(position); // advance buffer for further reading (possibly)
                    bytesToDiscard -= localBytesToDiscard;
                    if (bytesToDiscard == 0)
                    {
                        discardingTooLongFrame = false;
                        _logger.LogDebug("No longer discarding too long frame of size [{0}] bytes.", tooLongFrameLength);
                        tooLongFrameLength = 0;
                    }
                    else
                    {

                        return position; // can't do any further processing - still have more to discard upon next read
                    }
                }

                if (buffer.Length < frameLengthHeaderSize) // edge case - frame is less than 4bytes
                {
                    return position; // didn't read anything past start
                }

                var frameLength = Convert.ToInt64(buffer.Slice(frameLengthHeaderSize));
                if (buffer.Length <= frameLength + frameLengthHeaderSize) // can't read anymore - partial message
                {
                    return position;
                }


                // have at least one message inside buffer
                var msg = buffer.Slice(frameLengthHeaderSize, frameLength);
                decoded.Add(msg);
                position = buffer.GetPosition(frameLength + frameLengthHeaderSize);
                buffer = buffer.Slice(position);
            }
        }
    }
}
