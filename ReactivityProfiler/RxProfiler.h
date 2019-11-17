// RxProfiler.h : Declaration of the CRxProfiler

#pragma once
#include "resource.h"       // main symbols



#include "ReactivityProfiler_i.h"
#include "ProfileBase.h"


using namespace ATL;


// CRxProfiler

class ATL_NO_VTABLE CRxProfiler :
	public CComObjectRootEx<CComMultiThreadModel>,
	public CComCoClass<CRxProfiler, &CLSID_RxProfiler>,
	public CProfilerBase
{
public:
	CRxProfiler()
	{
	}

DECLARE_REGISTRY_RESOURCEID(106)

DECLARE_NOT_AGGREGATABLE(CRxProfiler)

BEGIN_COM_MAP(CRxProfiler)
	COM_INTERFACE_ENTRY(ICorProfilerCallback)
	COM_INTERFACE_ENTRY(ICorProfilerCallback2)
	COM_INTERFACE_ENTRY(ICorProfilerCallback3)
	COM_INTERFACE_ENTRY(ICorProfilerCallback4)
	COM_INTERFACE_ENTRY(ICorProfilerCallback5)
	COM_INTERFACE_ENTRY(ICorProfilerCallback6)
	COM_INTERFACE_ENTRY(ICorProfilerCallback7)
	COM_INTERFACE_ENTRY(ICorProfilerCallback8)
	COM_INTERFACE_ENTRY(ICorProfilerCallback9)
END_COM_MAP()



	DECLARE_PROTECT_FINAL_CONSTRUCT()

	HRESULT FinalConstruct()
	{
		return S_OK;
	}

	void FinalRelease()
	{
        m_profilerInfo.Release();
	}

public:
    virtual HRESULT STDMETHODCALLTYPE Initialize(
        /* [in] */ IUnknown* pICorProfilerInfoUnk) override;

    virtual HRESULT STDMETHODCALLTYPE Shutdown() override;

private:
    CComQIPtr<ICorProfilerInfo> m_profilerInfo;
};

OBJECT_ENTRY_AUTO(__uuidof(RxProfiler), CRxProfiler)
