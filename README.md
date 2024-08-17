![Mini-poster](https://github.com/user-attachments/assets/1be914a8-f612-4b55-a046-98ea5684088d)

<h1>What is it and what is it for</h1>

It is a simple and lightweight compressor/decompressor based on the delta encoding method. I planned it as a small auxiliary module for my MCU projects to output graphics to the display, but I got a little carried away and the project turned out to be quite extensive. Nevertheless, its main purpose is to reduce the size of graphical interface elements with their further decompression/displaying on the screen. This technology can be useful in embedded small and medium-level systems (among the examples I made [one for ATtiny85](/Examples/AVR/ATtiny85)).

The compressor application is written for Windows (although it can be ported to any platform on which the .NET can be installed), and for decompression I have prepared several versions of the source code, differing in their capabilities and performance.

And of course the project can be used to compress any data.

<h1>How it works</h1>

The delta encoding method is used for compression: instead of storing the original sequence of data, we store only the difference (delta) between each two adjacent values. And in the case of graphical data, this gives a good (at least not shameful) degree of compression.

You can find more information at the links below, where I talk about how this compression method works, about problems that arose during the development and how I solved them:

Boosty (Russian):  
Sprint 1 - https://boosty.to/muravyev/posts/9241e5a2-a490-4530-a376-e9cae0f9e9bf?share=post_link  
Sprint 2 - https://boosty.to/muravyev/posts/dfdd0b1d-f27d-446a-ab38-d55cbc7cd580?share=post_link  
Sprint 3 - https://boosty.to/muravyev/posts/bc58a9af-f075-4004-a98e-6535edab9925?share=post_link  

Patreon (Eng subs):  
Sprint 1 - https://www.patreon.com/posts/delta-encoding-1-106850934  
Sprint 2 - https://www.patreon.com/posts/delta-encoding-2-108056837  
Sprint 3 - https://www.patreon.com/posts/delta-encoding-3-108236064  

<h1>Features</h1>

- Supported image size up to 65535 x 65535px,
- up to 4 data channels (R/G/B/A, L/R, etc),
- up to 32 bits per data channel,
- up to 64 bits per frame (decompressor modification is required for a larger frame width),
- decompression per 1 request: up to 2^32 frames (pixels), up to 2^32 compressed/decompressed data size,
- decompression of 1-2-4 bits frames,
- splitting images into squares,
- encoding to the desired color bit depth for each channel,
- conversion to grayscale,
- alpha channel compression,
- 4 delta calculation options,
- AVR "far" memory access support*,
- splitting compressed data into memory chunks (up to 32767 bytes)*.
- Generating a header file with compressed data and their descriptive metadata. You just need to include this file in your code and pass references to the constants declared in it to the decompression procedure.
- Compression of any data file with the ability to split it into channels and select the channel bit width (so far only multiples of 8 bits are available).

\* Relevant only for the AVR platform.

<h1>How to use it</h1>
<h2>Compression</h2>

![Application](https://github.com/user-attachments/assets/88e63850-9be2-4132-8754-f7246748f318)

The compression application contains 2 tabs: for images and for any other files. After opening an image or file, you can set up the desired channel configuration, and select the ***Split into squares*** mode for images.
In the preview area, you can see how the image (or data divided into channels) will look for the selected configuration.
Compression settings are configured at the bottom of the window:
- The number of bits of the block length field. The larger this field, the longer blocks the compression algorithm will be able to make. This parameter is configured individually for each specific data instance and is usually in the range of 5-8 bits.
- Delta calculation method. A Fixed Window is more suitable for low-contrast noise, and an adaptive floating window is better for gradients (refer to the links above). Combinations of them are also available.
- Searching best blocks algorithm option: either faster or more efficient.
- Selection of the platform for which the header .h-file will be generated. The only difference between AVR and other platforms is that the macro symbol "PROGMEM" is added to the AVR array constant and a warning comment is issued every 16384 bytes of the array about exceeding this threshold. For other platforms these actions are not performed (configuration of platforms and code generation features can be customized and are located in the files: [DeltaComp.dll.config](/Release/DeltaComp.dll.config), [Header_Source.xml](/Release/Header_Source.xml), [Header_Transformation.xslt](/Release/Header_Transformation.xslt)).

After clicking on the "Compress" button and selecting the path to save the file, data analysis is performed first (blue progress bar), and then compression and writing the array to the output file (green progress bar).  
${\color{red}Attention!}$ The file name will be used as the base for macro names and constant names in the generated file.

***In case you don't have .NET 8 installed, I have prepared [self-contained application versions](https://drive.google.com/drive/folders/18RQaH1zRoYLzu4I6Uneg_3HMOnUn0RrV) for x86 and x64.***

In the near future, I will release a video on [my YouTube channel](https://youtube.com/@DmitryMuravyev), in which I will show the sequence of actions more clearly.

<h2>Decompression</h2>

For correct decompression, you only need to configure a few parameters in the file [Decompression.h](/Decompression/Decompression.h):

- Using commenting characters '//', select one of the delta calculating options and, for example, choosing the DECOMPRESSION_FIXED_WINDOW_FIRST option you can decompress data compressed not only by this method (Fixed + Adaptive), but also a data, compressed by Fixed Window only. Likewise using DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST, you can decompress data, compressed both by Adaptive + Fixed and only by Adaptive Window methods:

```C
//#define DECOMPRESSION_FIXED_WINDOW_ONLY
//#define DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_ONLY
#define DECOMPRESSION_FIXED_WINDOW_FIRST
//#define DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST
```
- Set the maximum number of channels (e.g. R/G/B = 3 channels) that will be decompressed in your code:

```C
#define DECOMPRESSION_MAX_NUMBER_OF_CHANNELS	3
```

- Define whether decompression of images divided into squares will be used:

```C
#define DECOMPRESSION_USE_SQUARES
```

- And for the AVR platform, define whether data chunking will be used and set the size of chunks (for data arrays exceeding 32767 bytes):

```C
#define USE_FAR_MEMORY_CHUNKS  
#define CHUNK_SIZE	16384
```

You can find [application examples for different tasks and platforms here](/Examples).

I have made several modifications to the decompression code that differ in capabilities and performance:

1) The original class - maximum features:
	- supported image size up to 65535 x 65535px,
	- up to 32 bits per data channel,
	- up to 64 bits per frame (modification is required for a larger frame width),
	- decompression per 1 request: up to 2^32 frames (pixels), up to 2^32 compressed/decompressed data size,
	- decompression of 1-2-4 bits frames,
	- AVR "far" memory access support*,
	- splitting compressed data into memory chunks (up to 32767 bytes)**.

   Source: [/Decompression](/Decompression)  
   Example: [ATMega2560 + ILI9486 480x320 display](/Examples/AVR/Arduino_boards/Mega2560_ILI9486).

2) High-speed adaptation:
	- focusing on the low-bit-length operations,
	- supported image size is up to 255x255px,
	- up to 8 bits per data channel,
	- up to 16 bits per frame,
	- decompression of 1-2-4 bits frames,
	- AVR "far" memory access support*,
	- splitting compressed data into memory chunks (up to 32767 bytes)**,
	- high performance.

   Source: [/Decompression/Mods/Fast](/Decompression/Mods/Fast)  
   Example: [ATMega2560 + ILI9486 480x320 display](/Examples/AVR/Arduino_boards/Mega2560_ILI9486_Fast).

3) High-speed with pixel support:
	- focusing on the low-bit-length operations,
	- supported image size is up to 255x255px,
	- up to 8 bits per data channel,
	- up to 16 bits per frame,
	- colorizing pixels during decompression (if used),
	- direct use of the RGB565 pixel buffer (if needed),
	- AVR "far" memory access support*,
	- splitting compressed data into memory chunks (up to 32767 bytes)**,
	- high performance+.

  Source: [/Decompression/Mods/Fast+pixel_support](/Decompression/Mods/Fast+pixel_support)  
  Examples:  
  [Arduino Nano + ST7789 240x240 display](/Examples/AVR/Arduino_boards/Nano_ST7789),  
  [ESP32 DEVKIT board + ST7789 240x240 display](/Examples/ESP32/Wroom_ST7789),  
  [STM32F070CB chip + ST7789 240x240 display](/Examples/STM32/F070CB_ST7789_DMA),  
  [DevEBox board by mcudev + NT35510 800x480 display](/Examples/STM32/F407ZGT6_NT35510_FSMC_DMA).  

