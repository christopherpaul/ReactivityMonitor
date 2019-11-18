#pragma once

using namespace ATL;

struct ModuleInfo
{
    LPCBYTE baseLoadAddress;
    AssemblyID assemblyId;
    std::wstring name;
};

class CProfilerInfo
{
public:
    CProfilerInfo() {}

    void Set(IUnknown* profilerInfo);

    bool IsValid()
    {
        return m_profilerInfo;
    }

    HRESULT SetEventMask(COR_PRF_MONITOR dwEventsLow, COR_PRF_HIGH_MONITOR dwEventsHigh = COR_PRF_HIGH_MONITOR_NONE)
    {
        return m_profilerInfo->SetEventMask2(dwEventsLow, dwEventsHigh);
    }

    HRESULT GetModuleInfo(ModuleID moduleId, ModuleInfo& info);

private:
    CComQIPtr<ICorProfilerInfo5> m_profilerInfo;
};
