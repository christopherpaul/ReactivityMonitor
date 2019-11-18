#include "pch.h"
#include "ProfilerInfo.h"

void CProfilerInfo::Set(IUnknown* profilerInfo)
{
    m_profilerInfo = profilerInfo;
}

HRESULT CProfilerInfo::GetModuleInfo(ModuleID moduleId, ModuleInfo &info)
{
    ULONG nameCount;
    CHECK_SUCCESS(m_profilerInfo->GetModuleInfo(moduleId, &info.baseLoadAddress, 0, &nameCount, nullptr, &info.assemblyId));
    std::vector<WCHAR> nameChars(nameCount);
    CHECK_SUCCESS(m_profilerInfo->GetModuleInfo(moduleId, &info.baseLoadAddress, nameCount, &nameCount, nameChars.data(), &info.assemblyId));
    info.name = std::wstring(nameChars.data(), nameCount);

    return S_OK;
}