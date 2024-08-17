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

// Instantiate class
ST7789_Class ST7789;

// Static variables references
long ST7789_Class::spiSpeed;
uint8_t ST7789_Class::spiMode;
bool ST7789_Class::isDisplayLE = DEFAULT_ENDIANNESS;
uint8_t ST7789_Class::xShift = 0;
uint8_t ST7789_Class::yShift = 0;

// Init w/o parameters
void ST7789_Class::init(void)
{
	init(DEFAULT_SPI_SPEED, DEFAULT_SPI_MODE, DEFAULT_ROTATION, DEFAULT_ENDIANNESS);
}

// Init with parameters
void ST7789_Class::init(uint32_t speed, uint8_t mode, uint8_t rotation, bool littleEndian) {
	// Configure pins
	pinMode(DISPLAY_RST, OUTPUT);
	pinMode(DISPLAY_DC, OUTPUT);
#if defined(DISPLAY_CS_USED)
	pinMode(DISPLAY_CS, OUTPUT);
#endif

	// Init SPI
	spiSpeed = speed;
	spiMode = mode;
	isDisplayLE = littleEndian;
	SPI.begin();

	// Reset display
	digitalWrite(DISPLAY_RST, LOW);
	delay(25);
	digitalWrite(DISPLAY_RST, HIGH);
	delay(50);

	begin();

	// Set color mode to RGB565
	writeCommand(ST7789_COLORMODE);
	writeDataByte(ST7789_COLOR_MODE_16bit);

	// Set display RAM control to Little Endian mode
	if (littleEndian) {
		writeCommand(ST7789_RAMCTRL);
		writeData((uint8_t *)ST7789_RAMCTRL_PARAMETERS, sizeof(ST7789_RAMCTRL_PARAMETERS));
	}

	// Sleep mode Off
	writeCommand(ST7789_SLPOUT);
	// Set display rotation
	setRotation(rotation);
	// Turn on the color inversion
	writeCommand(ST7789_INVON);
	// Normal Display Mode On
	writeCommand(ST7789_NORON);
	// Clear screen
	fillColor(0);
	// Display On
	writeCommand (ST7789_DISPON);

	end();
}

// Begin SPI transaction and select the display
void ST7789_Class::begin() {
	SPI.beginTransaction(SPISettings(spiSpeed, MSBFIRST, spiMode));
	selectDisplay(true);
}

// End SPI transaction and deselect the display
void ST7789_Class::end() {
	SPI.endTransaction();
	selectDisplay(false);
}

// Configure screen rotation
void ST7789_Class::setRotation(uint8_t rotation) {
	writeCommand(ST7789_MADCTL);
	writeDataByte(ST7789_ROTATION_PARAMETERS[rotation][0]);
	xShift = ST7789_ROTATION_PARAMETERS[rotation][1];
	yShift = ST7789_ROTATION_PARAMETERS[rotation][2];
}

// Set target window
void ST7789_Class::setWindow(uint8_t x, uint8_t y, uint8_t width, uint8_t height) {
	uint16_t address;
	// Column Address set
	writeCommand(ST7789_CASET);
	// Write X coordinates
	selectDataMode(true);
	address = x + xShift;
	SPI.transfer16(address);
	address += (width -1);
	SPI.transfer16(address);

	// Row Address set
	writeCommand(ST7789_RASET);
	// Write Y coordinates
	selectDataMode(true);
	address = y + yShift;
	SPI.transfer16(address);
	address += (height - 1);
	SPI.transfer16(address);

	// Write to RAM
	writeCommand(ST7789_RAMWR);
	selectDataMode(true);
}

// Fill with solid color
void ST7789_Class::fillColor(uint16_t color) {
	setWindow(0, 0, DISPLAY_WIDTH, DISPLAY_HEIGHT);
	for (uint16_t pixel = 0; pixel < DISPLAY_WIDTH * DISPLAY_HEIGHT; pixel++) {
		writePixel(color);
	}
}

// Write next pixel
void ST7789_Class::writePixel(uint16_t color) {
  if (isDisplayLE) color = __builtin_bswap16(color);
	SPI.transfer16(color);
}

// Write a chunk of pixels
void ST7789_Class::writePixels(uint16_t * pixels, uint16_t size, bool littleEndian) {
	if (isDisplayLE == littleEndian) {
		writeData((uint8_t *)pixels, size << 1);
	}
	else
	{
		for (uint16_t i = 0; i < size; i++) SPI.transfer16(pixels[i]);
	}
}

// Write command
void ST7789_Class::writeCommand(uint8_t command) {
	selectDataMode(false);
	SPI.transfer(command);
}

// Write one byte of data
void ST7789_Class::writeDataByte(uint8_t data) {
	selectDataMode(true);
	SPI.transfer(data);
}

// Write one word of data
// (for pixel use writePixel).
void ST7789_Class::writeDataWord(uint16_t data) {
	selectDataMode(true);
	SPI.transfer16(data);
}

// Write a chunk of data
// (for pixels use writePixels).
void ST7789_Class::writeData(uint8_t * data, uint16_t size) {
	selectDataMode(true);
#if defined(ESP32) || defined(ESP8266)
	SPI.writeBytes(data, size);
#else
	SPI.transfer(data, size);
#endif
}

// END-OF-FILE
