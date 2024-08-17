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

// Very simple class which allows you to output images
// or individual pixels on a display based on the ST7789 controller.
// It is designed only for use with 240x240 pixel displays.
// Some extra modifications required for other displays.

// ATTENTION! For better perfomance it is necessary that the 
// display byte order corresponds to the byte order of the transmitted pixels.

// Includes
#include <Arduino.h>
#include <SPI.h>

#ifndef _ST7789_
#define _ST7789_

// Global definitions
#define BIG_ENDIAN					0
#define LITTLE_ENDIAN				1

// Default SPI clock speed
#define DEFAULT_SPI_SPEED			8000000L
#define DEFAULT_SPI_MODE			SPI_MODE3

// Display configuration
#define DEFAULT_ROTATION			2		// 0-3, 2 - normal top to bottom
#define DEFAULT_ENDIANNESS			BIG_ENDIAN
#define DISPLAY_RST					8		// Reset pin
#define DISPLAY_DC					9		// Data/command pin
#define DISPLAY_CS					5		// If your display has CS pin
//#define DISPLAY_CS_USED
// SCK  13
// SDA  11

#define DISPLAY_WIDTH				240
#define DISPLAY_HEIGHT				240

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

class ST7789_Class {

public:

	static void init();
	static void init(uint32_t speed, uint8_t mode, uint8_t rotation, bool littleEndian);

	static void begin();
	static void end();

	static void setRotation(uint8_t rotation);
	static void setWindow(uint8_t x, uint8_t y, uint8_t width, uint8_t height);

	static void fillColor(uint16_t color);
	static void writePixel(uint16_t color);
	static void writePixels(uint16_t * pixels, uint16_t size, bool littleEndian);

	static void writeCommand(uint8_t command);
	static void writeDataByte(uint8_t data);
	static void writeDataWord(uint16_t data);
	static void writeData(uint8_t * data, uint16_t size);

private:

	static long spiSpeed;
	static uint8_t spiMode;
	static bool isDisplayLE; // 0 - big endian, 1 - little endian
	static uint8_t xShift;
	static uint8_t yShift;

	inline static void selectDisplay(bool val) {
#if defined(DISPLAY_CS_USED)
		digitalWrite(DISPLAY_CS, !val);
#endif
	}

	inline static void selectDataMode(bool val) {
		digitalWrite(DISPLAY_DC, val);
	}

};

extern ST7789_Class ST7789;

#endif // end _ST7789_

// END-OF-FILE
