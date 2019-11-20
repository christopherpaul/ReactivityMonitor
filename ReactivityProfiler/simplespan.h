#pragma once

template <typename T>
class simplespan
{
public:
    simplespan() : this(nullptr, nullptr)
    {
    }

    simplespan(T* pStart, T* pEnd) : m_pStart(pStart), m_pEnd(pEnd)
    {
    }

    simplespan(T* pStart, size_t count) : this(pStart, pStart + count)
    {
    }

    T* begin()
    {
        return m_pStart;
    }

    T* end()
    {
        return m_pEnd;
    }

    T& operator[](size_t index)
    {
        return m_pStart[index];
    }

private:
    T* m_pStart;
    T* m_pEnd;
};