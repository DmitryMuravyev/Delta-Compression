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
#include "NT35510.h"
#include <stdlib.h>


// Local variables
static uint16_t NT35510_width = NT35510_DISPLAY_WIDTH;
static uint16_t NT35510_height = NT35510_DISPLAY_HEIGHT;
static __IO uint16_t * reg_pointer = (__IO uint16_t *)NT35510_REG_ADDR;
static __IO uint16_t * data_pointer = (__IO uint16_t *)NT35510_DATA_ADDR;

#if defined (NT35510_USE_BUFFERING)
	uint16_t * NT35510_pixelBuffer = NULL;
#if defined (NT35510_USE_DOUBLE_BUFFERING)
	uint16_t NT35510_pixelBufferSize = 0;
	uint16_t NT35510_pixelBufferHalfSize = 0;
	uint8_t NT35510_currentBufferIndex = 0;
#else
	uint16_t NT35510_pixelBufferSize = 0;
#endif
#endif


// Init w/o parameters
void NT35510_init(void)
{
	NT35510_initWithParams(NT35510_DEFAULT_ROTATION);
}


// Init with parameters
void NT35510_initWithParams(uint8_t rotation) {
	// Reset display
#if defined(NT35510_RST_USED)
	HAL_GPIO_WritePin(NT35510_RST_PORT, NT35510_RST_PIN, GPIO_PIN_RESET);
	HAL_Delay(25);
	HAL_GPIO_WritePin(NT35510_RST_PORT, NT35510_RST_PIN, GPIO_PIN_SET);
	HAL_Delay(50);
#endif

	// Lots of Chinese magic for stable display work
	// (otherwise there may be some problems)
	NT35510_writeCommand(0xF000); NT35510_writeDataByte(0x55);
	NT35510_writeCommand(0xF001); NT35510_writeDataByte(0xAA);
	NT35510_writeCommand(0xF002); NT35510_writeDataByte(0x52);
	NT35510_writeCommand(0xF003); NT35510_writeDataByte(0x08);
	NT35510_writeCommand(0xF004); NT35510_writeDataByte(0x01);
	//# AVDD: manual
	NT35510_writeCommand(0xB600); NT35510_writeDataByte(0x34);
	NT35510_writeCommand(0xB601); NT35510_writeDataByte(0x34);
	NT35510_writeCommand(0xB602); NT35510_writeDataByte(0x34);

	NT35510_writeCommand(0xB000); NT35510_writeDataByte(0x0D);
	NT35510_writeCommand(0xB001); NT35510_writeDataByte(0x0D);
	NT35510_writeCommand(0xB002); NT35510_writeDataByte(0x0D);
	//# AVEE: manual
	NT35510_writeCommand(0xB700); NT35510_writeDataByte(0x24);
	NT35510_writeCommand(0xB701); NT35510_writeDataByte(0x24);
	NT35510_writeCommand(0xB702); NT35510_writeDataByte(0x24);

	NT35510_writeCommand(0xB100); NT35510_writeDataByte(0x0D);
	NT35510_writeCommand(0xB101); NT35510_writeDataByte(0x0D);
	NT35510_writeCommand(0xB102); NT35510_writeDataByte(0x0D);
	//#Power Control for
	//VCL
	NT35510_writeCommand(0xB800); NT35510_writeDataByte(0x24);
	NT35510_writeCommand(0xB801); NT35510_writeDataByte(0x24);
	NT35510_writeCommand(0xB802); NT35510_writeDataByte(0x24);

	NT35510_writeCommand(0xB200); NT35510_writeDataByte(0x00);

	//# VGH: Clamp Enable);
	NT35510_writeCommand(0xB900); NT35510_writeDataByte(0x24);
	NT35510_writeCommand(0xB901); NT35510_writeDataByte(0x24);
	NT35510_writeCommand(0xB902); NT35510_writeDataByte(0x24);

	NT35510_writeCommand(0xB300); NT35510_writeDataByte(0x05);
	NT35510_writeCommand(0xB301); NT35510_writeDataByte(0x05);
	NT35510_writeCommand(0xB302); NT35510_writeDataByte(0x05);
	//# VGL(LVGL):
	NT35510_writeCommand(0xBA00); NT35510_writeDataByte(0x34);
	NT35510_writeCommand(0xBA01); NT35510_writeDataByte(0x34);
	NT35510_writeCommand(0xBA02); NT35510_writeDataByte(0x34);
	//# VGL_REG(VGLO)
	NT35510_writeCommand(0xB500); NT35510_writeDataByte(0x0B);
	NT35510_writeCommand(0xB501); NT35510_writeDataByte(0x0B);
	NT35510_writeCommand(0xB502); NT35510_writeDataByte(0x0B);
	//# VGMP/VGSP:
	NT35510_writeCommand(0xBC00); NT35510_writeDataByte(0X00);
	NT35510_writeCommand(0xBC01); NT35510_writeDataByte(0xA3);
	NT35510_writeCommand(0xBC02); NT35510_writeDataByte(0X00);
	//# VGMN/VGSN
	NT35510_writeCommand(0xBD00); NT35510_writeDataByte(0x00);
	NT35510_writeCommand(0xBD01); NT35510_writeDataByte(0xA3);
	NT35510_writeCommand(0xBD02); NT35510_writeDataByte(0x00);
	//# VCOM=-0.1
	NT35510_writeCommand(0xBE00); NT35510_writeDataByte(0x00);
	NT35510_writeCommand(0xBE01); NT35510_writeDataByte(0x63);
	// End of magic

	// Sleep mode Off
	NT35510_writeCommand(NT35510_SLPOUT);

	// Set color mode to RGB565
	NT35510_writeCommand(NT35510_COLORMODE);
	NT35510_writeDataByte(NT35510_COLOR_MODE_16bit);

	// Normal Display Mode On
	NT35510_writeCommand(NT35510_NORON);

	// Set display rotation
	NT35510_setRotation(rotation);

	// Clear screen
	NT35510_begin();
	NT35510_fillColor(0);
	NT35510_end();

	// Display On
	NT35510_writeCommand(NT35510_DISPON);
}


