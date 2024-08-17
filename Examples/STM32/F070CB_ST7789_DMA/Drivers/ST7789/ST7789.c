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

// Includes
#include "ST7789.h"
#include <stdlib.h>


// Local variables
uint8_t isDisplayLE = ST7789_DEFAULT_ENDIANNESS;
uint8_t ST7789_xShift = 0;
uint8_t ST7789_yShift = 0;

#if defined (ST7789_USE_BUFFERING)
	uint16_t * ST7789_pixelBuffer = NULL;
#if defined (ST7789_USE_DOUBLE_BUFFERING)
	uint16_t ST7789_pixelBufferSize = ST7789_DISPLAY_WIDTH * ST7789_BUFFER_HEIGHT * 2;
	uint16_t ST7789_pixelBufferHalfSize = ST7789_DISPLAY_WIDTH * ST7789_BUFFER_HEIGHT;
	uint8_t ST7789_currentBufferIndex = 0;
#else
	uint16_t ST7789_pixelBufferSize = ST7789_DISPLAY_WIDTH * ST7789_BUFFER_HEIGHT;
#endif
#endif



// Select chip
void ST7789_selectDisplay(GPIO_PinState val) {
#if defined(ST7789_CS_USED)
	HAL_GPIO_WritePin(ST7789_CS_PORT, ST7789_CS_PIN, !val);
#endif
}


// Select either data or command mode
 void ST7789_selectDataMode(uint8_t val) {
	HAL_GPIO_WritePin(ST7789_DC_PORT, ST7789_DC_PIN, val);
}


// Write big endian word
void ST7789_writeBigEndianWord(uint16_t data) {
	uint8_t bigEndianData[2] = {data >> 8, data & 0xFF};
	HAL_SPI_Transmit(&ST7789_SPI_HANDLE, bigEndianData, sizeof(bigEndianData), HAL_MAX_DELAY);
}


// Init w/o parameters
void ST7789_init(void)
{
	ST7789_initWithParams(ST7789_DEFAULT_ROTATION, ST7789_DEFAULT_ENDIANNESS);
}


// Init with parameters
void ST7789_initWithParams(uint8_t rotation, uint8_t littleEndian) {
	// Reset display
	HAL_GPIO_WritePin(ST7789_RST_PORT, ST7789_RST_PIN, GPIO_PIN_RESET);
	HAL_Delay(25);
	HAL_GPIO_WritePin(ST7789_RST_PORT, ST7789_RST_PIN, GPIO_PIN_SET);
	HAL_Delay(50);

	ST7789_begin();
	
	// Set color mode to RGB565
	ST7789_writeCommand(ST7789_COLORMODE);
	ST7789_writeDataByte(ST7789_COLOR_MODE_16bit);

	// Set display RAM control to Little Endian mode
	isDisplayLE = littleEndian;
	if (littleEndian) {
		ST7789_writeCommand(ST7789_RAMCTRL);
		ST7789_writeData((uint8_t *)ST7789_RAMCTRL_PARAMETERS, sizeof(ST7789_RAMCTRL_PARAMETERS));
	}

	// Sleep mode Off
	ST7789_writeCommand(ST7789_SLPOUT);
	// Set display rotation
	ST7789_setRotation(rotation);
	// Turn on the color inversion
	ST7789_writeCommand(ST7789_INVON);
	// Normal Display Mode On
	ST7789_writeCommand(ST7789_NORON);
	// Clear screen
	ST7789_fillColor(0);
	WAIT_DMA;
	// Display On
	ST7789_writeCommand(ST7789_DISPON);

	ST7789_end();
}


// Select the display and allocate buffer
void ST7789_begin() {
#if defined(ST7789_USE_BUFFERING)
	if (ST7789_pixelBuffer == NULL) {
		ST7789_pixelBuffer = (uint16_t *)malloc(ST7789_pixelBufferSize << 1);
	}
#endif
	ST7789_selectDisplay(1);
}


// Deselect the display and free the buffer
void ST7789_end() {
	WAIT_DMA;
	ST7789_selectDisplay(0);
#if defined(ST7789_USE_BUFFERING) && defined(ST7789_RELEASE_BUFFER_AT_END)
	if (ST7789_pixelBuffer) {
		free(ST7789_pixelBuffer);
		ST7789_pixelBuffer = NULL;
	}
#endif
}


// Get current buffer
#if defined (ST7789_USE_BUFFERING)

uint16_t * ST7789_getPixelBuffer() {

#if defined(ST7789_USE_DOUBLE_BUFFERING)
	if (ST7789_currentBufferIndex) {
		return (&ST7789_pixelBuffer[ST7789_pixelBufferHalfSize]);
	} else {
		return (ST7789_pixelBuffer);
	}
#else

	return (ST7789_pixelBuffer);

#endif
}
#endif


