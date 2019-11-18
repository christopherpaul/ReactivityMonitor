// RxProfiler.cpp : Implementation of CRxProfiler

#include "pch.h"
#include "RxProfiler.h"


// CRxProfiler

HRESULT CRxProfiler::Initialize(
    /* [in] */ IUnknown* pICorProfilerInfoUnk)
{
    RELTRACE("Initialize");

    m_profilerInfo.Set(pICorProfilerInfoUnk);
    if (!m_profilerInfo.IsValid())
    {
        RELTRACE("Required profiling interface not available");
        return E_FAIL;
    }

    m_profilerInfo.SetEventMask(
        COR_PRF_MONITOR_MODULE_LOADS,
        COR_PRF_HIGH_MONITOR_NONE
    );

    return S_OK;
}

HRESULT CRxProfiler::Shutdown()
{
    RELTRACE("Shutdown");
    return S_OK;
}

HRESULT CRxProfiler::ModuleLoadFinished(
    /* [in] */ ModuleID moduleId,
    /* [in] */ HRESULT hrStatus)
{
    ModuleInfo moduleInfo;
    m_profilerInfo.GetModuleInfo(moduleId, moduleInfo);

    RELTRACE(L"ModuleLoadFinished (%x): %s", hrStatus, moduleInfo.name.c_str());
    return S_OK;
}