// Select the display and allocate buffer
void NT35510_begin() {
#if defined(NT35510_USE_BUFFERING)
	if (NT35510_pixelBuffer == NULL) {
		NT35510_pixelBuffer = (uint16_t *)malloc(NT35510_pixelBufferSize << 1);
	}
#endif
}


// Deselect the display and free the buffer
void NT35510_end() {
	WAIT_DMA;
#if defined(NT35510_USE_BUFFERING) && defined(NT35510_RELEASE_BUFFER_AT_END)
	if (NT35510_pixelBuffer) {
		free(NT35510_pixelBuffer);
		NT35510_pixelBuffer = NULL;
	}
#endif
}


// Get current buffer
#if defined (NT35510_USE_BUFFERING)

uint16_t * NT35510_getPixelBuffer() {

#if defined(NT35510_USE_DOUBLE_BUFFERING)
	if (NT35510_currentBufferIndex) {
		return (&NT35510_pixelBuffer[NT35510_pixelBufferHalfSize]);
	} else {
		return (NT35510_pixelBuffer);
	}
#else

	return (NT35510_pixelBuffer);

#endif
}
#endif


// Configure screen rotation
void NT35510_setRotation(uint8_t rotation) {
	NT35510_writeCommand(NT35510_MADCTL);
	NT35510_writeDataWord(NT35510_ROTATION_PARAMETERS[rotation][0]);
	NT35510_width = NT35510_ROTATION_PARAMETERS[rotation][1];
	NT35510_height = NT35510_ROTATION_PARAMETERS[rotation][2];

#if defined(NT35510_USE_BUFFERING)
	NT35510_end();
#if defined(NT35510_USE_DOUBLE_BUFFERING)
	NT35510_pixelBufferHalfSize = NT35510_width * NT35510_BUFFER_HEIGHT;
	NT35510_pixelBufferSize = NT35510_pixelBufferHalfSize << 1;
	NT35510_currentBufferIndex = 0;
#else
	NT35510_pixelBufferSize = NT35510_width * NT35510_BUFFER_HEIGHT;
#endif
#endif
}


