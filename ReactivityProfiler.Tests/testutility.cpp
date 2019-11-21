#include "pch.h"
#include "testutility.h"

std::ostream& operator<< (std::ostream& stm, const std::vector<COR_SIGNATURE>& vec)
{
    stm << '[';
    bool delim = false;
    for (auto x : vec)
    {
        if (delim)
        {
            stm << ' ';
        }
        stm << (unsigned)x;
        delim = true;
    }
    stm << ']';
    return stm;
}
