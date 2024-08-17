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

////////////////////////////////////////////////////////////////////////////////////////

/*

This file is part of the most complete decompression class, which has extensive
decompression capabilities for various source data formats. 
Both this class and all its modifications may not be very effective in terms of performance
or non-standard usage, so they can (and should) be modified for each specific task.

As examples, I have made several modifications to the decompression code that differ in capabilities and performance:

1) The original class (THIS ONE) - maximum features:
	- supported image size up to 65535x65535px,
	- up to 32 bits per data channel,
	- up to 64 bits per frame (modification is required for a larger frame width),
	- decompression per 1 request: up to 2^32 frames (pixels), up to 2^32 compressed/decompressed data size,
	- decompression of 1-2-4 bits frames,
	- AVR "far" memory access support*,
	- splitting compressed data into memory chunks (up to 32767 bytes)**.
Source: /Decompression
Example: /AVR/Arduino_boards/Mega2560_ILI9486 (ATMega2560 + ILI9486 480x320 display).

2) High-speed adaptation:
	- focusing on the low-bit-length operations,
	- supported image size is up to 255x255px,
	- up to 8 bits per data channel,
	- up to 16 bits per frame,
	- decompression of 1-2-4 bits frames,
	- AVR "far" memory access support*,
	- splitting compressed data into memory chunks (up to 32767 bytes)**,
	- high performance.
Source: /Decompression/Mods/Fast
Examples:
	/AVR/Arduino_boards/Mega2560_ILI9486_Fast (ATMega2560 + ILI9486 480x320 display).

3) High-speed with pixel support:
	- focusing on the low-bit-length operations,
	- supported image size is up to 255x255px,
	- up to 8 bits per data channel,
	- up to 16 bits per frame,
	- colorizing pixels during decompression (if used),
	- direct use of the RGB565 pixel buffer (if needed),
	- AVR "far" memory access support*,
	- splitting compressed data into memory chunks (up to 32767 bytes)**,
	- high performance+.
Source: /Decompression/Mods/Fast+pixel_support
Examples:
	/AVR/Arduino_boards/Nano_ST7789 (Arduino Nano + ST7789 240x240 display),
	/ESP32/Wroom_ST7789 (ESP32 DEVKIT board + ST7789 240x240 display),
	/STM32/F070CB_ST7789_DMA (STM32F070CB chip + ST7789 240x240 display),
	/STM32/F407ZGT6_NT35510_FSMC_DMA (mcudev DevEBox board + NT35510 800x480 display).

4) Tiny adaptation:
	- focusing on the 8-bit operations,
	- supported image size is up to 255x255px,
	- up to 8 bits per data channel,
	- up to 8 bits per frame supported,
	- colorizing pixels during decompression (if used),
	- direct use of the RGB565 pixel buffer (if needed),
	- high performance++.
Source: /Decompression/Mods/Tiny+pixel_support
Examples:
	/AVR/Arduino_boards/Nano_ST7789_Tiny (Arduino Nano + ST7789 240x240 display),
	/AVR/ATtiny85 (ATtiny85 chip + SPI output).

* Relevant only for the AVR platform.
** To do this, a pointer to an array of far pointers should be passed 
to the decompression procedure instead of a direct pointer to a data array:

static uint_farptr_t dataChunks[n];
decompressNextFrames(dc, dataChunks, decompressedData, framesCount);


////////////////////////////////////////////////////////////////////////////////////////

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

////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////

// Includes
#include <stdint.h>

#if defined(AVR)
#include <avr/pgmspace.h>
#endif

#ifndef _DELTA_DECOMPRESSION_
#define _DELTA_DECOMPRESSION_

// Global definitions

// Use only one option that matches your data.
// Using DECOMPRESSION_FIXED_WINDOW_FIRST, you can decompress data 
// compressed not only by this method (Fixed + Adaptive), but also
// a data, compressed by Fixed Window only.
// Likewise using DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST, 
// you can decompress data, compressed both by Adaptive + Fixed
// and only by Adaptive Window methods.
////////////////////////////////////////////////////////////////////////////////////////
//#define DECOMPRESSION_FIXED_WINDOW_ONLY
//#define DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_ONLY
#define DECOMPRESSION_FIXED_WINDOW_FIRST
//#define DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST
////////////////////////////////////////////////////////////////////////////////////////

// The maximum number of channels in the data that you will decompress
#define DECOMPRESSION_MAX_NUMBER_OF_CHANNELS	3

// Uncomment if you have data splitted into squares among those you will decompress
#define DECOMPRESSION_USE_SQUARES

// Uncomment if your data contains frames of 1, 2 or 4 bits
//#define FRAMES_LESS_THAN_BYTE_USED

#if defined(AVR)
// Maximum array size for the AVR platform is 32767 bytes,
// so if we want to use large arrays of compressed data, 
// then we must put the array in the "far" memory area and split it into chunks.
// Hide the string below if you don't want to use far memory and chunks.
#define USE_FAR_MEMORY_CHUNKS
// For example, we can divide the data by 16K chunks.
#define CHUNK_SIZE	16384

#endif


////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////

struct DecompressionContext
{
	uint8_t numberOfChannels;
	uint8_t bitsPerChannels[DECOMPRESSION_MAX_NUMBER_OF_CHANNELS];
	uint8_t blockSizeBits;
	uint8_t bitsPerMethodDeclaration[DECOMPRESSION_MAX_NUMBER_OF_CHANNELS];
	uint8_t bitsPerFrame;
#if defined(DECOMPRESSION_USE_SQUARES)
	bool splitToSquares;
	uint16_t imageWidth;
	uint8_t squareSide;
	uint16_t squaresPerWidth;
	uint16_t squaresPerWidthSize;
#endif
};


// Delegate method declaration
void frameDecompressed(uint64_t frame);


////////////////////////////////////////////////////////////////////////////////////////

class Decompression {

public:

	uint64_t inputBitsBuffer;
#if defined(USE_FAR_MEMORY_CHUNKS)
	uint8_t inputChunkIndex; // Additional variable for ROM chunk index
#endif
	uint32_t inputByteIndex;
	uint8_t inputBitsCount;

	uint32_t currentBlockFramesCount;
	uint8_t currentBlockMethods[DECOMPRESSION_MAX_NUMBER_OF_CHANNELS];
	uint32_t currentBaseValues[DECOMPRESSION_MAX_NUMBER_OF_CHANNELS];

	uint64_t outputBitsBuffer; // For larger frames we need wider variable, but uint64 is all we can afford.
	uint8_t outputBitsCount;

	// In the case of decompression images divided into squares, 
	// set this variable to the width of your decompression buffer in pixels.
	uint16_t bufferWidth;

	void resetDecompression();
#if defined(USE_FAR_MEMORY_CHUNKS)
	void decompressNextFrames(DecompressionContext * dc, uint_farptr_t * compressedData, uint8_t * decompressedData, uint32_t framesCount);
#else
	void decompressNextFrames(DecompressionContext * dc, uint8_t * compressedData, uint8_t * decompressedData, uint32_t framesCount);
#endif

private:

#if defined(USE_FAR_MEMORY_CHUNKS)
	uint32_t readNextBits(uint_farptr_t * compressedData, uint8_t bitsCount);
#else
	uint32_t readNextBits(uint8_t * compressedData, uint8_t bitsCount);
#endif

};

#endif // end _DELTA_DECOMPRESSION_

// END-OF-FILE
