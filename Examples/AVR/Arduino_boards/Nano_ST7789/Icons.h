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


// Combined data for 25 Google icons

// Includes
#include "Decompression.h"

// ICONS definitions
#define ICONS_NUMBER_OF_FRAMES		2304
#define ICONS_NUMBER_OF_CHANNELS		1
#define ICONS_BITS_PER_CHANNELS		{ 2 }
#define ICONS_BLOCK_SIZE_BITS		5
#define ICONS_BITS_PER_METHOD_DECLARATION		{ 2 }
#define ICONS_BITS_PER_FRAME		2
#define ICONS_DELTA_OPTION		0	// FixedWindowOnly
#define ICONS_IMAGE_WIDTH		48
#define ICONS_IMAGE_HEIGHT		48
#define ICONS_SPLIT_TO_SQUARES		0
#define ICONS_SQUARE_SIDE		1
#define ICONS_SQUARES_PER_WIDTH		ICONS_IMAGE_WIDTH / ICONS_SQUARE_SIDE
#define ICONS_SQUARES_PER_WIDTH_SIZE		ICONS_IMAGE_WIDTH * ICONS_SQUARE_SIDE

// A set of constants defining the initial conditions for decompression
static const DecompressionContext iconsDC = {
		ICONS_NUMBER_OF_CHANNELS,
		ICONS_BITS_PER_CHANNELS,
		ICONS_BLOCK_SIZE_BITS,
		ICONS_BITS_PER_METHOD_DECLARATION,
		ICONS_BITS_PER_FRAME,
#if defined(DECOMPRESSION_USE_SQUARES)
		ICONS_SPLIT_TO_SQUARES,
		ICONS_IMAGE_WIDTH,
		ICONS_SQUARE_SIDE,
		ICONS_SQUARES_PER_WIDTH,
		ICONS_SQUARES_PER_WIDTH_SIZE
#endif
};

// End of ICONS definitions

static const uint8_t accessData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x20, 0xB9, 0x6B, 0xFE, 0x97, 0xE0, 0x28, 0x26, 0x98, 0x0B, 0xA1, 0x36, 0x7F, 0xFF, 0xEC, 0x80, 0x67, 0xA1, 0x80, 0x6C, 0x01, 0x66, 0xC6, 0x1A, 0x26, 0x00, 0x2C, 0x98, 0x68, 0x88, 0x00, 0xB6, 0x61, 0xA1, 0xE0, 0x02, 0xE9, 0x86, 0x86, 0x80, 0x07, 0xE6, 0x14, 0x98, 0x1F, 0x30, 0x9B, 0x00, 0x04, 0xE3, 0x18, 0x38, 0xC0, 0x4B, 0x07, 0x98, 0xC1, 0xE6, 0xA0, 0x0B, 0x2E, 0x31, 0x83, 0xCC, 0x34, 0x20, 0x20, 0x63, 0x08, 0x1A, 0x00, 0x03, 0x03, 0x18, 0x40, 0xD2, 0x90, 0x0C, 0x0C, 0x61, 0x03, 0x42, 0x40, 0x00, 0x50, 0x31, 0x84, 0x0D, 0x34, 0x00, 0x01, 0x40, 0xC6, 
	0x10, 0x30, 0xD0, 0x60, 0x89, 0x8C, 0x22, 0x67, 0x08, 0x98, 0xC0, 0x05, 0x03, 0x38, 0x44, 0xC8, 0x01, 0x65, 0xC6, 0x70, 0x89, 0x9A, 0x80, 0x09, 0xC6, 0x70, 0x01, 0x44, 0xCD, 0x40, 0x04, 0xC3, 0x4D, 0x00, 0x00, 0x52, 0x33, 0x50, 0x01, 0x2C, 0xD3, 0x40, 0x00, 0x0C, 0xCC, 0xD4, 0x00, 0x4A, 0x34, 0xA4, 0x03, 0x43, 0x35, 0x00, 0x12, 0x4C, 0x02, 0x80, 0xA9, 0x9A, 0x80, 0x09, 0x06, 0x90, 0x01, 0x54, 0xC9, 0x40, 0x28, 0x30, 0x12, 0x82, 0xC6, 0x2A, 0x14, 0x9A, 0xC0, 0x05, 0x63, 0x5B, 0x3F, 0xF3, 0x03, 0xE6, 0x13, 0x68, 0x00, 0x7E, 0x61, 0x49, 0xA0, 0x16, 0x78, 0x61, 0xA1, 0xE0, 0x02, 0xD9, 0x86, 0x88, 
	0x80, 0x0B, 0x26, 0x1A, 0x26, 0x00, 0x2B, 0x98, 0x68, 0xA8, 0x06, 0x7A, 0x18, 0x07, 0x21, 0x36, 0x7F, 0xFF, 0xEE, 0x83, 0xD9, 0xFF, 0xFB, 0xE0, 0xD8, 0x16, 0xBF, 0xE9, 0x7E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0x88, 0x00};

static const uint8_t bluetoothData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x20, 0x2B, 0xDF, 0x83, 0x00, 0x46, 0x01, 0xF8, 0x2C, 0x06, 0x60, 0x1F, 0x82, 0x80, 0x86, 0x14, 0xBE, 0x08, 0x02, 0x98, 0x52, 0xF8, 0x1C, 0x0C, 0x61, 0x4B, 0x81, 0x2A, 0x40, 0x00, 0x3F, 0xFF, 0xDD, 0x80, 0xEB, 0xD3, 0x02, 0x0C, 0x03, 0x90, 0xB5, 0x7F, 0xA0, 0x01, 0xFE, 0xFF, 0xEE, 0x02, 0xF3, 0xFF, 0xA0, 0x07, 0xF8, 0xFF, 0xEE, 0x03, 0xF3, 0xFF, 0xA0, 0x1F, 0xE0, 0xFF, 0xE8, 0x00, 0x18, 0x68, 0x70, 0x7E, 0x7F, 0xF4, 0x0F, 0xF0, 0x1F, 0xFC, 0x00, 0x3F, 0x80, 0x7E, 0x7F, 0xF4, 0x3F, 0xC1, 0xFF, 0xD0, 0x01, 0xFE, 0x80, 0x7A, 0x7F, 0xF4, 0xFF, 0x1F, 0xFD, 
	0x00, 0x02, 0xFE, 0x21, 0xE9, 0xFF, 0xDF, 0xF7, 0xFF, 0x40, 0x00, 0x1F, 0xD8, 0x80, 0x67, 0x61, 0xA9, 0x20, 0x00, 0x8C, 0x05, 0x20, 0x0C, 0xEA, 0x37, 0xC8, 0x01, 0xA0, 0x1F, 0xD3, 0x00, 0xCE, 0x83, 0x84, 0x80, 0x1F, 0x00, 0x5F, 0xD0, 0x35, 0x3F, 0xFF, 0xE8, 0x01, 0xFE, 0x01, 0x7F, 0x50, 0xCC, 0xFF, 0xFA, 0x00, 0x7F, 0xE0, 0x17, 0xF5, 0x0C, 0xCF, 0xFF, 0xA0, 0x07, 0xFE, 0x01, 0x7F, 0x40, 0xD4, 0xFF, 0xFF, 0xA0, 0x07, 0xF8, 0x05, 0xFC, 0xC0, 0x33, 0xA0, 0xE1, 0x20, 0x07, 0xC0, 0x17, 0xD2, 0x00, 0xCE, 0xA3, 0x7C, 0x80, 0x1A, 0x01, 0xFD, 0x10, 0x0C, 0xEC, 0x35, 0x24, 0x00, 0x11, 0x80, 0x60, 0x1D, 
	0x9F, 0xFD, 0xFF, 0x7F, 0xF4, 0x00, 0x01, 0xFE, 0x01, 0xE9, 0xFF, 0xD3, 0xFC, 0x7F, 0xF4, 0x00, 0x0B, 0xF7, 0x87, 0xA7, 0xFF, 0x43, 0xFC, 0x1F, 0xFD, 0x00, 0x1F, 0xE0, 0x90, 0x00, 0x12, 0x1E, 0x64, 0x0F, 0xF0, 0x1F, 0xFC, 0x00, 0x3F, 0x70, 0x7E, 0x7F, 0xF4, 0x03, 0xFC, 0x1F, 0xFD, 0x00, 0x03, 0x7A, 0x40, 0x00, 0x90, 0xE3, 0x20, 0x07, 0xF8, 0xFF, 0xF5, 0xC2, 0xD5, 0xFE, 0x80, 0x07, 0xFB, 0xFF, 0xD9, 0x01, 0xD7, 0xA6, 0x04, 0x18, 0x0B, 0x61, 0x2A, 0x40, 0x00, 0x3F, 0xFF, 0xEF, 0x81, 0x80, 0xC6, 0x1A, 0x3E, 0x06, 0x02, 0x98, 0x68, 0xF8, 0x1C, 0x08, 0x61, 0xA3, 0xE0, 0x80, 0x19, 0x80, 0xBE, 0x0A, 
	0x01, 0x18, 0x0B, 0xE0, 0xB0, 0x13, 0x6F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x34, 0x00};

