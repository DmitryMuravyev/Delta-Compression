The original class - maximum features:
- supported image size up to 65535 x 65535px,
- up to 32 bits per data channel,
- up to 64 bits per frame (modification is required for a larger frame width),
- decompression per 1 request: up to 2^32 frames (pixels), up to 2^32 compressed/decompressed data size,
- decompression of 1-2-4 bits frames,
- AVR "far" memory access support*,
- splitting compressed data into memory chunks (up to 32767 bytes)**.

   Example: [ATMega2560 + ILI9486 480x320 display](/Examples/AVR/Arduino_boards/Mega2560_ILI9486).

\* Relevant only for the AVR platform.  
** To do this, a pointer to an array of far pointers should be passed to the decompression procedure instead of a direct pointer to a data array:

```C
static uint_farptr_t dataChunks[n];
decompressNextFrames(dc, dataChunks, decompressedData, framesCount);
```
