#include "pch.h"
#include "DmtxWrapper.h"
#include "dmtx.h"
#include <cstring>

int DecodeDataMatrix(const unsigned char* image, int width, int height, int stride, char* result, int resultSize)
{
    if (image == nullptr || result == nullptr || width <= 0 || height <= 0 || stride <= 0 || resultSize <= 0)
        return -1;
    result[0] = '\0';
    DmtxImage* img = dmtxImageCreate(const_cast<unsigned char*>(image), width, height, DmtxPack8bppK);

    if (img == nullptr)
        return -2;

    DmtxDecode* dec = dmtxDecodeCreate(img, 1);

    if (dec == nullptr)
    {
        dmtxImageDestroy(&img);
        return -3;
    }

    DmtxRegion* reg = dmtxRegionFindNext(dec, nullptr);
    if (reg == nullptr)
    {
        dmtxDecodeDestroy(&dec);
        dmtxImageDestroy(&img);
        return 0;
    }

    DmtxMessage* msg = dmtxDecodeMatrixRegion(dec, reg, DmtxUndefined);

    if (msg == nullptr)
    {
        dmtxRegionDestroy(&reg);
        dmtxDecodeDestroy(&dec);
        dmtxImageDestroy(&img);
        return 0;
    }

    int copySize = msg->outputSize;
    if (copySize >= resultSize)
        copySize = resultSize - 1;

    memcpy(result, msg->output, copySize);
    result[copySize] = '\0';
    dmtxMessageDestroy(&msg);
    dmtxRegionDestroy(&reg);
    dmtxDecodeDestroy(&dec);
    dmtxImageDestroy(&img);

    return 1;
}