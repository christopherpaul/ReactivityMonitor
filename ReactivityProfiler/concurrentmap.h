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

    Value add_or_get(Key key, std::function<Value()> valueFactory)
    {
        lock_guard lock(m_mutex);

        auto location = m_map.find(key);
        if (location == m_map.end())
        {
            Value value = valueFactory();
            m_map.insert({ key, value });
            return value;
        }

        return location->second;
    }

private:
    std::mutex m_mutex;
    std::unordered_map<Key, Value> m_map;
};