// Configure screen rotation
void ST7789_setRotation(uint8_t rotation) {
	ST7789_writeCommand(ST7789_MADCTL);
	ST7789_writeDataByte(ST7789_ROTATION_PARAMETERS[rotation][0]);
	ST7789_xShift = ST7789_ROTATION_PARAMETERS[rotation][1];
	ST7789_yShift = ST7789_ROTATION_PARAMETERS[rotation][2];
}


// Set target window
void ST7789_setWindow(uint8_t x, uint8_t y, uint8_t width, uint8_t height) {
	WAIT_DMA;

	uint16_t address;
	// Column Address set
	ST7789_writeCommand(ST7789_CASET);
	// Write X coordinates
	address = x + ST7789_xShift;
	ST7789_writeDataWord(address);
	address += (width -1);
	ST7789_writeDataWord(address);

	// Row Address set
	ST7789_writeCommand(ST7789_RASET);
	// Write Y coordinates
	address = y + ST7789_yShift;
	ST7789_writeDataWord(address);
	address += (height - 1);
	ST7789_writeDataWord(address);

	// Write to RAM
	ST7789_writeCommand(ST7789_RAMWR);
	ST7789_selectDataMode(1);
}


// Fill screen with solid color
void ST7789_fillColor(uint16_t color) {
	ST7789_setWindow(0, 0, ST7789_DISPLAY_WIDTH, ST7789_DISPLAY_HEIGHT);
	ST7789_fillPixels(color, ST7789_DISPLAY_WIDTH * ST7789_DISPLAY_HEIGHT);
}


// Fill configured window with pixels
void ST7789_fillPixels(uint16_t color, uint16_t count) {
	// Prepare color in the required endianness
	if (!isDisplayLE) color = __builtin_bswap16(color);

#if defined(ST7789_USE_BUFFERING)	
	// Prepare buffer
	uint16_t usedBufferPixels = (count > ST7789_pixelBufferSize) ? ST7789_pixelBufferSize : count;
	for (uint16_t i = 0; i < usedBufferPixels; i++) ST7789_pixelBuffer[i] = color;

	while (count) {

#if defined(ST7789_USE_DOUBLE_BUFFERING)
		uint16_t pixelsToFill = (count > ST7789_pixelBufferHalfSize) ? ST7789_pixelBufferHalfSize : count;
#else
		uint16_t pixelsToFill = (count > ST7789_pixelBufferSize) ? ST7789_pixelBufferSize : count;
#endif

		ST7789_writePixels(ST7789_getPixelBuffer(), pixelsToFill, isDisplayLE);
		count -= pixelsToFill;
	}

#else
	for (uint16_t pixel = 0; pixel < ST7789_DISPLAY_WIDTH * ST7789_DISPLAY_HEIGHT; pixel++) {
		HAL_SPI_Transmit(&ST7789_SPI_HANDLE, (uint8_t *)&color, sizeof(color), HAL_MAX_DELAY);
	}
#endif
}


// Write next pixel
void ST7789_writePixel(uint16_t color) {
	if (isDisplayLE) color = __builtin_bswap16(color);
	ST7789_writeBigEndianWord(color);
}


// Write next chunk of pixels
void ST7789_writePixels(uint16_t * pixels, uint16_t count, uint8_t littleEndian) {
	// If display is the same endianness as our data
	// then we can use DMA
	if (isDisplayLE == littleEndian) {
#if defined(ST7789_USE_DMA)

		WAIT_DMA;
		HAL_SPI_Transmit_DMA(&ST7789_SPI_HANDLE, (uint8_t *)pixels, count << 1);

#if defined(ST7789_USE_BUFFERING) && defined(ST7789_USE_DOUBLE_BUFFERING)
		ST7789_currentBufferIndex = !ST7789_currentBufferIndex;
#endif

#else
		// Or just transmit the data
		ST7789_writeData((uint8_t *)pixels, count << 1);

#endif

	}
	// Else we have to make sure that the endianness is correct for every pixel
	else
	{
		for(uint16_t i = 0; i < count; i++) ST7789_writeBigEndianWord(pixels[i]);
	}
}


// Write command
void ST7789_writeCommand(uint8_t command) {
	ST7789_selectDataMode(0);
	HAL_SPI_Transmit(&ST7789_SPI_HANDLE, &command, sizeof(command), HAL_MAX_DELAY);
}


// Write one byte of data
void ST7789_writeDataByte(uint8_t data) {
	ST7789_selectDataMode(1);
	HAL_SPI_Transmit(&ST7789_SPI_HANDLE, &data, sizeof(data), HAL_MAX_DELAY);
}


// Write one word of data with endianness control
// (for pixel use ST7789_writePixel).
void ST7789_writeDataWord(uint16_t data) {
	ST7789_selectDataMode(1);
	ST7789_writeBigEndianWord(data);
}


// Write a chunk of data
// (for pixels use ST7789_writePixels).
void ST7789_writeData(uint8_t * data, uint16_t size) {
	ST7789_selectDataMode(1);
	HAL_SPI_Transmit(&ST7789_SPI_HANDLE, data, size, HAL_MAX_DELAY);
}


// END-OF-FILE
