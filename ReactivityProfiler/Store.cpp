#include "pch.h"
#include "Store.h"

Store g_Store;

enum class EventId
{
    ModuleInfo,
    MethodInfo,
    CallInfo,
    MethodInstrumentationDone,
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
        int length = static_cast<int>(s.length());
        Write32(length);
        m_buffer.write(reinterpret_cast<const byte*>(s.data()), static_cast<std::streamsize>(length) * sizeof(wchar_t));
    }

    std::vector<byte> Get() const
    {
        std::basic_string<byte> str = m_buffer.str();
        size_t contentLen = str.length();

        std::vector<byte> vec;
        vec.resize(contentLen);
        std::copy(str.begin(), str.end(), vec.begin());
        return std::move(vec);
    }

private:
    std::basic_stringstream<byte> m_buffer;
};

class StoreImpl
{
public:
    void AddModuleInfo(ModuleID moduleId, const std::wstring& modulePath, const std::wstring& assemblyName);

    void AddMethodInfo(
        int32_t instrumentedMethodId, 
        ModuleID moduleId, 
        mdToken functionToken, 
        const std::wstring& owningTypeName, 
        const std::wstring& name);

    void AddInstrumentationInfo(
        int32_t instrumentationPoint,
        int32_t instrumentedMethodId,
        int32_t instructionOffset,
        const std::wstring& calledMethodName);

    void MethodInstrumentationDone(int32_t instrumentedMethodId);

    int32_t GetEventCount()
    {
        std::lock_guard lockBuffer(m_mutex);
        return static_cast<int32_t>(m_buffer.size());
    }

    simplespan<byte> ReadEvent(int32_t index)
    {
        std::lock_guard lockBuffer(m_mutex);
        auto& vec = m_buffer[index];
        return vec;
    }

private:
    void WriteRecord(const EventRecord& rec)
    {
        std::lock_guard lockBuffer(m_mutex);
        m_buffer.push_back(rec.Get());
    }

    std::mutex m_mutex;
    std::vector<std::vector<byte>> m_buffer;
};

Store::Store() :
    m_pImpl(std::make_unique<StoreImpl>())
{
}

void Store::AddModuleInfo(ModuleID moduleId, const std::wstring& modulePath, const std::wstring& assemblyName)
{
    m_pImpl->AddModuleInfo(moduleId, modulePath, assemblyName);
}

void Store::AddMethodInfo(int32_t instrumentedMethodId, ModuleID moduleId, mdToken functionToken, const std::wstring& owningTypeName, const std::wstring& name)
{
    m_pImpl->AddMethodInfo(instrumentedMethodId, moduleId, functionToken, owningTypeName, name);
}

void Store::AddInstrumentationInfo(
    int32_t instrumentationPoint, 
    int32_t instrumentedMethodId,
    int32_t instructionOffset,
    const std::wstring& calledMethodName)
{
    m_pImpl->AddInstrumentationInfo(
        instrumentationPoint,
        instrumentedMethodId,
        instructionOffset,
        calledMethodName);
}

void Store::MethodInstrumentationDone(int32_t instrumentedMethodId)
{
    m_pImpl->MethodInstrumentationDone(instrumentedMethodId);
}

int32_t Store::GetEventCount()
{
    return m_pImpl->GetEventCount();
}

simplespan<byte> Store::ReadEvent(int32_t index)
{
    return m_pImpl->ReadEvent(index);
}


void StoreImpl::AddModuleInfo(ModuleID moduleId, const std::wstring& modulePath, const std::wstring& assemblyName)
{
    EventRecord r(EventId::ModuleInfo);
    r.Write64(moduleId);
    r.Write(modulePath);
    r.Write(assemblyName);
    WriteRecord(r);
}

void StoreImpl::AddMethodInfo(
    int32_t instrumentedMethodId, 
    ModuleID moduleId, 
    mdToken functionToken, 
    const std::wstring& owningTypeName, 
    const std::wstring& name)
{
    EventRecord r(EventId::MethodInfo);
    r.Write32(instrumentedMethodId);
    r.Write64(moduleId);
    r.Write32(functionToken);
    r.Write(owningTypeName);
    r.Write(name);
    WriteRecord(r);
}

void StoreImpl::AddInstrumentationInfo(
    int32_t instrumentationPoint, 
    int32_t instrumentedMethodId,
    int32_t instructionOffset,
    const std::wstring& calledMethodName)
{
    EventRecord r(EventId::CallInfo);
    r.Write32(instrumentationPoint);
    r.Write32(instrumentedMethodId);
    r.Write32(instructionOffset);
    r.Write(calledMethodName);
    WriteRecord(r);
}

void StoreImpl::MethodInstrumentationDone(int32_t instrumentedMethodId)
{
    EventRecord r(EventId::MethodInstrumentationDone);
    r.Write32(instrumentedMethodId);
    WriteRecord(r);
}