static const uint8_t bugData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x3C, 0x02, 0xB2, 0xC0, 0x2A, 0xF8, 0x07, 0x5E, 0xA4, 0x07, 0x3F, 0x70, 0x27, 0x5F, 0xE8, 0x00, 0x03, 0xFF, 0x6C, 0x27, 0x3F, 0xFA, 0x00, 0x0F, 0xFE, 0xF0, 0x23, 0x3F, 0xFD, 0xFF, 0x7F, 0xEF, 0x80, 0x33, 0xB0, 0xC0, 0x3F, 0x00, 0xCE, 0xC3, 0x00, 0xF8, 0x03, 0x3B, 0x8C, 0x03, 0xC0, 0x0C, 0xF0, 0x30, 0x0E, 0xC2, 0x67, 0x50, 0xF9, 0xBC, 0x3E, 0x6F, 0x0F, 0x9B, 0xC3, 0xE7, 0x30, 0x39, 0x9C, 0x0E, 0x77, 0x03, 0x99, 0xC0, 0xE7, 0x70, 0x39, 0x9C, 0x0E, 0x77, 0x03, 0x99, 0xC0, 0xE7, 0x30, 0xF9, 0xBC, 0x3E, 0x6F, 0x0F, 0x9B, 0xC3, 0xE7, 0x30, 
	0x39, 0x9C, 0x0E, 0x77, 0x03, 0x99, 0xC0, 0xE7, 0x70, 0x39, 0x9C, 0x0E, 0x77, 0x03, 0x99, 0xC0, 0xE7, 0x30, 0xF9, 0xBC, 0x3E, 0x6F, 0x0F, 0x9B, 0xC3, 0xE7, 0x50, 0x99, 0xEC, 0x03, 0x3C, 0x0C, 0x03, 0xC0, 0x0C, 0xEE, 0x30, 0x0F, 0x80, 0x33, 0xB0, 0xC0, 0x3F, 0x01, 0x41, 0x24, 0xC0, 0x5F, 0x02, 0x81, 0xE6, 0xFF, 0x9F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x84, 0xC0};

static const uint8_t buildData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x0A, 0x03, 0x2B, 0xF0, 0xA5, 0xF0, 0x20, 0x06, 0x74, 0x18, 0x0B, 0xE0, 0x40, 0x0C, 0xE9, 0x30, 0x0F, 0xC0, 0x60, 0xC6, 0x7F, 0xF7, 0xC0, 0x60, 0xC6, 0x7F, 0xF7, 0xC0, 0x60, 0x16, 0x52, 0x60, 0x1F, 0x80, 0xC0, 0x2C, 0xA4, 0xF0, 0x01, 0x49, 0x20, 0xB6, 0x7F, 0xEB, 0x00, 0xA7, 0x54, 0x00, 0x24, 0x9D, 0x80, 0x75, 0xEA, 0x81, 0x2C, 0xFF, 0x80, 0x6A, 0x03, 0x35, 0xA4, 0x00, 0xA0, 0xC0, 0x55, 0x02, 0x1A, 0x52, 0x01, 0x51, 0xD4, 0x0A, 0x67, 0x48, 0x15, 0x9D, 0x40, 0xC6, 0x54, 0x85, 0x87, 0x50, 0x39, 0x8D, 0x25, 0xA7, 0x50, 0x43, 0x3F, 0xC2, 0x2D, 0x30, 0x15, 0x40, 0x2C, 0xD8, 0xC3, 
	0x45, 0x00, 0x03, 0x73, 0x0A, 0x55, 0x00, 0xB3, 0x53, 0x0D, 0x15, 0x00, 0xCF, 0x63, 0x0D, 0x15, 0x00, 0xB3, 0x63, 0x0D, 0x15, 0x00, 0xB3, 0x63, 0x0D, 0x15, 0x00, 0xB3, 0x63, 0x0D, 0x15, 0x00, 0xB3, 0x63, 0x0D, 0x15, 0x00, 0xAB, 0x63, 0x0D, 0x16, 0x04, 0xCD, 0x7F, 0xD2, 0xD3, 0x01, 0x7C, 0x02, 0x8B, 0x4C, 0x05, 0xF0, 0x0A, 0x2D, 0x30, 0x17, 0xC0, 0x28, 0xB4, 0xC0, 0x5F, 0x00, 0xA2, 0xD3, 0x01, 0x7C, 0x02, 0x8B, 0x4C, 0x05, 0xF0, 0x0A, 0x2D, 0x30, 0x17, 0xC0, 0x28, 0xB4, 0xC0, 0x5F, 0x00, 0xA2, 0xD3, 0x01, 0x7C, 0x02, 0x8B, 0x4C, 0x05, 0xF0, 0x0A, 0x2D, 0x30, 0x17, 0xC0, 0x28, 0xB4, 0xC0, 0x5F, 
	0x00, 0xA2, 0xD3, 0xF8, 0x09, 0x0A, 0xCC, 0x03, 0xF0, 0x10, 0x00, 0x52, 0x60, 0x1F, 0x81, 0x01, 0x33, 0xFF, 0xFF, 0x7C, 0x0C, 0x07, 0x9F, 0xFF, 0xBE, 0x08, 0x02, 0xCF, 0xFD, 0xF0, 0x50, 0x0E, 0x6D, 0xF8, 0x7C, 0x3E, 0x06, 0x00};

static const uint8_t cableData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x34, 0x0F, 0x37, 0xFC, 0xA4, 0x0F, 0x3F, 0xFE, 0xD0, 0x12, 0xCF, 0xF2, 0x00, 0xE7, 0x30, 0x5B, 0x3F, 0xF1, 0xC0, 0xE7, 0x20, 0x6B, 0x3F, 0xFC, 0x60, 0x39, 0xC4, 0x27, 0x3F, 0xFA, 0x0F, 0xFE, 0x80, 0x2C, 0xDE, 0x13, 0xAF, 0xF0, 0x00, 0xFF, 0x80, 0x16, 0x6F, 0x01, 0x99, 0xD2, 0x04, 0x66, 0x30, 0x59, 0xBC, 0x06, 0x67, 0x01, 0x98, 0xC1, 0x66, 0xF0, 0x19, 0x9C, 0x06, 0x63, 0x05, 0x9B, 0xC0, 0x66, 0x70, 0x19, 0x8C, 0x16, 0x6F, 0x01, 0x99, 0xC0, 0x66, 0x30, 0x59, 0xBC, 0x06, 0x67, 0x01, 0x98, 0xC1, 0x66, 0xF0, 0x19, 0x9C, 0x06, 0x63, 0x05, 
	0x9B, 0xC0, 0x66, 0x70, 0x19, 0x8C, 0x00, 0x29, 0x30, 0xA4, 0xE0, 0x19, 0x9C, 0x06, 0x67, 0x01, 0x9C, 0xC0, 0x66, 0x70, 0x19, 0x9C, 0x06, 0x73, 0x01, 0x99, 0xC0, 0x66, 0x70, 0x19, 0xCC, 0x06, 0x67, 0x01, 0x99, 0xC0, 0x67, 0x30, 0x19, 0x9C, 0x06, 0x67, 0x01, 0x9C, 0xC0, 0x66, 0x70, 0x19, 0x9C, 0x06, 0x73, 0x01, 0x99, 0xC0, 0x66, 0x70, 0x19, 0xCC, 0x06, 0x67, 0x01, 0x99, 0xC0, 0x66, 0xF0, 0x0C, 0xE8, 0x3A, 0x48, 0x07, 0xF8, 0x00, 0x07, 0xFB, 0xC1, 0x66, 0x30, 0x19, 0x9C, 0x06, 0x6F, 0x05, 0x98, 0xC0, 0x66, 0x70, 0x19, 0xBC, 0x16, 0x63, 0x01, 0x99, 0xC0, 0x66, 0xF0, 0x59, 0x8C, 0x06, 0x67, 0x01, 
	0x9B, 0xC1, 0x66, 0x30, 0x19, 0x9C, 0x06, 0x6F, 0x05, 0x98, 0xC0, 0x66, 0x70, 0x19, 0xBC, 0x16, 0x63, 0x01, 0x99, 0xD2, 0x04, 0x66, 0xF0, 0x59, 0x8C, 0x1F, 0x5F, 0xE0, 0x01, 0xFF, 0x3C, 0x16, 0x63, 0x07, 0xCF, 0xFE, 0x83, 0xFF, 0xB1, 0x03, 0x99, 0x81, 0xAC, 0xFF, 0xF4, 0x80, 0xE6, 0x70, 0x5B, 0x3F, 0xF4, 0xC0, 0xE6, 0x80, 0x4B, 0x3F, 0xD4, 0x03, 0xCF, 0xFF, 0xA9, 0x03, 0xCD, 0xFF, 0x3F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x06, 0x80};