// Set target window
void NT35510_setWindow(uint16_t x, uint16_t y, uint16_t width, uint16_t height) {
	WAIT_DMA;

	// Column Address set
	NT35510_writeCommand(NT35510_CASET);

	NT35510_writeDataWord(x);
	NT35510_writeCommand(NT35510_CASET + 2);
	NT35510_writeDataWord(x + width - 1);

	// Row Address set
	NT35510_writeCommand(NT35510_RASET);
	NT35510_writeDataWord(y);
	NT35510_writeCommand(NT35510_RASET + 2);
	NT35510_writeDataWord(y + height - 1);

	// Write to RAM
	NT35510_writeCommand(NT35510_RAMWR);
}


// Fill screen with solid color
void NT35510_fillColor(uint16_t color) {
	NT35510_setWindow(0, 0, NT35510_width, NT35510_height);
	NT35510_fillPixels(color, NT35510_DISPLAY_WIDTH * NT35510_DISPLAY_HEIGHT);
}


// Fill configured window with pixels
void NT35510_fillPixels(uint16_t color, uint32_t count) {
#if defined(NT35510_USE_BUFFERING)
	// Prepare buffer
	//color = __builtin_bswap16(color);
	uint16_t usedBufferPixels = (count > NT35510_pixelBufferSize) ? NT35510_pixelBufferSize : count;
	for (uint16_t i = 0; i < usedBufferPixels; i++) NT35510_pixelBuffer[i] = color;

	while (count) {

#if defined(NT35510_USE_DOUBLE_BUFFERING)
		uint16_t pixelsToFill = (count > NT35510_pixelBufferHalfSize) ? NT35510_pixelBufferHalfSize : count;
#else
		uint16_t pixelsToFill = (count > NT35510_pixelBufferSize) ? NT35510_pixelBufferSize : count;
#endif

		NT35510_writePixels(NT35510_getPixelBuffer(), pixelsToFill);
		count -= pixelsToFill;
	}

#else
	for (uint32_t pixel = 0; pixel < NT35510_DISPLAY_WIDTH * NT35510_DISPLAY_HEIGHT; pixel++) {
		NT35510_writeDataWord(color);
	}
#endif
}


// Write next pixel
void NT35510_writePixel(uint16_t color) {
	NT35510_writeDataWord(color);
}


// Write next chunk of pixels
void NT35510_writePixels(uint16_t * pixels, uint16_t count) {
#if defined(NT35510_USE_DMA)

	WAIT_DMA;
	HAL_SRAM_Write_DMA(&NT35510_SRAM_HANDLE, (uint32_t *)data_pointer, (uint32_t *)pixels, count);

#if defined(NT35510_USE_BUFFERING) && defined(NT35510_USE_DOUBLE_BUFFERING)
	NT35510_currentBufferIndex = !NT35510_currentBufferIndex;
#endif

#else
	// Or just transmit the data
	NT35510_writeDataWords(pixels, count);

#endif
}


// Write command
void NT35510_writeCommand(uint16_t command) {
	*reg_pointer = command;
}


// Write one byte of data
void NT35510_writeDataByte(uint8_t data) {
	*data_pointer = data;
}


// Write one word of data with endianness control
// (for pixel use NT35510_writePixel).
void NT35510_writeDataWord(uint16_t data) {
	*data_pointer = data;
}


// Write a chunk of 8-bit data
// (for pixels use NT35510_writePixels).
void NT35510_writeDataBytes(uint8_t * data, uint16_t size) {
	for(int i = 0; i < size; i++) {
		NT35510_writeDataByte(data[i]);
	}
}

// Write a chunk of 16-bit data
// (for pixels use NT35510_writePixels).
void NT35510_writeDataWords(uint16_t * data, uint16_t size) {
	for(int i = 0; i < size; i++) {
		NT35510_writeDataWord(data[i]);
	}
}

// END-OF-FILE
