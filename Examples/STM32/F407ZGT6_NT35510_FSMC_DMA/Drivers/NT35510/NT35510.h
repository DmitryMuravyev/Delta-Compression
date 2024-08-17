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
// or individual pixels on a display based on the NT35510 controller.
// It is designed only for use with 800x480 pixel displays
// and microcontrollers with 16 bit FSMC support.
// Also it supports DMA and double buffering.

// ATTENTION!!! Here we assume that the display always works
// in Big Endian mode. And since FSMC converts the internal data
// format from Little Endian to Big Endian, this means that we must
// output all data in the native format of the architecture, i.e.
// Little Endian. I can make a more universal driver (as in the
// example for ST7789), but I need a complete NT35510 datasheet
// for that, while I only have a preliminary version 0.8 available.

// The driver is configured to work with 16-bit FSMC NOR SRAM Bank 4
// and the A6 address line as an RS signal.

// For STM32F407ZGT6 microcontroller, the display pins are connected as follows:
//
//     LCD Pin        STM32 Port        STM32 Pin
//      VDD				DC3.3V				?
//      GND				GND					?
//
//		DB0				PD14				85
//		DB1				PD15				86
//		DB2				PD0					114
//		DB3				PD1					115
//		DB4				PE7					58
//		DB5				PE8					59
//		DB6				PE9					60
//		DB7				PE10				63
//		DB8				PE11				64
//		DB9				PE12				65
//		DB10			PE13				66
//		DB11			PE14				67
//		DB12			PE15				68
//		DB13			PD8					77
//		DB14			PD9					78
//		DB15			PD10				79
//       WR				PD5					119
//       RD				PD4					118
//       RS				PF12				50
//       RST			NRST				25		// You can use any other pin for the soft reset
//       CS				PG12				127


// Includes
#include <stdint.h>
#include "stm32f4xx_hal.h"


#ifndef _NT35510_
#define _NT35510_

// Display configuration
#define NT35510_DEFAULT_ROTATION	3   // 0-3, 2 - normal top to bottom

// At the moment only one screen resolution supported
#define NT35510_DISPLAY_WIDTH		800
#define NT35510_DISPLAY_HEIGHT		480

// The name of external SRAM_HandleTypeDef variable (defined in "main.c")
#define NT35510_SRAM_HANDLE			hsram1

// SRAM Data bank addresses (base + A6/7th bit)
#define NT35510_REG_ADDR			0x6C000000u
#define NT35510_DATA_ADDR			0x6C000080u

// If you plan to use DMA, declare this symbol
#define NT35510_USE_DMA

// You can shift the responsibility for the buffer to this module.
// In addition, even without DMA, using a buffer speeds up
// the painting of rectangular areas in one color.
#define NT35510_USE_BUFFERING

// Double buffering can be used together with DMA.
// One of the buffers is transferring to the display,
// while the second one is rendering. Then they are swapped.
#define NT35510_USE_DOUBLE_BUFFERING

// You can free the buffer during the "end()" call to avoid
// wasting RAM when no display operations are performed.
//#define NT35510_RELEASE_BUFFER_AT_END

// The buffer size in pixels will be defined as
// the display width multiplied by this number.
// Also you can change the buffer size as you want,
// changing NT35510_pixelBufferSize / NT35510_pixelBufferHalfSize
// variables outside of the begin() / end() calls.
#define NT35510_BUFFER_HEIGHT		2

// Ports/pins (you can include the "main.h" file and use the symbols defined in it.
// Uncomment and configure port/pin if Reset pin used
//#define NT35510_RST_USED
#if defined(NT35510_RST_USED)
#define NT35510_RST_PORT			GPIOA
#define NT35510_RST_PIN				GPIO_PIN_4
#endif


// Display commands
#define NT35510_COLORMODE			0x3A00
#define NT35510_MADCTL				0x3600
#define NT35510_SLPOUT				0x1100
#define NT35510_NORON				0x1300
#define NT35510_DISPON				0x2900
#define NT35510_CASET				0x2A00
#define NT35510_RASET				0x2B00
#define NT35510_RAMWR				0x2C00

// Command arguments
// Page Address Order: 0 = Top to Bottom, 1 = Bottom to Top.
#define NT35510_MADCTL_MY			0x80
// Column Address Order: 0 = Left to Right, 1 = Right to Left.
#define NT35510_MADCTL_MX			0x40
// Page/Column Order: 0 = Normal Mode, 1 = Reverse Mode.
#define NT35510_MADCTL_MV			0x20
// Line Address Order: 0 = LCD Refresh Top to Bottom, 1 = LCD Refresh Bottom to Top.
#define NT35510_MADCTL_ML			0x10
// RGB/BGR Order: 0 = RGB, 1 = BGR.
#define NT35510_MADCTL_RGB			0x00
// Display Data Latch Data Order: 0 = LCD Refresh Left to Right, 1 = LCD Refresh Right to Left.
#define NT35510_MADCTL_MH			0x00

static const uint8_t NT35510_COLOR_MODE_16bit = 0x55;
static const uint16_t NT35510_ROTATION_PARAMETERS[4][3] = { { NT35510_MADCTL_MX | NT35510_MADCTL_MY | NT35510_MADCTL_RGB, NT35510_DISPLAY_HEIGHT, NT35510_DISPLAY_WIDTH },
                                                            { NT35510_MADCTL_MY | NT35510_MADCTL_MV | NT35510_MADCTL_RGB, NT35510_DISPLAY_WIDTH, NT35510_DISPLAY_HEIGHT },
                                                            { NT35510_MADCTL_RGB, NT35510_DISPLAY_HEIGHT, NT35510_DISPLAY_WIDTH },
                                                            { NT35510_MADCTL_MX | NT35510_MADCTL_MV | NT35510_MADCTL_RGB, NT35510_DISPLAY_WIDTH, NT35510_DISPLAY_HEIGHT } };


////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////

extern SRAM_HandleTypeDef NT35510_SRAM_HANDLE;

#if defined(NT35510_USE_DMA)
	#define WAIT_DMA while (NT35510_SRAM_HANDLE.hdma->State != HAL_DMA_STATE_READY) {} //{ asm volatile("nop"); }
#else
	#define WAIT_DMA {}
#endif

#if defined (NT35510_USE_BUFFERING)
	extern uint16_t * pixelBuffer;
	extern uint16_t pixelBufferSize;
#if defined (NT35510_USE_DOUBLE_BUFFERING)
	extern uint16_t pixelBufferHalfSize;
#endif
#endif

void NT35510_init();
void NT35510_initWithParams(uint8_t rotation);

void NT35510_begin();
void NT35510_end();

#if defined (NT35510_USE_BUFFERING)
	uint16_t * NT35510_getPixelBuffer();
#endif

void NT35510_setRotation(uint8_t rotation);
void NT35510_setWindow(uint16_t x, uint16_t y, uint16_t width, uint16_t height);

void NT35510_fillColor(uint16_t color);
void NT35510_fillPixels(uint16_t color, uint32_t count);
void NT35510_writePixel(uint16_t color);
void NT35510_writePixels(uint16_t * pixels, uint16_t count);

void NT35510_writeCommand(uint16_t command);
void NT35510_writeDataByte(uint8_t data);
void NT35510_writeDataWord(uint16_t data);
void NT35510_writeDataBytes(uint8_t * data, uint16_t size);
void NT35510_writeDataWords(uint16_t * data, uint16_t size);


#endif // end _NT35510_

// END-OF-FILE
