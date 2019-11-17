// RxProfiler.cpp : Implementation of CRxProfiler

#include "pch.h"
#include "RxProfiler.h"


// CRxProfiler

HRESULT CRxProfiler::Initialize(
    /* [in] */ IUnknown* pICorProfilerInfoUnk)
{
    RELTRACE("Initialize");

    m_profilerInfo = pICorProfilerInfoUnk;
    if (!m_profilerInfo)
    {
        RELTRACE("Required profiling interface not available");
        return E_FAIL;
    }

    m_profilerInfo->SetEventMask2(
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
    RELTRACE("ModuleLoadFinished");
    return S_OK;
}