static const uint8_t checkData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x1C, 0x02, 0xCF, 0x0C, 0x34, 0x34, 0x3E, 0x61, 0x36, 0x00, 0x0B, 0xE6, 0x26, 0xCB, 0x0F, 0x98, 0xCD, 0x61, 0xF3, 0x19, 0xAC, 0x3E, 0x63, 0x35, 0x87, 0xCC, 0x66, 0xB0, 0xD9, 0x9D, 0x8F, 0xD6, 0x1A, 0x31, 0xD0, 0x24, 0x35, 0x86, 0x4D, 0x34, 0x00, 0xFF, 0xAC, 0x30, 0x6A, 0xA0, 0x01, 0xFF, 0x58, 0x5C, 0xCD, 0x40, 0x02, 0x43, 0x58, 0x58, 0xCD, 0x40, 0x02, 0x53, 0x58, 0x54, 0xCD, 0x40, 0x02, 0x63, 0x58, 0x50, 0xCD, 0x40, 0x02, 0x73, 0x58, 0x51, 0xBF, 0x3F, 0xFC, 0x40, 0x0C, 0xE7, 0x35, 0x81, 0x0C, 0x72, 0x12, 0x4C, 0xD4, 0x00, 0x29, 0x35, 
	0x80, 0xCE, 0x94, 0x01, 0x7F, 0xFF, 0xC0, 0x02, 0xA3, 0x58, 0x0C, 0xE7, 0x20, 0x05, 0xFF, 0xF0, 0x00, 0xAC, 0xD6, 0x04, 0x38, 0xC8, 0x01, 0x7F, 0xC0, 0x02, 0xC3, 0x58, 0x14, 0xDF, 0x20, 0x05, 0xF0, 0x00, 0xB4, 0xD6, 0x06, 0x32, 0xA4, 0x07, 0xA8, 0x00, 0x5C, 0x6B, 0x03, 0x9A, 0xD2, 0x00, 0x5E, 0x6B, 0x04, 0x1A, 0x52, 0x01, 0x81, 0xAC, 0x12, 0x67, 0x48, 0x18, 0x9A, 0xC1, 0x46, 0x54, 0x86, 0x46, 0xB0, 0x59, 0x8D, 0x26, 0x66, 0xB0, 0x61, 0x84, 0x68, 0x6B, 0x0F, 0x98, 0xCD, 0x61, 0xF3, 0x19, 0xAC, 0x3E, 0x63, 0x35, 0x87, 0xCC, 0x66, 0xB0, 0xF9, 0x8C, 0xD6, 0x00, 0x2F, 0x98, 0x9B, 0x30, 0x3E, 0x61, 
	0x37, 0x00, 0x0B, 0xA6, 0x1A, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x06, 0x00};

static const uint8_t cloudData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x09, 0x02, 0x66, 0xBF, 0xE9, 0xF8, 0x0C, 0x03, 0x3A, 0x8C, 0x05, 0xF0, 0x0C, 0xEE, 0x30, 0x0F, 0x02, 0x6C, 0xFF, 0xFF, 0xDA, 0x00, 0xB3, 0x23, 0x0D, 0x17, 0x00, 0xB3, 0x43, 0x0D, 0x15, 0x00, 0xCF, 0x63, 0x0A, 0x54, 0x0C, 0x9C, 0x80, 0x2A, 0xE8, 0xC2, 0x93, 0x80, 0x2C, 0xF4, 0xDA, 0x01, 0x9E, 0xC6, 0x9A, 0x7F, 0xFF, 0x56, 0x00, 0x1B, 0x9A, 0xA8, 0x1F, 0xFF, 0x94, 0x00, 0x1B, 0x9B, 0xA8, 0x01, 0xFF, 0xFA, 0x4E, 0x17, 0x33, 0x50, 0x00, 0xA4, 0xCA, 0x90, 0x00, 0xAC, 0x66, 0xA0, 0x01, 0x59, 0x92, 0x80, 0x2C, 0x66, 0xA0, 
	0x01, 0x69, 0x8E, 0x81, 0x69, 0xA1, 0x8F, 0xC4, 0x00, 0xCE, 0xE3, 0x14, 0x93, 0x0D, 0xD2, 0x17, 0xFC, 0x00, 0x31, 0x30, 0xC6, 0xB3, 0x75, 0x00, 0x5F, 0x00, 0x0C, 0x8C, 0x33, 0xAC, 0xCA, 0x90, 0x1E, 0xA0, 0x01, 0x99, 0x85, 0x96, 0x1A, 0xD2, 0x00, 0x7E, 0x64, 0x34, 0xA4, 0x03, 0x63, 0x0B, 0x2D, 0x33, 0xA4, 0x0D, 0xCC, 0x33, 0xB8, 0xCA, 0x90, 0xE0, 0xC3, 0x1B, 0xCC, 0x69, 0x38, 0x31, 0x51, 0x40, 0xC2, 0x39, 0x31, 0x24, 0xF9, 0xB0, 0xC4, 0x00, 0x1F, 0x9A, 0x8C, 0xA9, 0x0F, 0xCD, 0x06, 0x7A, 0x00, 0x07, 0xE6, 0x63, 0x55, 0x00, 0x00, 0x17, 0xCC, 0x66, 0xD4, 0x80, 0x04, 0x05, 0xD3, 0x0A, 0xDF, 0x0F, 
	0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE0, 0x50};

static const uint8_t editData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x09, 0x05, 0xDF, 0x05, 0x80, 0xCC, 0x05, 0xF0, 0x48, 0x14, 0xC3, 0x47, 0xC0, 0xC0, 0x73, 0x0D, 0x1F, 0x02, 0x02, 0xA7, 0xFF, 0xFF, 0xBE, 0x05, 0x05, 0x4F, 0xFF, 0xFF, 0x7C, 0x0C, 0x84, 0x50, 0x7F, 0x07, 0xC7, 0xA0, 0x3F, 0xFF, 0xDF, 0x01, 0x99, 0x12, 0x25, 0x3F, 0x81, 0x4D, 0x12, 0x03, 0xFF, 0xF0, 0x39, 0x91, 0x22, 0x13, 0xF8, 0x24, 0xC8, 0x91, 0xF8, 0x2C, 0xC2, 0x97, 0xC0, 0x31, 0xB0, 0xC0, 0x3F, 0x07, 0x1F, 0xC1, 0xC7, 0xF0, 0x71, 0xFC, 0x1C, 0x7F, 0x07, 0x1F, 0xC1, 0xC7, 0xF0, 0x71, 0xFC, 0x1C, 0x7F, 0x07, 0x1F, 0xC1, 0xC7, 
	0xF0, 0x71, 0xFC, 0x02, 0xCB, 0x0F, 0xE0, 0x16, 0x58, 0x7F, 0x00, 0xB2, 0xC3, 0xF8, 0x05, 0x96, 0x1F, 0xC0, 0x31, 0xB0, 0xFE, 0x01, 0x06, 0x1F, 0xC0, 0x40, 0xB3, 0xF8, 0x0C, 0x14, 0x7F, 0x02, 0x02, 0x4F, 0xE0, 0x50, 0x41, 0xFC, 0x0C, 0x07, 0x3F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC0, 0x20};

