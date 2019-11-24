#include "pch.h"
#include "Store.h"

STDAPI_(int32_t) GetStoreLength()
{
    return g_Store.GetStoreLength();
}

STDAPI_(int32_t) ReadStore(int32_t start, byte* buffer, int32_t length)
{
    return g_Store.ReadStore(start, length, buffer);
}
