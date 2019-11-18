#include "pch.h"
#include "ProfilerInfo.h"

void CProfilerInfo::Set(IUnknown* profilerInfo)
{
    m_profilerInfo = profilerInfo;
    if (profilerInfo && !m_profilerInfo)
    {
        RELTRACE("Required profiling interface not available");
        throw E_FAIL;
    }
}

ModuleInfo CProfilerInfo::GetModuleInfo(ModuleID moduleId)
{
    ModuleInfo info;
    ULONG nameCount;
    CHECK_SUCCESS(m_profilerInfo->GetModuleInfo(moduleId, &info.baseLoadAddress, 0, &nameCount, nullptr, &info.assemblyId));
    std::vector<WCHAR> nameChars(nameCount);
    CHECK_SUCCESS(m_profilerInfo->GetModuleInfo(moduleId, &info.baseLoadAddress, nameCount, &nameCount, nameChars.data(), &info.assemblyId));
    info.name = std::wstring(nameChars.data(), nameCount);

    return info;
}

CMetadataImport CProfilerInfo::GetMetadataImport(ModuleID moduleId, CorOpenFlags openFlags)
{
    IUnknown* metadataImport;
    CHECK_SUCCESS(m_profilerInfo->GetModuleMetaData(moduleId, openFlags, IID_IMetaDataImport2, &metadataImport));

    return CMetadataImport(metadataImport);
}

CCorEnum<mdModule> CMetadataImport::EnumModuleRefs()
{
    CCorEnum<mdModule> e(m_metadata.p, [=](auto imp, auto e, auto arr, auto c, auto pc) { return imp->EnumModuleRefs(e, arr, c, pc); });
    return e;
}

bool CMetadataImport::TryFindTypeRef(mdToken scope, const std::wstring& name, mdTypeRef& typeRef)
{
    return SUCCEEDED(m_metadata->FindTypeRef(scope, name.c_str(), &typeRef));
}