static const uint8_t emojiData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x20, 0xB9, 0x6B, 0xFE, 0x97, 0xE0, 0x28, 0x26, 0x98, 0x0B, 0xA1, 0x36, 0x7F, 0xFF, 0xEC, 0x80, 0x67, 0xA1, 0x80, 0x6C, 0x01, 0x66, 0xC6, 0x1A, 0x26, 0x00, 0x2C, 0x98, 0x68, 0x88, 0x00, 0xB6, 0x61, 0xA1, 0xE0, 0x02, 0xE9, 0x86, 0x86, 0x80, 0x07, 0xE6, 0x14, 0x98, 0x1F, 0x30, 0x9B, 0x00, 0x05, 0xF3, 0x13, 0x65, 0x87, 0xCC, 0x66, 0xA0, 0x4B, 0x3F, 0xE3, 0x49, 0x49, 0x8D, 0x25, 0x06, 0x02, 0x48, 0x20, 0xE7, 0x20, 0x0F, 0xFF, 0xFA, 0x00, 0xA0, 0xD0, 0x01, 0x9C, 0xE6, 0x50, 0x39, 0x94, 0x10, 0x69, 0x48, 0x05, 0x06, 0x50, 0x39, 0x94, 0x10, 0x68, 0x48, 0x02, 
	0x59, 0xFF, 0x9C, 0x80, 0x3F, 0xFF, 0xE8, 0x02, 0x83, 0x01, 0x1C, 0x14, 0xCF, 0xFC, 0x69, 0x29, 0x31, 0xA4, 0xA4, 0xC0, 0x47, 0x0F, 0x99, 0xCC, 0xE1, 0xF3, 0x39, 0x9C, 0x3E, 0x67, 0x33, 0x87, 0xCC, 0xE6, 0x70, 0x0B, 0x3F, 0x32, 0x1A, 0x68, 0x00, 0x02, 0xF9, 0x94, 0xD3, 0x40, 0x00, 0x0A, 0x0C, 0x03, 0x10, 0x0C, 0xE7, 0x34, 0xA4, 0x02, 0x83, 0x01, 0x44, 0x12, 0xCF, 0xFA, 0x52, 0x00, 0x49, 0xC4, 0x12, 0x69, 0x05, 0x33, 0xFE, 0x78, 0x29, 0x9F, 0xF2, 0x81, 0x26, 0x1A, 0x18, 0x00, 0x24, 0x9A, 0xC1, 0x6C, 0xFF, 0xCB, 0x05, 0xB3, 0xFF, 0x30, 0x14, 0x6B, 0xA4, 0x00, 0x01, 0x94, 0x6D, 0x00, 0xCE, 0xA3, 
	0x3D, 0x20, 0x0C, 0xB3, 0x0A, 0x4D, 0x00, 0xB3, 0xC3, 0x0D, 0x0F, 0x00, 0x16, 0xCC, 0x34, 0x44, 0x00, 0x59, 0x30, 0xD1, 0x30, 0x01, 0x5C, 0xC3, 0x45, 0x40, 0x33, 0xD0, 0xC0, 0x39, 0x09, 0xB3, 0xFF, 0xFF, 0x74, 0x1E, 0xCF, 0xFF, 0xDF, 0x06, 0xC0, 0xB5, 0xFF, 0x4B, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x84, 0x40};

static const uint8_t errorData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x20, 0xB9, 0x6B, 0xFE, 0x97, 0xE0, 0x28, 0x26, 0x98, 0x0B, 0xA1, 0x36, 0x7F, 0xFF, 0xEC, 0x80, 0x67, 0xA1, 0x80, 0x6C, 0x01, 0x66, 0xC6, 0x1A, 0x26, 0x00, 0x2C, 0x98, 0x68, 0x88, 0x00, 0xB6, 0x61, 0xA1, 0xE0, 0x02, 0xE9, 0x86, 0x86, 0x80, 0x07, 0xE6, 0x14, 0x98, 0x1F, 0x30, 0x9B, 0x00, 0x04, 0xE3, 0x18, 0x38, 0xC0, 0x4B, 0x07, 0x98, 0xC1, 0xE6, 0xA0, 0x0B, 0x2E, 0x31, 0x83, 0xCC, 0x34, 0x20, 0x20, 0x63, 0x08, 0x1A, 0x00, 0x03, 0x03, 0x18, 0x40, 0xD2, 0x90, 0x0C, 0x0C, 0x61, 0x03, 0x42, 0x40, 0x00, 0x50, 0x31, 0x84, 0x0D, 0x34, 0x00, 0x01, 0x40, 0xC6, 
	0x10, 0x30, 0xD0, 0x60, 0x89, 0x8C, 0x22, 0x67, 0x08, 0x98, 0xC2, 0x26, 0x70, 0x89, 0x8C, 0x22, 0x67, 0x08, 0x98, 0xC2, 0x26, 0x70, 0x01, 0x7C, 0xCA, 0x69, 0xA0, 0x00, 0x0B, 0xE6, 0x53, 0x4D, 0x00, 0x00, 0x3F, 0x32, 0x9A, 0x52, 0x01, 0xF9, 0x94, 0xD2, 0x90, 0x04, 0x0C, 0x61, 0x03, 0x48, 0x05, 0x97, 0x18, 0xC1, 0xE6, 0x1A, 0x12, 0x0F, 0x31, 0x83, 0xCD, 0x60, 0x16, 0x5A, 0x63, 0x07, 0x18, 0x09, 0x81, 0xF3, 0x09, 0xB4, 0x00, 0x3F, 0x30, 0xA4, 0xD0, 0x0B, 0x3C, 0x30, 0xD0, 0xF0, 0x01, 0x6C, 0xC3, 0x44, 0x40, 0x05, 0x93, 0x0D, 0x13, 0x00, 0x15, 0xCC, 0x34, 0x54, 0x03, 0x3D, 0x0C, 0x03, 0x90, 0x9B, 
	0x3F, 0xFF, 0xF7, 0x41, 0xEC, 0xFF, 0xFD, 0xF0, 0x6C, 0x0B, 0x5F, 0xF4, 0xBF, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x44, 0x00};

static const uint8_t handData[] PROGMEM = {
	0xB8, 0x09, 0x97, 0xC1, 0x60, 0x46, 0x77, 0xC1, 0x40, 0x43, 0xF8, 0x28, 0x08, 0x62, 0x01, 0x32, 0xF0, 0x46, 0xB8, 0x0F, 0xFC, 0x2F, 0xEE, 0x03, 0x6B, 0xF8, 0x3F, 0xF0, 0x21, 0xF0, 0x08, 0x68, 0x83, 0xFF, 0x02, 0x1F, 0x00, 0x86, 0x88, 0x3F, 0xF0, 0x21, 0xF0, 0x08, 0x68, 0x83, 0xFF, 0x02, 0x1F, 0x00, 0x87, 0x38, 0x3F, 0xF0, 0xFF, 0xC0, 0xBA, 0xC0, 0x43, 0xA4, 0x1F, 0xF8, 0x7F, 0xE1, 0x7F, 0x54, 0x08, 0x6F, 0x83, 0xFF, 0x0F, 0xFC, 0x08, 0x75, 0x02, 0x1B, 0xE0, 0xFF, 0xC3, 0xFF, 0x02, 0x1D, 0x40, 0x86, 0xF8, 0x3F, 0xF0, 0xFF, 0xC0, 0x87, 0x50, 0x21, 0xBE, 0x0F, 0xFC, 0x3F, 0xF0, 0x21, 0xD4, 0x08, 
	0x6F, 0x83, 0xFF, 0x0F, 0xFC, 0x08, 0x75, 0x02, 0x1B, 0xE0, 0xFF, 0xC3, 0xFF, 0x02, 0x1D, 0x40, 0x86, 0xF8, 0x3F, 0xF0, 0xFF, 0xC0, 0x87, 0x50, 0x21, 0xBE, 0x0F, 0xFC, 0x3F, 0xF0, 0x21, 0xD4, 0x08, 0x6F, 0x83, 0xFF, 0x0F, 0xFC, 0x08, 0x6A, 0x0F, 0xCF, 0xC8, 0x00, 0x7F, 0xE1, 0xFF, 0x87, 0xFE, 0x04, 0x35, 0x01, 0x0F, 0x54, 0x00, 0x7F, 0xE1, 0xFF, 0x87, 0xFE, 0x04, 0x35, 0x01, 0x4F, 0x32, 0x01, 0xFF, 0x87, 0xFE, 0x1F, 0xF8, 0x10, 0xD4, 0x1F, 0xAF, 0xFF, 0x00, 0xFF, 0xC3, 0xFF, 0x0F, 0xFC, 0x08, 0x6B, 0x02, 0x98, 0xD2, 0x32, 0x6B, 0x03, 0x33, 0xE1, 0x06, 0x4D, 0x60, 0x99, 0xFF, 0xF0, 0x32, 0x6C, 
	0x02, 0x98, 0x92, 0x64, 0xD8, 0x06, 0x67, 0xE1, 0x0C, 0x9B, 0x01, 0x13, 0xFF, 0xE9, 0x93, 0x68, 0x05, 0x9F, 0x9B, 0x40, 0x33, 0xFC, 0xDC, 0x1F, 0x30, 0xD8, 0xD0, 0x01, 0x7C, 0xDC, 0x01, 0x9F, 0xA6, 0x1A, 0x1C, 0x00, 0x2E, 0x98, 0x68, 0x70, 0x00, 0x7A, 0x61, 0x49, 0xE1, 0xD3, 0x88, 0x05, 0x9D, 0x18, 0x68, 0x88, 0x6C, 0xE6, 0x01, 0x9F, 0x06, 0x1A, 0x26, 0x00, 0x2B, 0x98, 0x68, 0xA8, 0x5C, 0xF0, 0x15, 0x30, 0xA5, 0x80, 0x9B, 0x3F, 0xFF, 0xF7, 0x02, 0x2C, 0xFF, 0xFF, 0x7C, 0x1A, 0xCF, 0xFF, 0x7C, 0x06, 0x09, 0x9A, 0xFF, 0xA6, 0x00};

