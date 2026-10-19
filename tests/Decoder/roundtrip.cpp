// Round-trip helper: decodes a compressed stream with the reference microcontroller
// decoder (Decompression/Decompression.cpp) compiled for the PC, and writes every
// decoded frame as a little-endian uint64 (channel 0 in the most significant bits).
//
// Usage: roundtrip <compressed.bin> <frames> <blockSizeBits> <bitsPerChannel,...> <bitsPerMethodDeclaration,...> <out.bin>

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <vector>
#include "Decompression.h"

static FILE * output = nullptr;

void frameDecompressed(uint64_t frame) {
	fwrite(&frame, sizeof(frame), 1, output);
}

static int parseList(const char * text, uint8_t * values) {
	int count = 0;
	for (const char * p = text; *p; ) {
		values[count++] = (uint8_t)strtoul(p, (char **)&p, 10);
		if (*p == ',') p++;
	}
	return count;
}

int main(int argc, char ** argv) {
	if (argc != 7) {
		fprintf(stderr, "usage: %s <compressed.bin> <frames> <blockSizeBits> <bpc,...> <bpmd,...> <out.bin>\n", argv[0]);
		return 2;
	}

	FILE * input = fopen(argv[1], "rb");
	if (!input) { perror(argv[1]); return 1; }
	std::vector<uint8_t> data;
	int ch;
	while ((ch = fgetc(input)) != EOF) data.push_back((uint8_t)ch);
	fclose(input);
	data.resize(data.size() + 16, 0); // the decoder may read a few bytes ahead

	DecompressionContext dc;
	memset(&dc, 0, sizeof(dc));
	dc.numberOfChannels = (uint8_t)parseList(argv[4], dc.bitsPerChannels);
	if (parseList(argv[5], dc.bitsPerMethodDeclaration) != dc.numberOfChannels) { fprintf(stderr, "channel count mismatch\n"); return 2; }
	dc.blockSizeBits = (uint8_t)atoi(argv[3]);
	dc.bitsPerFrame = 0;
	for (int c = 0; c < dc.numberOfChannels; c++) dc.bitsPerFrame += dc.bitsPerChannels[c];

	output = fopen(argv[6], "wb");
	if (!output) { perror(argv[6]); return 1; }

	Decompression decoder;
	decoder.resetDecompression();
	decoder.bufferWidth = 0;
	// A null output buffer makes the decoder hand every frame to frameDecompressed() in stream order.
	decoder.decompressNextFrames(&dc, data.data(), nullptr, (uint32_t)strtoul(argv[2], nullptr, 10));

	fclose(output);
	return 0;
}
