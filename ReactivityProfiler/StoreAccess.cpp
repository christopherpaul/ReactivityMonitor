#include "pch.h"
#include "Store.h"

STDAPI_(int32_t) GetStoreEventCount()
{
    return g_Store.GetEventCount();
}

STDAPI_(void) ReadStoreEvent(int32_t index, byte** buffer, int32_t* size)
{
    simplespan<byte> eventData = g_Store.ReadEvent(index);
    *buffer = eventData.begin();
    *size = static_cast<int>(eventData.length());
}
