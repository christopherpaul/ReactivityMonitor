#pragma once

template <typename T>
class simplespan
{
public:
    simplespan() : simplespan(nullptr, nullptr)
    {
    }

    simplespan(T* pStart, T* pEnd) : m_pStart(pStart), m_pEnd(pEnd)
    {
    }

    simplespan(T* pStart, size_t count) : simplespan(pStart, pStart + count)
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

    size_t length() const
    {
        if (!m_pStart)
        {
            return 0;
        }
        return m_pEnd - m_pStart;
    }

    T& operator[](size_t index)
    {
        return m_pStart[index];
    }

    operator bool() const
    {
        return m_pStart;
    }

private:
    T* m_pStart;
    T* m_pEnd;
};