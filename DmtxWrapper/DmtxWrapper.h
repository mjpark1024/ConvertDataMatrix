#pragma once

#ifdef LIBDMTXWRAPPER_EXPORTS
#define DMTX_API __declspec(dllexport)
#else
#define DMTX_API __declspec(dllimport)
#endif

extern "C"
{
	DMTX_API int DecodeDataMatrix(
		const unsigned char* image,
		int width,
		int height,
		int stride,
		char* result,
		int resultSize
	);
}