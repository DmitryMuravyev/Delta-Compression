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

// This example demonstrates the use of a memory area beyond 64K 
// on the AVR platform. It's a little bit tricky and works slower 
// than the "near" memory, but it works nonetheless. 
// This example also uses the most complete decompression class, 
// whose performance can (and should) be significantly increased by 
// further modifications, simplification, and reduction of variables bit length.
//
// ATTENTION!!! To run this example, you need an Arduino Mega2560 board, 
// an ILI9486 320x480 TFT color display shield, 
// and the UTFT library installed into the Arduino IDE.

#include <UTFT.h>
#include "Slides.h"

#define DISPLAY_WIDTH       320
#define DISPLAY_HEIGHT      480
#define FRAMES_PER_BUFFER   SLIDE_IMAGE_WIDTH * SLIDE_SQUARE_SIDE
#define BUFFERS_PER_SLIDE   SLIDE_IMAGE_HEIGHT / SLIDE_SQUARE_SIDE

// Display
UTFT myGLCD(CTE40, 38, 39, 40, 41);

// Decompression instance
static Decompression decomp;
// And buffer
static uint16_t decompressedData[FRAMES_PER_BUFFER];
// Far data sddresses
static uint_farptr_t slide1DataChunks[6];
static uint_farptr_t slide2DataChunks[7];
// Current slide
static bool currentSlide = true;


void setup() {
  // put your setup code here, to run once:
  myGLCD.InitLCD(PORTRAIT);
  // Some strange commands to mirror displayed characters...
  // ...we don't need them for the moment.
  //myGLCD.LCD_Write_COM(0x36);
  //myGLCD.LCD_Write_DATA(0x4A);
  decomp.bufferWidth = SLIDE_IMAGE_WIDTH; // Set the width of our buffer for correct decompression

  // We have to save the far addresses of the data chunks 
  // in order to use them during decompression. 
  // This can only be done at runtime.
  slide1DataChunks[0] = pgm_get_far_address(slide1Data1);
  slide1DataChunks[1] = pgm_get_far_address(slide1Data2);
  slide1DataChunks[2] = pgm_get_far_address(slide1Data3);
  slide1DataChunks[3] = pgm_get_far_address(slide1Data4);
  slide1DataChunks[4] = pgm_get_far_address(slide1Data5);
  slide1DataChunks[5] = pgm_get_far_address(slide1Data6);
  // For both slides
  slide2DataChunks[0] = pgm_get_far_address(slide2Data1);
  slide2DataChunks[1] = pgm_get_far_address(slide2Data2);
  slide2DataChunks[2] = pgm_get_far_address(slide2Data3);
  slide2DataChunks[3] = pgm_get_far_address(slide2Data4);
  slide2DataChunks[4] = pgm_get_far_address(slide2Data5);
  slide2DataChunks[5] = pgm_get_far_address(slide2Data6);
  slide2DataChunks[6] = pgm_get_far_address(slide2Data7);
}


void loop() {
  // put your main code here, to run repeatedly:

  decomp.resetDecompression();

	cbi(myGLCD.P_CS, myGLCD.B_CS);
  myGLCD.setXY(0, 0, DISPLAY_WIDTH, DISPLAY_HEIGHT);
	sbi(myGLCD.P_RS, myGLCD.B_RS);

  // Draw single slide
  for (uint8_t i = 0; i < BUFFERS_PER_SLIDE; i++)
  {
    // Decompress one buffer
    decomp.decompressNextFrames(&photoDC, currentSlide ? slide1DataChunks : slide2DataChunks, (uint8_t *)decompressedData, FRAMES_PER_BUFFER);
    // And display it
    for (uint16_t pixel = 0; pixel < FRAMES_PER_BUFFER; pixel++) {
      myGLCD.setPixel(__builtin_bswap16(decompressedData[pixel]));
    }
  }
  sbi(myGLCD.P_CS, myGLCD.B_CS);

  // Swap slide
  currentSlide = !currentSlide;
  // Wait
  delay(10000);
}

// END-OF-FILE