static const uint8_t lightbulbData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x40, 0x99, 0xAF, 0xFA, 0x7E, 0x03, 0x00, 0xCE, 0xA3, 0x00, 0xFC, 0x02, 0xAB, 0x4C, 0x2B, 0x70, 0x03, 0x3C, 0x0C, 0x03, 0xA0, 0x0C, 0xF2, 0x30, 0x0E, 0x00, 0x33, 0xD0, 0xC0, 0x37, 0x00, 0xB3, 0x43, 0x0D, 0x15, 0x00, 0xCF, 0x63, 0x0A, 0x54, 0x0C, 0x9D, 0x00, 0x33, 0xE0, 0xC2, 0x94, 0x80, 0x2C, 0xE0, 0xC3, 0x44, 0x80, 0x05, 0x93, 0x0D, 0x12, 0x0D, 0x9C, 0xC3, 0x67, 0x30, 0xD9, 0xCC, 0x36, 0x73, 0x00, 0xB3, 0x83, 0x0D, 0x12, 0x00, 0x16, 0x4C, 0x34, 0x48, 0x00, 0x39, 0x30, 0xA5, 0x30, 0xC1, 0x86, 0x8A, 0x00, 0x06, 0xE6, 0x14, 0xAA, 0x01, 0x66, 0x86, 0x1A, 
	0x2E, 0x15, 0x3C, 0x80, 0x67, 0x91, 0x80, 0x74, 0x01, 0x9E, 0x06, 0x01, 0xE8, 0x3C, 0xFE, 0x0F, 0x3F, 0x83, 0xCF, 0xE0, 0xF3, 0xF8, 0x3C, 0xFE, 0x0F, 0x3F, 0x80, 0x67, 0x61, 0x80, 0x7E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x04, 0x16, 0x7F, 0x01, 0x82, 0xCF, 0xE0, 0x30, 0x59, 0xFC, 0x06, 0x01, 0x9D, 0x06, 0x01, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x20};

static const uint8_t memoryData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x44, 0x06, 0x63, 0x01, 0x9F, 0xC0, 0x60, 0x33, 0x18, 0x0C, 0xFE, 0x03, 0x01, 0x98, 0xC0, 0x67, 0xF0, 0x18, 0x0C, 0xC6, 0x03, 0x3E, 0x80, 0x59, 0xA1, 0x86, 0x8A, 0x86, 0x4E, 0x80, 0x16, 0x70, 0x61, 0xA2, 0x41, 0xB3, 0x98, 0x0C, 0xE6, 0x03, 0x39, 0x80, 0xCE, 0x60, 0x33, 0x98, 0x0C, 0xE6, 0x03, 0x39, 0x80, 0xCE, 0x60, 0x33, 0x78, 0x1C, 0xC6, 0x0B, 0x31, 0x81, 0xCD, 0x60, 0x73, 0x18, 0x2C, 0xC6, 0x07, 0x35, 0x81, 0xCC, 0x60, 0xB3, 0x18, 0x1C, 0xD6, 0x07, 0x31, 0x82, 0xCC, 0x60, 0x73, 0x78, 0x0C, 0xC6, 0x03, 0x31, 0x80, 0xCC, 0x60, 0x33, 
	0x98, 0x0C, 0xC6, 0x03, 0x31, 0x80, 0xCC, 0x60, 0x33, 0x98, 0x0C, 0xC6, 0x03, 0x31, 0x80, 0xCC, 0x60, 0x33, 0x98, 0x0C, 0xC6, 0x03, 0x31, 0x80, 0xCC, 0x60, 0x33, 0x78, 0x1C, 0xC6, 0x0B, 0x31, 0x81, 0xCD, 0x60, 0x73, 0x18, 0x2C, 0xC6, 0x07, 0x35, 0x81, 0xCC, 0x60, 0xB3, 0x18, 0x1C, 0xD6, 0x07, 0x31, 0x82, 0xCC, 0x60, 0x73, 0x78, 0x0C, 0xE6, 0x03, 0x39, 0x80, 0xCE, 0x60, 0x33, 0x98, 0x0C, 0xE6, 0x03, 0x39, 0x80, 0xCE, 0x60, 0x33, 0x98, 0x6C, 0xE6, 0x01, 0x67, 0x06, 0x1A, 0x26, 0x19, 0x3B, 0x00, 0x59, 0xA1, 0x86, 0x8E, 0x00, 0xCC, 0x60, 0x33, 0xF8, 0x0C, 0x06, 0x63, 0x01, 0x9F, 0xC0, 0x60, 0x33, 
	0x18, 0x0C, 0xFE, 0x03, 0x01, 0x98, 0xC0, 0x67, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0x88, 0x00};

static const uint8_t notificationsData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xB0, 0x4A, 0x40, 0x03, 0x66, 0x40, 0x00, 0x6A, 0x1B, 0x9F, 0x40, 0x00, 0xBF, 0xE0, 0x00, 0x1F, 0x64, 0x1C, 0x9F, 0xF4, 0x00, 0x3F, 0xFC, 0x00, 0x1F, 0xF9, 0x01, 0x0E, 0x74, 0x00, 0x1F, 0xFE, 0x00, 0x11, 0x0E, 0x01, 0xFA, 0xFF, 0x80, 0x01, 0xFF, 0xF4, 0x00, 0x2F, 0xF9, 0xC0, 0x56, 0x78, 0x60, 0x15, 0x52, 0x6B, 0xA0, 0x02, 0xFF, 0x9A, 0x03, 0x32, 0xD0, 0x02, 0xD3, 0x4C, 0x80, 0x5F, 0xEC, 0x04, 0xD7, 0xF8, 0x04, 0xF3, 0x4C, 0x80, 0x7F, 0xCB, 0x01, 0x99, 0x12, 0x31, 0x34, 0x48, 0x0F, 0xFA, 0x81, 0x15, 0xFC, 0x01, 0x33, 0x18, 0x11, 0x9C, 0x90, 0x19, 
	0x8D, 0x20, 0x05, 0x33, 0x45, 0x00, 0xFF, 0xA0, 0x11, 0x3F, 0xE0, 0x15, 0x31, 0x80, 0xCD, 0x29, 0x00, 0x9E, 0xFE, 0x01, 0xA9, 0xE6, 0x40, 0xBF, 0x40, 0x00, 0x2F, 0xD0, 0x2A, 0x9E, 0x68, 0x07, 0xF8, 0x00, 0x02, 0xFD, 0x02, 0xA9, 0xE2, 0x80, 0x7F, 0x80, 0x00, 0x3F, 0xC0, 0x2E, 0x63, 0x01, 0x9B, 0xC2, 0xE7, 0x70, 0xB9, 0xDC, 0x2E, 0x77, 0x0B, 0x9D, 0xC2, 0xE7, 0x70, 0xB9, 0xDC, 0x2E, 0x77, 0x0B, 0x9D, 0xC2, 0xE7, 0x70, 0xB9, 0xDC, 0x2E, 0x76, 0x00, 0xCF, 0x63, 0x0A, 0x53, 0x00, 0xCF, 0x83, 0xA2, 0x40, 0x00, 0x07, 0x67, 0x24, 0x80, 0x00, 0x3D, 0x30, 0xA4, 0xE0, 0xF9, 0xBC, 0x3E, 0x7F, 0x0F, 0x87, 
	0xC3, 0x60, 0x73, 0xF8, 0x1C, 0x0E, 0xCF, 0xDF, 0x04, 0x01, 0x4F, 0xE0, 0xA0, 0x1B, 0x37, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x15, 0x00};

static const uint8_t personData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x09, 0x81, 0xE6, 0xFF, 0x9F, 0x81, 0x81, 0x2C, 0xFF, 0x7C, 0x08, 0x0B, 0x67, 0xFE, 0xF8, 0x08, 0x00, 0x4B, 0x30, 0x17, 0xC0, 0x28, 0xB4, 0xC0, 0x3F, 0x07, 0xB3, 0xFF, 0xF7, 0xC1, 0xE7, 0xF0, 0x79, 0xFC, 0x1E, 0x7F, 0x07, 0x9F, 0xC1, 0xEC, 0xFF, 0xFD, 0xF0, 0x0C, 0xEC, 0x30, 0x0F, 0xC0, 0x31, 0x2C, 0xC0, 0x5F, 0x01, 0x00, 0x09, 0x26, 0x02, 0xF8, 0x10, 0x12, 0xCF, 0xF7, 0xC0, 0xC0, 0x79, 0xBF, 0xE7, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x81, 0x41, 0x73, 0x57, 0xFD, 0x4F, 0xC0, 0x50, 0xCD, 0x30, 0xAD, 0xA0, 0x0A, 
	0xB3, 0x30, 0xAD, 0x60, 0x0B, 0x36, 0x30, 0xD1, 0x20, 0x00, 0xEC, 0xC2, 0x94, 0x03, 0xA7, 0x00, 0x0B, 0x3C, 0x30, 0xD0, 0xE0, 0xF9, 0xBC, 0x3E, 0x6F, 0x0F, 0x9B, 0xC3, 0xE6, 0xF0, 0xF9, 0xFC, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x81, 0xC0};

