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

    simplespan(const std::vector<std::remove_const_t<T>>& vec) : simplespan(vec.data(), vec.size())
    {
    }

    simplespan(std::vector<std::remove_const_t<T>>& vec) : simplespan(vec.data(), vec.size())
    {
    }

    T* begin() const
    {
        return m_pStart;
    }

    T* end() const
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

    T& operator[](size_t index) const
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