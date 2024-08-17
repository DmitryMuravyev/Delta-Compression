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

// Auxilary macro
#define MASK8_FOR_BITS_COUNT(x)  (0xFF >> (8 - x))
#define MASK16_FOR_BITS_COUNT(x)  (0xFFFF >> (16 - x))

// Includes
#include "Decompression.h"
#include <stdlib.h>

////////////////////////////////////////////////////////////////////////////////////////

// Weak delegate method
void __attribute__((weak)) frameDecompressed(uint16_t frame) {
	// Do not delete this function
}


////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////

// Clear runtime variables
void Decompression::resetDecompression()
{
	inputBitsBuffer = 0;
#if defined(USE_FAR_MEMORY_CHUNKS)
	inputChunkIndex = 0;
#endif
	inputByteIndex = 0;
	inputBitsCount = 0;

	currentBlockFramesCount = 0;

	outputBitsBuffer = 0;
	outputBitsCount = 0;
}

////////////////////////////////////////////////////////////////////////////////////////

// Batch decompression (specified number of frames)
#if defined(USE_FAR_MEMORY_CHUNKS)
void Decompression::decompressNextFrames(DecompressionContext * dc, uint_farptr_t * compressedData, uint8_t * decompressedData, uint16_t framesCount)
#else
void Decompression::decompressNextFrames(DecompressionContext * dc, uint8_t * compressedData, uint8_t * decompressedData, uint16_t framesCount)
#endif
{
#if defined(DECOMPRESSION_USE_SQUARES)
	uint8_t row = 0;
	uint8_t column = 0;
	uint16_t squareOffset = 0;
	uint16_t squareLineOffset = 0;
#endif

	uint16_t dataOffset = 0;

#if defined(FRAMES_LESS_THAN_BYTE_USED)
	uint8_t bitOffset = 0;
#endif

#if !defined(DECOMPRESSION_FIXED_WINDOW_ONLY)
	bool * afw = (bool *)malloc(dc->numberOfChannels);
	bool firstFrame = false;
#endif

	while (framesCount > 0)
	{
		if (currentBlockFramesCount == 0)
		{
			currentBlockFramesCount = readNextBits(compressedData, dc->blockSizeBits);
			currentBlockFramesCount++; // 0 value doesn't make any sense to us, so let's get rid of it to save bits.

#if !defined(DECOMPRESSION_FIXED_WINDOW_ONLY)
			firstFrame = true;
#endif

			for (uint8_t c = 0; c < dc->numberOfChannels; c++)
			{
				currentBlockMethods[c] = readNextBits(compressedData, dc->bitsPerMethodDeclaration[c]);

				if (currentBlockMethods[c] != dc->bitsPerChannels[c])
				{
					currentBaseValues[c] = readNextBits(compressedData, dc->bitsPerChannels[c]);

#if defined(DECOMPRESSION_FIXED_WINDOW_FIRST) || defined(DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST)
					if (currentBlockMethods[c] > dc->bitsPerChannels[c])
					{
						currentBlockMethods[c] -= dc->bitsPerChannels[c];

# if defined(DECOMPRESSION_FIXED_WINDOW_FIRST)
						afw[c] = true;
# elif defined(DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST)
						afw[c] = false;
# endif

					}
					else
					{

# if defined(DECOMPRESSION_FIXED_WINDOW_FIRST)
						afw[c] = false;
# elif defined(DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST)
						afw[c] = true;
# endif

					}

#elif defined(DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_ONLY)
					afw[c] = true;
#endif

				}
				else
				{
					currentBaseValues[c] = 0;

#if !defined(DECOMPRESSION_FIXED_WINDOW_ONLY)
					afw[c] = false;
#endif

				}
			}
		}

		while ((framesCount > 0) && (currentBlockFramesCount > 0))
		{
			for (uint8_t c = 0; c < dc->numberOfChannels; c++)
			{
				uint8_t decompressedValue = currentBaseValues[c];
				if (currentBlockMethods[c]) {

#if !defined(DECOMPRESSION_FIXED_WINDOW_ONLY)
					if (afw[c])
					{
						if (!firstFrame)
						{            
							uint8_t windowWidth = (uint8_t)1 << currentBlockMethods[c];
							uint8_t halfWindowWidth = windowWidth >> 1;
							uint8_t maxWindowStart = MASK8_FOR_BITS_COUNT(dc->bitsPerChannels[c]) - windowWidth + 1;

							if (halfWindowWidth < decompressedValue) {
								decompressedValue -= halfWindowWidth;
								if (decompressedValue > maxWindowStart) decompressedValue = maxWindowStart;
							} 
							else
							{
								decompressedValue = 0;
							}
							decompressedValue += readNextBits(compressedData, currentBlockMethods[c]);
							currentBaseValues[c] = decompressedValue;
						}
					}
					else
#endif

					{
						decompressedValue += readNextBits(compressedData, currentBlockMethods[c]);
					}
				}

				// Save bits or optionally call delegate for convertion to appropriate format
				outputBitsBuffer <<= dc->bitsPerChannels[c];
				outputBitsBuffer |= decompressedValue;
				outputBitsCount += dc->bitsPerChannels[c];

				if (outputBitsCount >= dc->bitsPerFrame)
				{
					if (decompressedData && ((dc->bitsPerFrame % 8) == 0))
					{
						while (outputBitsCount >= 8)
						{
							uint8_t newByte = (outputBitsBuffer >> (outputBitsCount -= 8)) & 0xFF;
							decompressedData[dataOffset++] = newByte;
						}
						// Frame saved, continue to the next one
						continue;
					}

					uint16_t frame = (outputBitsBuffer >> (outputBitsCount -= dc->bitsPerFrame)) & MASK16_FOR_BITS_COUNT(dc->bitsPerFrame);

#if defined(FRAMES_LESS_THAN_BYTE_USED)
					if (decompressedData && ((8 % dc->bitsPerFrame) == 0))
					{
						uint8_t storedBufferData = (bitOffset == 0) ? 0 : decompressedData[dataOffset];

						storedBufferData |= (frame << (8 - dc->bitsPerFrame - bitOffset));
						decompressedData[dataOffset] = storedBufferData;

						bitOffset += dc->bitsPerFrame;
						if (bitOffset >= 8)
						{
							dataOffset++;
							bitOffset = 0;
						}
					}
					else
#endif

					{
						// The frame width is not a multiple of 1, 2, 4 or 8 bits, 
						// or the output buffer is not set, so we must ask the delegate for help.
						frameDecompressed(frame);
					}

				}
			}

#if defined(DECOMPRESSION_USE_SQUARES)
			// Calculate data offset within the buffer if split into squares used
			bool rowChanged = false;
			if (dc->splitToSquares)
			{
				column++;
				if (column >= dc->squareSide)
				{
					row++;
					rowChanged = true;
					if (row >= dc->squareSide)
					{
						squareOffset += dc->squareSide;
						if (squareOffset >= dc->imageWidth)
						{
							squareLineOffset += dc->squaresPerWidthSize;
							squareOffset = 0;
						}
						row = 0;
					}
					column = 0;
				}

				if (rowChanged)
				{

#if defined(FRAMES_LESS_THAN_BYTE_USED)
					uint32_t bitsDataOffset = (squareLineOffset + squareOffset + row * bufferWidth + column) * dc->bitsPerFrame;
					dataOffset = bitsDataOffset >> 3;
					bitOffset = bitsDataOffset - (dataOffset << 3);
#else
					dataOffset = (squareLineOffset + squareOffset + row * bufferWidth + column) * (dc->bitsPerFrame >> 3);
#endif

				}
			}
#endif

#if !defined(DECOMPRESSION_FIXED_WINDOW_ONLY)
			firstFrame = false;
#endif

			framesCount--;
			currentBlockFramesCount--;
		}
	}

#if !defined(DECOMPRESSION_FIXED_WINDOW_ONLY)
	free(afw);
#endif

}