4) Tiny adaptation (THIS ONE):
	- focusing on the 8-bit operations,
	- supported image size is up to 255x255px,
	- up to 8 bits per data channel,
	- up to 8 bits per frame supported,
	- colorizing pixels during decompression (if used),
	- direct use of the RGB565 pixel buffer (if needed),
	- high performance++.

  Source: [/Decompression/Mods/Tiny+pixel_support](/Decompression/Mods/Tiny+pixel_support)  
  Examples:  
  [Arduino Nano + ST7789 240x240 display](/Examples/AVR/Arduino_boards/Nano_ST7789_Tiny),  
  [ATtiny85 chip + SPI output](/Examples/AVR/ATtiny85).  

\* Relevant only for the AVR platform.  
** To do this, a pointer to an array of far pointers should be passed to the decompression procedure instead of a direct pointer to a data array:

```C
static uint_farptr_t dataChunks[n];
decompressNextFrames(dc, dataChunks, decompressedData, framesCount);
```

<h1>Links</h1>

Project files - https://drive.google.com/drive/folders/1WRB-s4aPv4rQhnDVdqntXX3w0xl3WAg_

Data compression - https://en.wikipedia.org/wiki/Data_compression

Entropy coding - https://en.wikipedia.org/wiki/Entropy_coding

Delta encoding - https://en.wikipedia.org/wiki/Delta_encoding

List of monochrome and RGB color formats - https://en.wikipedia.org/wiki/List_of_monochrome_and_RGB_color_formats

Girls - https://www.freepik.com/free-photo/woman-with-beautiful-body_9828277.htm
https://www.pexels.com/photo/woman-wearing-black-top-1642161/
https://www.freepik.com/free-photo/hispanic-woman-wearing-lingerie-smiling-doing-phone-gesture-with-hand-fingers-like-talking-telephone-communicating-concepts_53265959.htm
