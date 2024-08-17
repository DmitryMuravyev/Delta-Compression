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
#if defined(AVR)
#include <avr/pgmspace.h>
#endif

////////////////////////////////////////////////////////////////////////////////////////

// Weak delegate method
void __attribute__((weak)) frameDecompressed(uint8_t frame) {
	// Do not delete this function
}

// Another one delegate method to colorize grayscale pixels
uint16_t __attribute__((weak)) colorizePixel(uint8_t frame) {
	// Do not delete this function
	return (frame);
}


////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////

// Clear runtime variables
void Decompression::resetDecompression()
{
	inputBitsBuffer = 0;
	inputByteIndex = 0;
	inputBitsCount = 0;

	currentBlockFramesCount = 0;

	outputBitsBuffer = 0;
	outputBitsCount = 0;
}

////////////////////////////////////////////////////////////////////////////////////////

// Batch decompression (specified number of frames)
// Let's change buffer type to our pixel formats, i.e. uint16_t (RGB565, RGB555, RGB444),
// and add colorize flag to call another one delegate method.
void Decompression::decompressNextFrames(DecompressionContext * dc, uint8_t * compressedData, uint16_t * decompressedData, uint16_t framesCount, bool colorize)
{
#if defined(DECOMPRESSION_USE_SQUARES)
	uint8_t row = 0;
	uint8_t column = 0;
	uint8_t squareOffset = 0;
	uint16_t squareLineOffset = 0;
#endif

	uint16_t dataOffset = 0;

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
					uint8_t frame = (outputBitsBuffer >> (outputBitsCount -= dc->bitsPerFrame)) & MASK8_FOR_BITS_COUNT(dc->bitsPerFrame);

					if (decompressedData)
					{
						decompressedData[dataOffset++] = colorize ? colorizePixel(frame) : frame;
					}
					else
					{
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

				// We will use a buffer consisting of whole pixels, i.e. in our case it is uint16_t or 2 bytes.
				// Therefore, we do not need to calculate the offset inside the buffer in bytes/bits.
				if (rowChanged) dataOffset = (squareLineOffset + squareOffset + row * bufferWidth + column);
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

// END-OF-FILE