static const uint8_t phoneData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x01, 0x80, 0x07, 0xE0, 0xE0, 0x0D, 0xBF, 0x06, 0x80, 0xAF, 0x7E, 0x0C, 0x01, 0x18, 0x05, 0xA0, 0xA9, 0xFF, 0xFF, 0xD9, 0x00, 0xCC, 0x29, 0x2C, 0x14, 0x72, 0x02, 0x18, 0x52, 0x50, 0x28, 0xD4, 0x0D, 0x30, 0xA4, 0x90, 0x51, 0xA8, 0x1C, 0x6A, 0x48, 0x00, 0xA3, 0x50, 0x3C, 0xD2, 0x90, 0x02, 0x99, 0xFF, 0xA9, 0x20, 0x04, 0x0D, 0x09, 0x00, 0x53, 0x3F, 0xF5, 0x24, 0x00, 0x81, 0xA1, 0x20, 0x0B, 0x67, 0xFE, 0x48, 0x3C, 0xD4, 0x90, 0x05, 0x46, 0x90, 0x71, 0x86, 0x84, 0x82, 0x8C, 0x02, 0x80, 0x69, 0x86, 0x85, 0x02, 0x8E, 0x20, 0x43, 0x0D, 0x0B, 0x04, 0xB3, 0xFE, 0x01, 0x88, 0x0C, 
	0xC0, 0x4D, 0x04, 0xCF, 0xFF, 0xFB, 0x20, 0x11, 0x80, 0x9E, 0x06, 0x30, 0x0C, 0xC0, 0x4D, 0xA0, 0x05, 0x67, 0xC0, 0x34, 0x00, 0xB5, 0x10, 0x2C, 0xFF, 0xB5, 0x00, 0xD1, 0x20, 0x23, 0x3B, 0xE0, 0xA0, 0x2C, 0xFF, 0xBF, 0x05, 0x01, 0x19, 0xFF, 0x05, 0x80, 0xCC, 0x05, 0xF0, 0x50, 0x11, 0x9E, 0x01, 0xF8, 0x28, 0x08, 0xCF, 0x00, 0xFC, 0x14, 0x04, 0x30, 0xA5, 0xF0, 0x40, 0x1A, 0x7F, 0xF5, 0x60, 0x01, 0xE0, 0x1A, 0x7F, 0xF5, 0x00, 0x69, 0xFE, 0x5C, 0x83, 0xAB, 0xFF, 0x40, 0x00, 0x54, 0x60, 0x1A, 0x81, 0x99, 0xF1, 0x00, 0x02, 0xC3, 0xB0, 0x2A, 0x7F, 0xFD, 0x05, 0xA7, 0x70, 0x44, 0xFF, 0xFC, 0xB8, 0xF2, 
	0x01, 0x66, 0x67, 0xA0, 0x0B, 0x32, 0x3E, 0x00, 0x59, 0x81, 0xF4, 0x02, 0xAB, 0xCF, 0xE0, 0x15, 0x5A, 0x7F, 0x01, 0x00, 0x55, 0x51, 0xFC, 0x0C, 0x08, 0x9A, 0xBF, 0xDF, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x81, 0x40};

static const uint8_t powerData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x3C, 0x06, 0x67, 0x01, 0x9F, 0xC0, 0x66, 0x70, 0x19, 0xFC, 0x06, 0x67, 0x01, 0x9F, 0xC0, 0x66, 0x70, 0x19, 0xFC, 0x06, 0x67, 0x01, 0x9F, 0xC0, 0x66, 0x70, 0x19, 0xFC, 0x06, 0x67, 0x01, 0x9F, 0xC0, 0x66, 0x70, 0x19, 0xF4, 0x26, 0xCF, 0xFF, 0xFD, 0xA0, 0xA9, 0xE0, 0x02, 0xCD, 0x0C, 0x34, 0x58, 0x2E, 0x77, 0x0B, 0x9D, 0xC2, 0xE7, 0x70, 0xB9, 0xDC, 0x2E, 0x77, 0x0B, 0x9D, 0xC2, 0xE7, 0x70, 0xB9, 0xDC, 0x2E, 0x77, 0x0B, 0x9D, 0xC2, 0xE7, 0x70, 0xB9, 0xDC, 0x03, 0x3D, 0x0C, 0x03, 0x80, 0x0C, 0xF2, 0x30, 0x0E, 0x80, 0x33, 0xC0, 0xC0, 0x3C, 
	0x00, 0xCE, 0xE3, 0x00, 0xF8, 0x03, 0x3B, 0x0C, 0x03, 0xF0, 0x0C, 0x4B, 0x30, 0x0F, 0xC0, 0x40, 0x02, 0x49, 0x80, 0x7E, 0x04, 0x04, 0x9F, 0xC0, 0xA0, 0x93, 0xF8, 0x14, 0x12, 0x7F, 0x02, 0x82, 0x4F, 0xE0, 0x50, 0x49, 0xFC, 0x0A, 0x09, 0x3F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x84, 0x80};

static const uint8_t sdData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x40, 0x19, 0xDE, 0x60, 0x2D, 0x80, 0x67, 0x91, 0xE4, 0x03, 0x3C, 0xCC, 0x05, 0x70, 0x0C, 0xF5, 0x3B, 0x06, 0x27, 0xFC, 0x03, 0xC0, 0x3C, 0x03, 0xFE, 0xA1, 0x99, 0xFF, 0xC0, 0x3C, 0x03, 0xC0, 0x3F, 0xE8, 0x1A, 0x9F, 0xFF, 0x00, 0xF0, 0x0F, 0x00, 0xFF, 0x98, 0x6E, 0x7F, 0xFF, 0x00, 0xF0, 0x0F, 0x00, 0xFF, 0x90, 0x72, 0x7F, 0xFF, 0xC0, 0x3C, 0x03, 0xC0, 0x3F, 0xE2, 0x1D, 0x9F, 0xFF, 0xFC, 0x03, 0xC0, 0x3C, 0x03, 0xFE, 0x00, 0x19, 0xD0, 0x63, 0x07, 0xDE, 0x01, 0xE0, 0x1F, 0xEF, 0x00, 0xCE, 0x93, 0x18, 0x3E, 0xF0, 0x0F, 0x00, 0xFF, 0x78, 0x7C, 0xDE, 0x1F, 
	0x37, 0x87, 0xCD, 0xE1, 0xF3, 0x78, 0x7C, 0xDE, 0x1F, 0x37, 0x87, 0xCD, 0xE1, 0xF3, 0x78, 0x7C, 0xDE, 0x1F, 0x37, 0x87, 0xCD, 0xE1, 0xF3, 0x78, 0x7C, 0xDE, 0x1F, 0x37, 0x87, 0xCD, 0xE1, 0xF3, 0x78, 0x7C, 0xDE, 0x1F, 0x37, 0x87, 0xCD, 0xE1, 0xF3, 0x78, 0x7C, 0xDE, 0x1F, 0x37, 0x87, 0xCD, 0xE1, 0xF3, 0x78, 0x7C, 0xDE, 0x01, 0x67, 0x86, 0x1A, 0x1E, 0x1D, 0x39, 0x00, 0x59, 0xC1, 0x86, 0x8F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x10, 0x00};

static const uint8_t smsData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC0, 0xA0, 0x16, 0x7E, 0x61, 0x69, 0x41, 0xF3, 0x29, 0xA0, 0x02, 0xCF, 0xCC, 0x86, 0x1A, 0x0C, 0x1F, 0x33, 0x99, 0xC3, 0xE6, 0x73, 0x38, 0x7C, 0xCE, 0x67, 0x0F, 0x99, 0xCC, 0xE1, 0xF3, 0x39, 0x9C, 0x22, 0x63, 0x08, 0x99, 0xC2, 0x26, 0x30, 0x89, 0x9C, 0x22, 0x63, 0x08, 0x99, 0xC2, 0x26, 0x30, 0x89, 0x9C, 0x22, 0x63, 0x08, 0x99, 0xC2, 0x26, 0x30, 0x89, 0x9C, 0x22, 0x63, 0x08, 0x99, 0xC2, 0x26, 0x30, 0x89, 0x9C, 0x3E, 0x67, 0x33, 0x87, 0xCC, 0xE6, 0x70, 0xF9, 0x9C, 0xCE, 0x1F, 0x33, 0x99, 0xC2, 0x26, 0x30, 0x89, 0x9C, 0x22, 0x63, 0x08, 0x99, 0xC2, 0x26, 0x30, 
	0x89, 0x9C, 0x22, 0x63, 0x08, 0x99, 0xC3, 0xE6, 0x73, 0x38, 0x7C, 0xCE, 0x67, 0x0F, 0x99, 0xCC, 0xE1, 0xF3, 0x39, 0x9C, 0x3E, 0x67, 0x33, 0x87, 0xCC, 0xC6, 0x1A, 0x0C, 0x1F, 0x33, 0x1A, 0x03, 0xE6, 0x43, 0x0D, 0x08, 0x03, 0x18, 0x07, 0xE0, 0x70, 0x29, 0x85, 0x2F, 0x81, 0xC0, 0x86, 0x1A, 0x3E, 0x08, 0x01, 0x98, 0x0B, 0xE0, 0xA0, 0x11, 0x80, 0xBE, 0x0B, 0x01, 0x36, 0xF8, 0x30, 0x02, 0xD7, 0xC1, 0xA0, 0x14, 0xBE, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x24, 0x00};

