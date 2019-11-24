#include "pch.h"
#include "Store.h"

Store g_Store;

enum class EventId
{
    ModuleInfo,
    InstrumentationInfo
};

class EventRecord
{
public:
    EventRecord(EventId eventId)
    {
        Write32(static_cast<uint32_t>(eventId));
    }

    void Write64(uint64_t value)
    {
        m_buffer.write(reinterpret_cast<byte*>(&value), sizeof value);
    }

    void Write32(uint32_t value)
    {
        m_buffer.write(reinterpret_cast<byte*>(&value), sizeof value);
    }

    void Write32(int32_t value)
    {
        m_buffer.write(reinterpret_cast<byte*>(&value), sizeof value);
    }

    void Write(const std::wstring& s)
    {
        Write64(s.length());
        m_buffer.write(reinterpret_cast<const byte*>(s.data()), s.length() * sizeof(wchar_t));
    }

    void AppendToVector(std::vector<byte>& vec) const
    {
        std::basic_string<byte> str = m_buffer.str();
        uint64_t contentLen = str.length();

        size_t offset = vec.size();
        size_t recordLen = (sizeof contentLen) + contentLen;
        vec.resize(offset + recordLen);

        byte* pContentLen = reinterpret_cast<byte*>(&contentLen);
        std::copy(pContentLen, pContentLen + (sizeof contentLen), vec.begin() + offset);
        std::copy(str.data(), str.data() + contentLen, vec.begin() + offset + (sizeof contentLen));
    }

private:
    std::basic_stringstream<byte> m_buffer;
};

class StoreImpl
{
public:
    void AddModuleInfo(ModuleID moduleId, const std::wstring& modulePath);
    void AddInstrumentationInfo(
        int32_t instrumentationPoint,
        ModuleID moduleId,
        mdToken functionToken,
        int32_t instructionOffset,
        const std::wstring& calledMethodName);

    int32_t GetStoreLength()
    {
        std::lock_guard lockBuffer(m_mutex);
        return static_cast<int32_t>(m_buffer.size());
    }

    int32_t ReadStore(int32_t start, int32_t length, byte* buffer)
    {
        ATLTRACE("ReadStore(%lu, %lu, <buf>)", start, length);
        std::lock_guard lockBuffer(m_mutex);
        if (start >= m_buffer.size())
        {
            return 0;
        }

        size_t size = min(length, m_buffer.size() - start);

        std::copy(m_buffer.begin() + start, m_buffer.begin() + start + size, buffer);

        return static_cast<int32_t>(size);
    }

private:
    void WriteRecord(const EventRecord& rec)
    {
        std::lock_guard lockBuffer(m_mutex);
        rec.AppendToVector(m_buffer);
    }

    std::mutex m_mutex;
    std::vector<byte> m_buffer;
};

Store::Store() :
    m_pImpl(std::make_unique<StoreImpl>())
{
}

void Store::AddModuleInfo(ModuleID moduleId, const std::wstring& modulePath)
{
    m_pImpl->AddModuleInfo(moduleId, modulePath);
}

void Store::AddInstrumentationInfo(int32_t instrumentationPoint, ModuleID moduleId, mdToken functionToken, int32_t instructionOffset, const std::wstring& calledMethodName)
{
    m_pImpl->AddInstrumentationInfo(
        instrumentationPoint,
        moduleId,
        functionToken,
        instructionOffset,
        calledMethodName);
}

int32_t Store::GetStoreLength()
{
    return m_pImpl->GetStoreLength();
}

int32_t Store::ReadStore(int32_t start, int32_t length, byte* buffer)
{
    return m_pImpl->ReadStore(start, length, buffer);
}

void StoreImpl::AddModuleInfo(ModuleID moduleId, const std::wstring& modulePath)
{
    EventRecord r(EventId::ModuleInfo);
    r.Write64(moduleId);
    r.Write(modulePath);
    WriteRecord(r);
}

void StoreImpl::AddInstrumentationInfo(int32_t instrumentationPoint, ModuleID moduleId, mdToken functionToken, int32_t instructionOffset, const std::wstring& calledMethodName)
{
    EventRecord r(EventId::InstrumentationInfo);
    r.Write32(instrumentationPoint);
    r.Write64(moduleId);
    r.Write32(functionToken);
    r.Write32(instructionOffset);
    r.Write(calledMethodName);
    WriteRecord(r);
}
