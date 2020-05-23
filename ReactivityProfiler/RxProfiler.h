// RxProfiler.h : Declaration of the CRxProfiler

#pragma once
#include "resource.h"       // main symbols



#include "ReactivityProfiler_i.h"
#include "ProfileBase.h"
#include "ProfilerInfo.h"
#include "concurrentmap.h"
#include "Instrumentation/Method.h"
#include "HostInteraction.h"

using namespace ATL;


// CRxProfiler

struct PerModuleData;
struct ObservableTypeReferences;
struct SupportAssemblyReferences;

class ATL_NO_VTABLE CRxProfiler :
	public CComObjectRootEx<CComMultiThreadModel>,
	public CComCoClass<CRxProfiler, &CLSID_RxProfiler>,
	public CProfilerBase
{
public:
    CRxProfiler();

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
        m_profilerInfo.Set(nullptr);
	}

public:
    virtual HRESULT STDMETHODCALLTYPE Initialize(
        /* [in] */ IUnknown* pICorProfilerInfoUnk) override;

    virtual HRESULT STDMETHODCALLTYPE InitializeForAttach(
        /* [in] */ IUnknown* pICorProfilerInfoUnk,
        /* [in] */ void* pvClientData,
        /* [in] */ UINT cbClientData) override;

    virtual HRESULT STDMETHODCALLTYPE ProfilerAttachComplete() override;

    virtual HRESULT STDMETHODCALLTYPE Shutdown() override;

    virtual HRESULT STDMETHODCALLTYPE ModuleLoadFinished(
        /* [in] */ ModuleID moduleId,
        /* [in] */ HRESULT hrStatus) override;

    virtual HRESULT STDMETHODCALLTYPE GetAssemblyReferences(
        /* [string][in] */ const WCHAR* wszAssemblyPath,
        /* [in] */ ICorProfilerAssemblyReferenceProvider* pAsmRefProvider) override;

    virtual HRESULT STDMETHODCALLTYPE JITCompilationStarted(
        /* [in] */ FunctionID functionId,
        /* [in] */ BOOL fIsSafeToBlock) override;

private:
    CProfilerInfo m_profilerInfo;
    const std::wstring m_supportAssemblyFolder;
    concurrent_map<ModuleID, std::shared_ptr<PerModuleData>> m_moduleInfoMap;
    RuntimeInfo m_runtimeInfo;
    std::atomic<ModuleID> m_supportAssemblyModuleId;
    std::unique_ptr<HostInteraction> m_hostInteraction;

    void DoInitialize(IUnknown* pICorProfilerInfoUnk);
    void InstallAssemblyResolutionHandler(ModuleID mscorlibId);
    bool ReferencesObservableInterfaces(ModuleID moduleId, ObservableTypeReferences& typeRefs);
    void AddSupportAssemblyReference(ModuleID moduleId, PerModuleData& perModuleData);
    void InstrumentMethodBody(FunctionID functionId, const MethodProps& name, const FunctionInfo& info, CMetadataImport& metadata, std::shared_ptr<PerModuleData>& pPerModuleData);
};

OBJECT_ENTRY_AUTO(__uuidof(RxProfiler), CRxProfiler)
