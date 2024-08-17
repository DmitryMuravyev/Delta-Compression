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

// Very simple "driver" which allows you to output images
// or individual pixels on a display based on the ST7789 controller.
// It is designed only for use with 240x240 pixel displays.
// Some extra modifications required for other displays.
// Also it supports DMA and double buffering.

// ATTENTION! For the normal operation of DMA and buffering,
// it is necessary that the display byte order corresponds
// to the byte order of the transmitted pixels.

// Includes
#include <stdint.h>
#include "stm32f0xx_hal.h"


#ifndef _ST7789_
#define _ST7789_

// Global definitions
#define BIG_ENDIAN					0
#define LITTLE_ENDIAN				1

// Display configuration
#define ST7789_DEFAULT_ROTATION		2   // 0-3, 2 - normal top to bottom
#define ST7789_DEFAULT_ENDIANNESS	BIG_ENDIAN

// At the moment only one screen resolution supported
// (support for other resolutions can be easily added)
#define ST7789_DISPLAY_WIDTH		240
#define ST7789_DISPLAY_HEIGHT		240

// The name of external SPI_HandleTypeDef variable (defined in "main.c")
#define ST7789_SPI_HANDLE			hspi1

// If you plan to use DMA, declare this symbol
#define ST7789_USE_DMA

// You can shift the responsibility for the buffer to this module.
// In addition, even without DMA, using a buffer speeds up
// the painting of rectangular areas in one color.
#define ST7789_USE_BUFFERING

// Double buffering can be used together with DMA.
// One of the buffers is transferring to the display,
// while the second one is rendering. Then they are swapped.
//#define ST7789_USE_DOUBLE_BUFFERING

// You can free the buffer during the "end()" call to avoid
// wasting RAM when no display operations are performed.
//#define ST7789_RELEASE_BUFFER_AT_END

// The buffer size in pixels will be defined as
// the display width multiplied by this number.
// Also you can change the buffer size as you want,
// changing ST7789_pixelBufferSize / ST7789_pixelBufferHalfSize
// variables outside of the begin() / end() calls.
#define ST7789_BUFFER_HEIGHT		2

// Ports/pins (you can include the "main.h" file and use the symbols defined in it.
#define ST7789_RST_PORT				GPIOA
#define ST7789_RST_PIN				GPIO_PIN_4
#define ST7789_DC_PORT				GPIOB
#define ST7789_DC_PIN				GPIO_PIN_2

// Uncomment and configure port/pin if Chip Select pin used
//#define ST7789_CS_USED
#if defined(ST7789_CS_USED)
	#define ST7789_CS_PORT			GPIOA
	#define ST7789_CS_PIN			GPIO_PIN_1
#endif


// Display commands
#define ST7789_COLORMODE			0x3A
#define ST7789_RAMCTRL				0xB0
#define ST7789_MADCTL				0x36
#define ST7789_INVON				0x21
#define ST7789_SLPOUT				0x11
#define ST7789_NORON				0x13
#define ST7789_DISPON				0x29
#define ST7789_CASET				0x2A
#define ST7789_RASET				0x2B
#define ST7789_RAMWR				0x2C

// Command arguments
// Page Address Order: 0 = Top to Bottom, 1 = Bottom to Top.
#define ST7789_MADCTL_MY			0x80
// Column Address Order: 0 = Left to Right, 1 = Right to Left.
#define ST7789_MADCTL_MX			0x40
// Page/Column Order: 0 = Normal Mode, 1 = Reverse Mode.
#define ST7789_MADCTL_MV			0x20
// Line Address Order: 0 = LCD Refresh Top to Bottom, 1 = LCD Refresh Bottom to Top.
#define ST7789_MADCTL_ML			0x10
// RGB/BGR Order: 0 = RGB, 1 = BGR.
#define ST7789_MADCTL_RGB			0x00
// Display Data Latch Data Order: 0 = LCD Refresh Left to Right, 1 = LCD Refresh Right to Left.
#define ST7789_MADCTL_MH			0x00

static const uint8_t ST7789_COLOR_MODE_16bit = 0x55;
static const uint8_t ST7789_RAMCTRL_PARAMETERS[2] = { 0x00, 0xE8 };
static const uint8_t ST7789_ROTATION_PARAMETERS[4][3] = { { ST7789_MADCTL_MX | ST7789_MADCTL_MY | ST7789_MADCTL_RGB, 0, 80 },
                                                        { ST7789_MADCTL_MY | ST7789_MADCTL_MV | ST7789_MADCTL_RGB, 80, 0 },
                                                        { ST7789_MADCTL_RGB, 0, 0},
                                                        { ST7789_MADCTL_MX | ST7789_MADCTL_MV | ST7789_MADCTL_RGB, 0, 0 } };


////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////

extern SPI_HandleTypeDef ST7789_SPI_HANDLE;

#if defined(ST7789_USE_DMA)
	#define WAIT_DMA while (ST7789_SPI_HANDLE.hdmatx->State != HAL_DMA_STATE_READY) {} //{ asm volatile("nop"); }
#else
	#define WAIT_DMA {}
#endif

#if defined (ST7789_USE_BUFFERING)
	extern uint16_t * pixelBuffer;
	extern uint16_t pixelBufferSize;
#if defined (ST7789_USE_DOUBLE_BUFFERING)
	extern uint16_t pixelBufferHalfSize;
#endif
#endif

void ST7789_init();
void ST7789_initWithParams(uint8_t rotation, uint8_t littleEndian);

void ST7789_begin();
void ST7789_end();

#if defined (ST7789_USE_BUFFERING)
	uint16_t * ST7789_getPixelBuffer();
#endif

void ST7789_setRotation(uint8_t rotation);
void ST7789_setWindow(uint8_t x, uint8_t y, uint8_t width, uint8_t height);

void ST7789_fillColor(uint16_t color);
void ST7789_fillPixels(uint16_t color, uint16_t count);
void ST7789_writePixel(uint16_t color);
void ST7789_writePixels(uint16_t * pixels, uint16_t count, uint8_t littleEndian);

void ST7789_writeCommand(uint8_t command);
void ST7789_writeDataByte(uint8_t data);
void ST7789_writeDataWord(uint16_t data);
void ST7789_writeData(uint8_t * data, uint16_t size);



#endif // end _ST7789_

// END-OF-FILE
