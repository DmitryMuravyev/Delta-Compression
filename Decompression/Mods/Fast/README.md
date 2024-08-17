High-speed adaptation:
- focusing on the low-bit-length operations,
- supported image size is up to 255x255px,
- up to 8 bits per data channel,
- up to 16 bits per frame,
- decompression of 1-2-4 bits frames,
- AVR "far" memory access support*,
- splitting compressed data into memory chunks (up to 32767 bytes)**,
- high performance.

   Example: [ATMega2560 + ILI9486 480x320 display](/Examples/AVR/Arduino_boards/Mega2560_ILI9486_Fast).


\* Relevant only for the AVR platform.  
** To do this, a pointer to an array of far pointers should be passed to the decompression procedure instead of a direct pointer to a data array:

```C
static uint_farptr_t dataChunks[n];
decompressNextFrames(dc, dataChunks, decompressedData, framesCount);
```
