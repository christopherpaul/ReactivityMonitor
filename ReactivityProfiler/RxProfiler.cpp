// RxProfiler.cpp : Implementation of CRxProfiler

#include "pch.h"
#include "RxProfiler.h"


// CRxProfiler

HRESULT CRxProfiler::Initialize(
    /* [in] */ IUnknown* pICorProfilerInfoUnk)
{
    return HandleExceptions([&] {
        RELTRACE("Initialize");

        m_profilerInfo.Set(pICorProfilerInfoUnk);

        m_profilerInfo.SetEventMask(
            COR_PRF_MONITOR_MODULE_LOADS
        );
    });
}

HRESULT CRxProfiler::Shutdown()
{
    return HandleExceptions([&] {
        RELTRACE("Shutdown");
    });
}

HRESULT CRxProfiler::ModuleLoadFinished(
    /* [in] */ ModuleID moduleId,
    /* [in] */ HRESULT hrStatus)
{
    return HandleExceptions([&] {
        ModuleInfo moduleInfo = m_profilerInfo.GetModuleInfo(moduleId);
        RELTRACE(L"ModuleLoadFinished (%x): %s", hrStatus, moduleInfo.name.c_str());

        CMetadataImport metadataImport = m_profilerInfo.GetMetadataImport(moduleId, ofRead);
        mdTypeRef observableRef;
        for (auto moduleEnum = metadataImport.EnumModuleRefs(); moduleEnum.MoveNext(); )
        {
            if (metadataImport.TryFindTypeRef(moduleEnum.Current(), L"System.ObservableExtensions", observableRef))
            {
                RELTRACE(L"Found ref to ObservableExtensions!");
            }
        }
    });
}