// Read compressed data (two options for near and far memory)
#if defined(USE_FAR_MEMORY_CHUNKS)

uint16_t Decompression::readNextBits(uint_farptr_t * compressedData, uint8_t bitsCount)
{
	if (!bitsCount) return (0);
	// Get the far address
	uint_farptr_t currentChunk = compressedData[inputChunkIndex];
	while (inputBitsCount < bitsCount) {
		inputBitsBuffer <<= 8;
		inputBitsBuffer |= pgm_read_byte_far(currentChunk + inputByteIndex); // Read far byte
		inputBitsCount += 8;
		inputByteIndex++;

		// Check if next address is outside the chunk
		if (inputByteIndex >= CHUNK_SIZE) {
			inputChunkIndex++;
			inputByteIndex = 0;
		}
	}

	uint16_t bits = (inputBitsBuffer >> (inputBitsCount - bitsCount)) & MASK16_FOR_BITS_COUNT(bitsCount);
	inputBitsCount -= bitsCount;
	return (bits);
}

#else

uint16_t Decompression::readNextBits(uint8_t * compressedData, uint8_t bitsCount)
{
	if (!bitsCount) return (0);
	// Get the far address
	while (inputBitsCount < bitsCount) {
		inputBitsBuffer <<= 8;
  
#if defined(AVR)
		inputBitsBuffer |= pgm_read_byte(compressedData + inputByteIndex);
#else
		inputBitsBuffer |= compressedData[inputByteIndex];
#endif
		inputBitsCount += 8;
		inputByteIndex++;
	}
  
	uint16_t bits = (inputBitsBuffer >> (inputBitsCount - bitsCount)) & MASK16_FOR_BITS_COUNT(bitsCount);
	inputBitsCount -= bitsCount;
	return (bits);
}

#endif

// END-OF-FILE
