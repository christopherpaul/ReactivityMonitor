#pragma once

typedef simplespan<const COR_SIGNATURE> SignatureBlob;

template<typename TRange>
std::string FormatBytes(const TRange& bytes)
{
    std::stringstream ss;
    for (auto it = bytes.begin(); it != bytes.end(); it++)
    {
        byte b = *it;
        ss << std::hex << std::setfill('0') << std::setw(2) << (int)b << ' ';
    }
    return ss.str();
}