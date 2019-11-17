// RxProfiler.cpp : Implementation of CRxProfiler

#include "pch.h"
#include "RxProfiler.h"


// CRxProfiler

HRESULT CRxProfiler::Initialize(
    /* [in] */ IUnknown* pICorProfilerInfoUnk)
{
    RELTRACE("Initialize");
    return S_OK;
}

HRESULT CRxProfiler::Shutdown()
{
    RELTRACE("Shutdown");
    return S_OK;
}