static const uint8_t starData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0xC0, 0x11, 0xF8, 0x34, 0x02, 0x5F, 0x06, 0x80, 0x4F, 0xE0, 0xC0, 0x1C, 0xFB, 0xF0, 0x58, 0x0C, 0xFE, 0x0A, 0x02, 0xCF, 0xFB, 0xF0, 0x48, 0x15, 0x9E, 0xF8, 0x24, 0x0A, 0x7F, 0x04, 0x01, 0xD9, 0xFB, 0xE0, 0x70, 0x39, 0xFC, 0x0C, 0x09, 0x9F, 0xFF, 0xF7, 0xE0, 0x50, 0x4B, 0x3F, 0xDF, 0x02, 0x00, 0x67, 0x41, 0x80, 0x7A, 0x03, 0x17, 0xB1, 0xFF, 0xE0, 0xC5, 0xE0, 0x35, 0x7F, 0x98, 0x88, 0x02, 0x80, 0x01, 0x7C, 0xC6, 0x61, 0xA1, 0x20, 0x02, 0xF9, 0x89, 0xB3, 0x00, 0x05, 0xF3, 0x0D, 0x0D, 0x00, 0x0F, 0x4E, 0x49, 0x00, 0x00, 0x76, 0x61, 0x4A, 0x41, 0x93, 0xB0, 
	0x05, 0x9A, 0x18, 0x68, 0xB8, 0x05, 0x99, 0x18, 0x68, 0xC8, 0x4D, 0x9F, 0xFF, 0xFB, 0x60, 0x19, 0xE0, 0x60, 0x1D, 0x80, 0x67, 0x81, 0x80, 0x76, 0x13, 0x67, 0xFF, 0xFE, 0xD8, 0x4C, 0xF6, 0x13, 0x3D, 0x00, 0x67, 0x91, 0x80, 0x72, 0x15, 0x67, 0xFC, 0xFF, 0xB2, 0x08, 0x67, 0xFB, 0x24, 0x1F, 0xFF, 0xFB, 0x20, 0x63, 0x3D, 0x00, 0x04, 0x63, 0xC0, 0x5E, 0x7F, 0xFD, 0x00, 0x00, 0x7F, 0xFD, 0xB8, 0x16, 0x7F, 0xD5, 0x81, 0x67, 0xFD, 0xB8, 0x0D, 0x99, 0xE0, 0x36, 0x6B, 0x80, 0xAF, 0x62, 0x02, 0x9F, 0xB8, 0x05, 0x2A, 0x00, 0x07, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE0, 0xB0};

static const uint8_t syncData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x16, 0x00, 0xA5, 0xF0, 0x60, 0x06, 0x7F, 0x83, 0x00, 0x53, 0xFF, 0x05, 0x80, 0xE7, 0xFF, 0x82, 0x80, 0x93, 0xFF, 0xF0, 0x48, 0x16, 0x7F, 0xFF, 0x82, 0x01, 0x73, 0xFF, 0xFF, 0x4F, 0xC0, 0x40, 0x01, 0x61, 0x80, 0xBE, 0x01, 0x45, 0xC6, 0x02, 0xF8, 0x06, 0x76, 0x98, 0x0B, 0xE1, 0x1A, 0xFF, 0xC1, 0xBF, 0xFF, 0x7C, 0x0A, 0xAF, 0xF0, 0x01, 0x29, 0xFC, 0x21, 0x5F, 0x80, 0x01, 0x7F, 0xD4, 0x05, 0x24, 0x00, 0x85, 0x78, 0x00, 0x05, 0xFF, 0x4C, 0x03, 0x6A, 0x40, 0x2C, 0xA0, 0x08, 0xCF, 0x90, 0x0E, 0xBD, 0x48, 0x06, 0x84, 0x00, 0xCC, 0x05, 0x10, 0x19, 0x80, 0x66, 0x04, 0x9F, 0xF8, 0x01, 
	0x27, 0xFE, 0xA0, 0x33, 0x0A, 0x4E, 0x02, 0x33, 0xAA, 0x04, 0x67, 0x3C, 0x06, 0xCE, 0x01, 0xA8, 0x12, 0x7F, 0x9E, 0x03, 0x3B, 0x80, 0xCD, 0xE0, 0x33, 0xB8, 0x0C, 0xDE, 0x03, 0x3B, 0x80, 0xCD, 0xE0, 0x33, 0xB8, 0x0C, 0xDE, 0x03, 0x67, 0x00, 0xD4, 0x09, 0x3F, 0xCF, 0x02, 0x33, 0xAA, 0x04, 0x67, 0x3C, 0x09, 0x3F, 0xF5, 0x01, 0x98, 0x52, 0x78, 0x0C, 0xC0, 0x33, 0x02, 0x4F, 0xFC, 0x40, 0x8C, 0xF4, 0x82, 0xD2, 0x00, 0x44, 0xDA, 0x40, 0x33, 0x5D, 0x00, 0x00, 0x1A, 0x90, 0x0B, 0x33, 0x08, 0x57, 0xFC, 0x00, 0x01, 0xEA, 0x40, 0x03, 0x40, 0x85, 0x7F, 0xC0, 0x00, 0x7E, 0xFC, 0x0A, 0x65, 0x48, 0x06, 0x60, 
	0x1F, 0x04, 0x67, 0xFF, 0xF9, 0x0F, 0xFD, 0xF8, 0x05, 0x96, 0x98, 0x52, 0xF0, 0x05, 0x96, 0x98, 0x52, 0xF8, 0x05, 0x95, 0x98, 0x52, 0xF8, 0x36, 0x06, 0xBF, 0xFF, 0xDF, 0x82, 0x00, 0x86, 0x1A, 0x3E, 0x08, 0x01, 0x98, 0x0B, 0xE0, 0xA0, 0x11, 0x80, 0xBE, 0x0B, 0x01, 0x36, 0xF8, 0x30, 0x02, 0xD7, 0xC1, 0xA0, 0x1A, 0x3E, 0x1F, 0x0F, 0x85, 0x40};

static const uint8_t thermostatData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x80, 0x59, 0xBE, 0x7E, 0x08, 0x03, 0xB3, 0xF7, 0xC0, 0xC0, 0x96, 0x7F, 0xBE, 0x04, 0x00, 0xCE, 0x83, 0x00, 0xFC, 0x06, 0x0B, 0xAF, 0xE0, 0xBF, 0xBE, 0x03, 0x01, 0x98, 0xC0, 0x67, 0xF0, 0x18, 0x0C, 0xC6, 0x03, 0x3F, 0x80, 0xC0, 0x66, 0x30, 0x19, 0xFC, 0x06, 0x03, 0x30, 0x81, 0x4F, 0xE0, 0x30, 0x19, 0x84, 0x0A, 0x7F, 0x01, 0x80, 0xCC, 0x60, 0x33, 0xF8, 0x0C, 0x06, 0x63, 0x01, 0x9F, 0xC0, 0x60, 0x33, 0x18, 0x0C, 0xFE, 0x03, 0x01, 0x98, 0xC0, 0x67, 0xF0, 0x18, 0x0C, 0xC2, 0x05, 0x3F, 0x80, 0xC0, 0x66, 0x10, 0x29, 0xFC, 0x06, 0x03, 0x31, 0x80, 0xCF, 0xE0, 
	0x30, 0x19, 0x8C, 0x06, 0x7F, 0x01, 0x82, 0xCF, 0xE0, 0x30, 0x59, 0xFC, 0x06, 0x0B, 0x3F, 0x80, 0xC1, 0x67, 0xF0, 0x10, 0x00, 0x96, 0x60, 0x2F, 0x80, 0x62, 0x69, 0x80, 0xBC, 0x01, 0x9D, 0xC6, 0x01, 0xE8, 0x44, 0xF8, 0x01, 0x9E, 0x06, 0x01, 0xD8, 0x4D, 0x9F, 0xFF, 0xFB, 0x61, 0x33, 0xD8, 0x4C, 0xF6, 0x13, 0x3D, 0x84, 0xCF, 0x61, 0x36, 0x7F, 0xFF, 0xED, 0x80, 0x67, 0x81, 0x80, 0x78, 0x11, 0x3E, 0x80, 0x67, 0x71, 0x80, 0x7C, 0x0F, 0x67, 0xFF, 0xEF, 0x80, 0x62, 0x59, 0x80, 0xBE, 0x02, 0x00, 0x0A, 0x4C, 0x03, 0xF0, 0x28, 0x1E, 0x6F, 0xF9, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0x60};

