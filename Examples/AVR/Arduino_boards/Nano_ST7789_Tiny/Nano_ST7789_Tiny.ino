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

// The example is based on a tiny version of the decompression class, 
// which has rather modest but sufficient capabilities and very high performance.
// In addition, it uses a minimalistic library which can draw images 
// or individual pixels on a display based on the ST7789 controller. 
// You don't need any extra libraries for this example to work.

// ATTENTION!!! To run this example, you need an Arduino Nano board, 
// and a 240x240 pixel IPS color display (on the ST7789 controller).
// To configure the connection pins, please refer to ST7789.h.

#include "ST7789.h"
#include "Icons.h"

#define FRAMES_PER_BUFFER   ICONS_IMAGE_WIDTH * ICONS_SQUARE_SIDE
#define BUFFERS_PER_ICON    ICONS_IMAGE_HEIGHT / ICONS_SQUARE_SIDE

// Icons list
static const uint8_t * icons[25] = { accessData,
                                     bluetoothData,
                                     bugData,
                                     buildData,
                                     cableData,
                                     checkData,
                                     cloudData,
                                     editData,
                                     emojiData,
                                     errorData,
                                     handData,
                                     lightbulbData,
                                     memoryData,
                                     notificationsData,
                                     personData,
                                     phoneData,
                                     powerData,
                                     sdData,
                                     smsData,
                                     starData,
                                     syncData,
                                     thermostatData,
                                     thumbData,
                                     warningData,
                                     wifiData };

// Color gradients for smoothing edges
static const uint16_t colors[7][3] = { { 0x5000, 0xA800, 0xF800 },    // Red
                                       { 0x02A0, 0x0540, 0x07E0 },    // Green
                                       { 0x000A, 0x0015, 0x001F },    // Blue
                                       { 0x52A0, 0xAD40, 0xFFE0 },    // Yellow
                                       { 0x02AA, 0x0555, 0x7FFF },    // Cyan
                                       { 0x500A, 0xA815, 0xF81F },    // Magenta
                                       { 0x52AA, 0xAD55, 0xFFFF } };  // White


uint8_t currentColor;
uint8_t x = 0;
uint8_t y = 0;

// Decompression instance
static Decompression decomp;
// And buffer
static uint16_t decompressedData[FRAMES_PER_BUFFER];


void setup() {
  // put your setup code here, to run once:
  // Initialize display
  ST7789.init(DEFAULT_SPI_SPEED, DEFAULT_SPI_MODE, DEFAULT_ROTATION, LITTLE_ENDIAN);
  // If the display does not initialize, then try this:
  //ST7789.init(DEFAULT_SPI_SPEED, SPI_MODE0, DEFAULT_ROTATION, LITTLE_ENDIAN);
  // Set the width of our buffer for correct decompression
  decomp.bufferWidth = ICONS_IMAGE_WIDTH;
}

void loop() {
  // put your main code here, to run repeatedly:

  // Display 30 random icons
  for (uint8_t i = 0; i < 30; i++)
  {
    ST7789.begin();
    ST7789.setWindow(x, y, ICONS_IMAGE_WIDTH, ICONS_IMAGE_HEIGHT);
    decomp.resetDecompression();
    uint8_t * currentIcon = icons[random(25)];
    currentColor = random(7);

    // Draw single icon
    for (uint8_t j = 0; j < BUFFERS_PER_ICON; j++)
    {
      // Decompress one buffer
      decomp.decompressNextFrames(&iconsDC, currentIcon, decompressedData, FRAMES_PER_BUFFER, true);
      // And display it
      ST7789.writePixels(decompressedData, FRAMES_PER_BUFFER, LITTLE_ENDIAN);
    }
    ST7789.end();
    delay(10);
  }

  // Finally move to the next icon position
  x += ICONS_IMAGE_WIDTH;
  if (x >= DISPLAY_WIDTH) {
    x = 0;
    y += ICONS_IMAGE_HEIGHT;
    if (y >= DISPLAY_HEIGHT) y = 0;
  }
}

// Delegate method for coloring a pixel in the current color
uint16_t colorizePixel(uint8_t frame) {
  uint16_t pixelColor = (frame == 0) ? 0 : colors[currentColor][frame - 1];
  return (pixelColor);
}

// END-OF-FILE
