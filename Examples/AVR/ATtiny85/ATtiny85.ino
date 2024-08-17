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

// The example outputs an ASCII ART image over the SPI bus.
// It is based on a tiny version of the decompression class, which has 
// rather modest but sufficient capabilities and very high performance.

// To run this example you have to: 
// install the ATTinyCore boards library,
// select the ATtiny25/45/86 (No bootloader) board
// and enable the LTO option.
#include "ASCII.h"
#include <SPI.h>

static Decompression decomp;

void setup() {
  // put your setup code here, to run once:
  SPI.begin();
}

void loop() {
  // put your main code here, to run repeatedly:
  // Decompress all data at once
  decomp.resetDecompression();
  SPI.beginTransaction(SPISettings());
  decomp.decompressNextFrames(&asciiDC, asciiData, NULL, ASCII_NUMBER_OF_FRAMES, false);
  SPI.endTransaction();
  delay(10000);
}

// Frame decompressed delegate (can be used if the image is not divided into squares and the buffer is not used)
void frameDecompressed(uint8_t frame) {
  SPI.transfer(frame);
}

// END-OF-FILE