static const uint8_t thumbData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1A, 0x00, 0xAB, 0xF0, 0x60, 0x0E, 0x7E, 0xF8, 0x28, 0x0B, 0x3F, 0xF7, 0xC1, 0x00, 0x79, 0xFF, 0xF7, 0xE0, 0x60, 0x44, 0xFF, 0xFE, 0xFC, 0x0A, 0x01, 0x9C, 0xC7, 0xF0, 0x28, 0x06, 0x73, 0x9F, 0xC0, 0x80, 0xA9, 0xFF, 0xFF, 0xEF, 0x80, 0xC0, 0x33, 0xA0, 0xC0, 0x5F, 0x01, 0x00, 0x05, 0x46, 0x01, 0xF8, 0x09, 0x0A, 0xCF, 0xE0, 0x24, 0x2C, 0x3F, 0x80, 0x51, 0x61, 0x80, 0xBE, 0x01, 0x8D, 0x86, 0x02, 0xF8, 0x05, 0x9D, 0x18, 0x68, 0x80, 0x78, 0xC8, 0x07, 0x31, 0x87, 0x8E, 0x14, 0x01, 0xFF, 0xFE, 0x01, 0xF3, 0x18, 0x1C, 0xC6, 0x1F, 0x31, 0x81, 0xCC, 0x61, 0xF3, 0x18, 0x1C, 0xC6, 0x1F, 
	0x31, 0x81, 0xCC, 0x61, 0xF3, 0x18, 0x1C, 0xC6, 0x1F, 0x31, 0x81, 0xCC, 0x61, 0xE3, 0x85, 0x00, 0x7F, 0xFF, 0x80, 0x78, 0xE1, 0x20, 0x1F, 0xFF, 0xE0, 0x1E, 0x32, 0x01, 0xCC, 0x61, 0xD3, 0x8C, 0x80, 0x1F, 0xFF, 0xE0, 0x1D, 0x32, 0x81, 0xCC, 0x61, 0xC3, 0x95, 0x00, 0x07, 0xFF, 0xF8, 0x07, 0x0C, 0xC0, 0x73, 0x18, 0x6C, 0xE7, 0x40, 0x00, 0x7F, 0xFF, 0x80, 0x6C, 0xCE, 0x90, 0x07, 0x31, 0x86, 0xCC, 0xE0, 0x73, 0x18, 0x68, 0xD0, 0x90, 0x03, 0x98, 0xC3, 0x46, 0x80, 0x39, 0x8C, 0x32, 0x75, 0xA0, 0x00, 0x03, 0xFF, 0xFC, 0x03, 0x26, 0x94, 0x80, 0x0E, 0x63, 0x00, 0x15, 0xCC, 0x34, 0x20, 0x0E, 0x64, 0x0B, 
	0x9A, 0x80, 0xE6, 0x50, 0x0B, 0x32, 0x30, 0xD1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0x40, 0x00};

static const uint8_t warningData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0xC0, 0x11, 0xF8, 0x34, 0x02, 0x7F, 0x06, 0x00, 0xE7, 0xDF, 0x82, 0xC0, 0x67, 0xF0, 0x50, 0x15, 0x9E, 0xF8, 0x20, 0x0F, 0x3F, 0xFE, 0xFC, 0x0E, 0x07, 0x67, 0xEF, 0x81, 0x81, 0x33, 0xFF, 0xFE, 0xFC, 0x0A, 0x09, 0x3F, 0x81, 0x00, 0x33, 0xA0, 0xC0, 0x3F, 0x01, 0x82, 0xCF, 0xE0, 0x20, 0x01, 0x2C, 0xC0, 0x5F, 0x00, 0xA2, 0xD3, 0x00, 0xFC, 0x1E, 0xCF, 0xFF, 0xDE, 0x00, 0xCE, 0xE3, 0x00, 0xF4, 0x22, 0x7C, 0x03, 0xB3, 0xF8, 0xC0, 0xC6, 0x02, 0xD8, 0x1C, 0xC6, 0x07, 0x3D, 0x02, 0x19, 0xFE, 0x30, 0x39, 0x80, 0xB0, 0x0D, 0x9F, 0xFF, 0xFC, 0x01, 0x06, 0x01, 0xB8, 
	0x25, 0x9F, 0xF1, 0x82, 0x0C, 0x05, 0x60, 0x0C, 0xE8, 0x31, 0x82, 0x4C, 0x03, 0x50, 0x51, 0x8C, 0x14, 0x74, 0x00, 0xB2, 0x93, 0x18, 0x28, 0xC0, 0x53, 0x0D, 0x9C, 0x80, 0x2C, 0xE8, 0xC3, 0x43, 0xC0, 0x03, 0xD3, 0x0A, 0x4E, 0x00, 0xB3, 0xC3, 0x0D, 0x0D, 0x00, 0xCE, 0xC3, 0x18, 0x34, 0xC2, 0x93, 0x01, 0xC6, 0x30, 0x71, 0xB0, 0x02, 0xCB, 0x4C, 0x60, 0xE3, 0x01, 0x2C, 0x1E, 0x63, 0x07, 0x9A, 0x80, 0x2C, 0xFC, 0xC4, 0x60, 0x24, 0x00, 0x07, 0xE6, 0x53, 0x42, 0x40, 0x3E, 0x67, 0x33, 0x00, 0x67, 0xF9, 0x98, 0xCC, 0x90, 0x3E, 0x69, 0x32, 0x00, 0x59, 0xF9, 0xA0, 0xC3, 0x47, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 
	0x3E, 0x1F, 0x0F, 0x87, 0xC0, 0x00};

static const uint8_t wifiData[] PROGMEM = {
	0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC1, 0xD0, 0xD7, 0xFF, 0xFD, 0x3D, 0x00, 0xAB, 0x13, 0x0A, 0xD7, 0x00, 0xCF, 0x63, 0x0A, 0x52, 0x00, 0xB3, 0xA3, 0x0D, 0x0E, 0x00, 0x0F, 0xCC, 0x29, 0x2C, 0x2A, 0xCF, 0xFF, 0x80, 0x30, 0xC3, 0x42, 0x00, 0x02, 0xA3, 0x0A, 0xCB, 0x00, 0xAA, 0xA3, 0x42, 0x40, 0x50, 0x61, 0x5A, 0x20, 0x15, 0x50, 0x66, 0x48, 0x27, 0x30, 0xAD, 0x50, 0xBC, 0xDF, 0xFF, 0xE8, 0x03, 0xFF, 0xFB, 0x90, 0x9C, 0xFF, 0xFE, 0x80, 0x0F, 0xFD, 0xD0, 0x85, 0x7F, 0xA0, 0x00, 0x5F, 0x28, 0x0E, 0xA7, 0xE1, 0x15, 0x00, 0xD9, 0x90, 0x01, 0x24, 
	0x00, 0x55, 0x59, 0x85, 0x64, 0x00, 0x4A, 0x40, 0x19, 0xE0, 0x60, 0x1D, 0x00, 0x59, 0x91, 0x86, 0x8B, 0x00, 0x67, 0xB1, 0x85, 0x29, 0x80, 0x67, 0xC1, 0x85, 0x29, 0x06, 0xE7, 0xFF, 0xFE, 0x40, 0x01, 0xBF, 0xFF, 0xDA, 0x01, 0xA7, 0xFF, 0x56, 0x06, 0x9F, 0xFD, 0xB0, 0x0D, 0x99, 0xE0, 0x36, 0x6C, 0x00, 0x4A, 0x20, 0x12, 0xF8, 0x7C, 0x24, 0x03, 0x1F, 0x82, 0x01, 0x33, 0x7F, 0xFC, 0xFC, 0x08, 0x01, 0x9D, 0x06, 0x01, 0xF8, 0x0C, 0x03, 0x3A, 0x0C, 0x03, 0xF0, 0x20, 0x26, 0x7F, 0xFF, 0xDF, 0x81, 0x80, 0xEC, 0xFD, 0xF0, 0x40, 0x15, 0x9E, 0xF8, 0x28, 0x06, 0xCD, 0xF0, 0x60, 0x04, 0xBE, 0x1F, 0x0F, 0x87, 
	0xC3, 0xE1, 0xF0, 0xF8, 0x7C, 0x3E, 0x1F, 0x0F, 0x87, 0xC2, 0xC0};

// END-OF-FILE
