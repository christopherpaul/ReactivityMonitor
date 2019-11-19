#pragma once

template <typename Key, typename Value>
class concurrent_map
{
private:
    typedef const std::lock_guard<std::mutex> lock_guard;
public:
    bool try_get(Key key, Value& value)
    {
        lock_guard lock(m_mutex);

        auto location = m_map.find(key);
        if (location == m_map.end())
        {
            return false;
        }

        value = location->second;
        return true;
    }

    bool try_add(Key key, const Value& value)
    {
        lock_guard lock(m_mutex);

        bool inserted = m_map.insert({ key, value }).second;
        return inserted;
    }

private:
    std::mutex m_mutex;
    std::unordered_map<Key, Value> m_map;
};