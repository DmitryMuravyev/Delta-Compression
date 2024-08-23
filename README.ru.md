![Mini-poster](https://github.com/user-attachments/assets/1be914a8-f612-4b55-a046-98ea5684088d)

[![English](https://img.shields.io/badge/English-8A2BE2)](README.md)

<h1>Что это и для чего это надо</h1>

Это простой и легковесный компрессор/декомпрессор, основанный на методе дельта-кодирования. Я планировал его как небольшой вспомогательный модуль для вывода микроконтроллером графики на дисплей, но немного увлёкся и проект получился достаточно масштабный. Тем не менее, основное его предназначение - уменьшение размера с последующей декомпрессией и выводом на экран элементов интерфейса. Технология может быть полезна в эмбедированных системах мелкого и среднего уровня (среди примеров я сделал [один для ATtiny85](/Examples/AVR/ATtiny85)).

Приложение компрессора написано для Windows (хотя, может быть портировано на любую платформу, где есть .NET), а для декомпрессии я подготовил несколько вариантов исходного кода, отличающиеся своими возможностями и производительностью.  

И, конечно, проект может быть использован для компрессии любых данных.

<h1>Как это работает</h1>

Для компрессии используется метод дельта-кодирования: вместо того, чтобы хранить оригинальную последовательность данных, мы храним только разность (дельту) между каждыми двумя соседними значениями. И в случае с графическими данными это даёт неплохую (во всяком случае нестыдную) степень компрессии.

Более подробную информацию вы можете найти по ссылкам ниже. Там я рассказываю о том как работает этот метод сжатия, о проблемах, возникших в процессе разработки, и о том, как я их решал:

Boosty (Русский):  
Sprint 1 - https://boosty.to/muravyev/posts/9241e5a2-a490-4530-a376-e9cae0f9e9bf?share=post_link  
Sprint 2 - https://boosty.to/muravyev/posts/dfdd0b1d-f27d-446a-ab38-d55cbc7cd580?share=post_link  
Sprint 3 - https://boosty.to/muravyev/posts/bc58a9af-f075-4004-a98e-6535edab9925?share=post_link  

Patreon (Английские субтитры):  
Sprint 1 - https://www.patreon.com/posts/delta-encoding-1-106850934  
Sprint 2 - https://www.patreon.com/posts/delta-encoding-2-108056837  
Sprint 3 - https://www.patreon.com/posts/delta-encoding-3-108236064  

<h1>Возможности</h1>

- Поддержка изображений с размером до 65535 x 65535 пикселей;
- до 4 каналов данных (R/G/B/A, L/R, и т.д.);
- до 32 бит на канал;
- до 64 бит на кадр (для большей ширины кадра требуется модификация декомпрессора);
- за 1 запрос можно распаковать до 2^32 кадров (пикселей) или до 2^32 упакованных/распакованных данных;
- декомпрессия кадров шириной 1-2-4 бит;
- разбиение изображений на квадраты;
- сжатие произвольной глубины цвета для каждого из каналов;
- преобразование в оттенки серого;
- сжатие альфа-канала;
- 4 варианта вычисления дельты;
- поддержка доступа в "дальнюю" память AVR*;
- разбиение сжатых данных на части (до 32767 байт)*;
- создание заголовочного файла со сжатыми данными и их описанием. Вам надо будет только включить этот файл в ваш код и передать ссылки на объявленные в нём константы в процедуру декомпрессии;
- компрессия произвольных файлов с возможностью разбиения на каналы и выбора ширины каналов (пока доступны только варианты, кратные 8-ми битам);

\* Относится только к платформе AVR.

<h1>Как пользоваться</h1>
<h2>Компрессия</h2>

![Application](https://github.com/user-attachments/assets/88e63850-9be2-4132-8754-f7246748f318)

Приложение для сжатия данных содержит 2 вкладки: для изображений и для любых других файлов. После открытия изображения или файла вы можете настроить желаемую конфигурацию каналов (или формат пикселя) и выбрать режим разбиения на квадраты для изображений.
В области предварительного просмотра можно видеть как будет выглядеть изображение (или данные, разбитые на каналы) с применением выбранной конфигурации.  

Параметры сжатия настраиваются в нижней части окна:
- Количество бит на поле длина блока. Чем больше это поле, тем более длинные блоки сможет создавать алгоритм сжатия. Этот параметр настраивается индивидуально для каждого конкретного экземпляра данных и обычно находится в диапазоне 5-8 бит.
- Способ вычисления дельты. Фиксированное Окно больше подходит для малоконтрастных шумов, а Адаптивное Плавающее Окно лучше подходит для градиентов (см. ссылки выше). Также доступны их комбинации.
- Вариант алгоритма поиска наилучших блоков: либо быстрый, либо эффективный.
- Выбор платформы, для которой будет сгенерирован заголовочный .h-файл. Единственное отличие AVR от других платформ заключается в том, что в определение константы массива добавляется макро-символ "PROGMEM", и через каждые 16384 байта массива выдается предупреждающий комментарий о превышении этого порога. Для других платформ эти действия не выполняются (конфигурация платформ и возможностей генерации кода может быть настроена и находится в файлах: [DeltaComp.dll.config](/Release/DeltaComp.dll.config), [Header_Source.xml](/Release/Header_Source.xml), [Header_Transformation.xslt](/Release/Header_Transformation.xslt)).



После нажатия на кнопку "Compress" и выбора пути сохранения файла, сначала выполняется анализ данных (голубой прогресс-бар), а потом компрессия и запись массива в выходной файл (зелёный прогресс-бар).  
${\color{red} Внимание!}$ Название файла будет использовано в качестве основы для макро-имён и имён констант в генерируемом файле.

В ближайшее время я выпущу ролик на [моём YouTube канале](https://youtube.com/@DmitryMuravyev), в котором покажу последовательность действий более наглядно.


<h2>Декомпрессия</h2>

Для корректной распаковки вам нужно всего лишь настроить несколько параметров в файле [Decompression.h](/Decompression/Decompression.h):

- Используя символы комментирования '//', выберете один из вариантов вычисления дельты. Обратите внимание, что выбирая, например, опцию DECOMPRESSION_FIXED_WINDOW_FIRST вы сможете распаковывать не только данные, сжатые с применением этой опции, но и данные, сжатые с помощью только Фиксированного Окна. И наоборот, выбирая опцию DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST, вы сможете распаковывать данные, сжатые как при помощи Адаптивного + Фиксированного, так и только Адаптированным Окном:

```C
//#define DECOMPRESSION_FIXED_WINDOW_ONLY
//#define DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_ONLY
#define DECOMPRESSION_FIXED_WINDOW_FIRST
//#define DECOMPRESSION_ADAPTIVE_FLOATING_WINDOW_FIRST
```

- Задайте максимальное количество каналов (например, R/G/B = 3 канала), которое будет распаковываться в вашем продукте:

```C
#define DECOMPRESSION_MAX_NUMBER_OF_CHANNELS	3
```

- Определите, будут ли распаковываться изображения, разбитые на квадраты:

```C
#define DECOMPRESSION_USE_SQUARES
```

- А для платформы AVR вы можете определить, будет ли использоваться разбиение сжатых данных на части (для массивов, превышающих 32767 байт):

```C
#define USE_FAR_MEMORY_CHUNKS  
#define CHUNK_SIZE	16384
```

Дополнительно, если используется разбиение на квадраты, то для корректной распаковки вам надо задать значение ширины буфера в пикселях (в коде основной программы):

```C
static Decompression decomp;
decomp.bufferWidth = LOGO_IMAGE_WIDTH;
```

И не забывайте сбрасывать переменные среды декомпрессора перед распаковкой каждого следующего объекта:

```C
decomp.resetDecompression();
```

Вот и все! Как видите, всё довольно просто!  
В пакет входят [примеры применения для разных задач и платформ](/Examples).

Я сделал несколько модификаций кода распаковки, которые отличаются возможностями и производительностью:

1) Оригинальный класс - максимальные возможности:
	- Поддержка изображений с размером до 65535 x 65535 пикселей;
	- до 32 бит на канал;
	- до 64 бит на кадр (для большей ширины кадра требуется модификация декомпрессора);
	- за 1 запрос можно распаковать до 2^32 кадров (пикселей) или до 2^32 упакованных/распакованных данных;
	- декомпрессия кадров шириной 1-2-4 бит;
	- поддержка доступа в "дальнюю" память AVR*;
	- разбиение сжатых данных на части (до 32767 байт)**.

   Исходники: [/Decompression](/Decompression)  
   Пример: [ATMega2560 + дисплей ILI9486 480x320](/Examples/AVR/Arduino_boards/Mega2560_ILI9486).

2) Скоростная адаптация:
	- ориентация на работу с переменными меньшей разрядности;
	- размер изображений до 255 x 255 пикселей;
	- до 8 бит на канал данных;
	- до 16 бит на кадр;
	- декомпрессия кадров шириной 1-2-4 бит;
	- поддержка доступа в "дальнюю" память AVR*;
	- разбиение сжатых данных на части (до 32767 байт)**;
	- высокая производительность.

   Исходники: [/Decompression/Mods/Fast](/Decompression/Mods/Fast)  
   Пример: [ATMega2560 + дисплей ILI9486 480x320](/Examples/AVR/Arduino_boards/Mega2560_ILI9486_Fast).

3) Скоростная с поддержкой пикселей:
	- ориентация на работу с переменными меньшей разрядности;
	- размер изображений до 255 x 255 пикселей;
	- до 8 бит на канал данных;
	- до 16 бит на кадр;
	- раскрашивание пикселей во время декомпрессии (если требуется);
	- прямое использование пиксельного буфера RGB565 (если необходимо);
	- поддержка доступа в "дальнюю" память AVR*;
	- разбиение сжатых данных на части (до 32767 байт)**;
	- высокая производительность+.

     Исходники: [/Decompression/Mods/Fast+pixel_support](/Decompression/Mods/Fast+pixel_support)  
     Примеры:  
     [Arduino Nano + дисплей ST7789 240x240](/Examples/AVR/Arduino_boards/Nano_ST7789),  
     [Плата ESP32 DEVKIT + дисплей ST7789 240x240](/Examples/ESP32/Wroom_ST7789),  
     [STM32F070CB (чип) + дисплей ST7789 240x240](/Examples/STM32/F070CB_ST7789_DMA),  
     [Плата DevEBox от mcudev + дисплей NT35510 800x480](/Examples/STM32/F407ZGT6_NT35510_FSMC_DMA).  

4) Миниатюрная адаптация:
	- ориентация на 8-битную архитектуру;
	- размер изображений до 255 x 255 пикселей;
	- до 8 бит на канал данных;
	- до 8 бит на кадр;
	- раскрашивание пикселей во время декомпрессии (если требуется);
	- прямое использование пиксельного буфера RGB565 (если необходимо);
	- высокая производительность++.

     Исходники: [/Decompression/Mods/Tiny+pixel_support](/Decompression/Mods/Tiny+pixel_support)  
     Примеры:  
     [Arduino Nano + дисплей ST7789 240x240](/Examples/AVR/Arduino_boards/Nano_ST7789_Tiny),  
     [ATtiny85 (чип) + вывод по SPI](/Examples/AVR/ATtiny85).  

\* Относится только к платформе AVR.  
** Чтобы это сделать, надо вместо указателя на массив данных передать в процедуру декомпрессии указатель на массив указателей на "дальние" данные:

```C
static uint_farptr_t dataChunks[n];
decompressNextFrames(dc, dataChunks, decompressedData, framesCount);
```

<h1>Скачать</h1>

***На случай если у вас не установлен .NET 8, я подготовил [self-contained версии приложения](https://drive.google.com/drive/folders/18RQaH1zRoYLzu4I6Uneg_3HMOnUn0RrV) для x86 и x64.***

<h1>Ссылки</h1>

Файлы проекта - [https://drive.google.com/drive/folders/1WRB-s4aPv4rQhnDVdqntXX3w0xl3WAg_](https://drive.google.com/drive/folders/1WRB-s4aPv4rQhnDVdqntXX3w0xl3WAg_)

Сжатие данных - [https://ru.wikipedia.org/wiki/Сжатие_данных](https://ru.wikipedia.org/wiki/%D0%A1%D0%B6%D0%B0%D1%82%D0%B8%D0%B5_%D0%B4%D0%B0%D0%BD%D0%BD%D1%8B%D1%85)

Энтропийное сжатие - [https://ru.wikipedia.org/wiki/Энтропийное_кодирование](https://ru.wikipedia.org/wiki/%D0%AD%D0%BD%D1%82%D1%80%D0%BE%D0%BF%D0%B8%D0%B9%D0%BD%D0%BE%D0%B5_%D0%BA%D0%BE%D0%B4%D0%B8%D1%80%D0%BE%D0%B2%D0%B0%D0%BD%D0%B8%D0%B5)

Дельта-кодирование - [https://ru.wikipedia.org/wiki/Дельта-кодирование](https://ru.wikipedia.org/wiki/%D0%94%D0%B5%D0%BB%D1%8C%D1%82%D0%B0-%D0%BA%D0%BE%D0%B4%D0%B8%D1%80%D0%BE%D0%B2%D0%B0%D0%BD%D0%B8%D0%B5)

Список форматов пикселя - [https://en.wikipedia.org/wiki/List_of_monochrome_and_RGB_color_formats](https://en.wikipedia.org/wiki/List_of_monochrome_and_RGB_color_formats)

Барышни - https://www.freepik.com/free-photo/woman-with-beautiful-body_9828277.htm
https://www.pexels.com/photo/woman-wearing-black-top-1642161/
https://www.freepik.com/free-photo/hispanic-woman-wearing-lingerie-smiling-doing-phone-gesture-with-hand-fingers-like-talking-telephone-communicating-concepts_53265959.htm