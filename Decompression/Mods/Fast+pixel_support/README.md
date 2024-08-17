High-speed with pixel support:
- focusing on the low-bit-length operations,
- supported image size is up to 255x255px,
- up to 8 bits per data channel,
- up to 16 bits per frame,
- colorizing pixels during decompression (if used),
- direct use of the RGB565 pixel buffer (if needed),
- AVR "far" memory access support*,
- splitting compressed data into memory chunks (up to 32767 bytes)**,
- high performance+.

  Examples:  
  [Arduino Nano + ST7789 240x240 display](/Examples/AVR/Arduino_boards/Nano_ST7789),  
  [ESP32 DEVKIT board + ST7789 240x240 display](/Examples/ESP32/Wroom_ST7789),  
  [STM32F070CB chip + ST7789 240x240 display](/Examples/STM32/F070CB_ST7789_DMA),  
  [DevEBox board by mcudev + NT35510 800x480 display](/Examples/STM32/F407ZGT6_NT35510_FSMC_DMA).  


\* Relevant only for the AVR platform.  
** To do this, a pointer to an array of far pointers should be passed to the decompression procedure instead of a direct pointer to a data array:

```C
static uint_farptr_t dataChunks[n];
decompressNextFrames(dc, dataChunks, decompressedData, framesCount);
```
