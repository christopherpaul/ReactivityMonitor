// RxProfiler.cpp : Implementation of CRxProfiler

#include "pch.h"
#include "RxProfiler.h"
#include "RxProfilerImpl.h"

#include "Signature.h"
#include "Store.h"

static bool IsExcludedAssembly(const AssemblyProps& assemblyProps);
static bool IsMscorlib(const AssemblyProps& assemblyProps);

// CRxProfiler

CRxProfiler::CRxProfiler() : m_supportAssemblyFolder(GetSupportAssemblyPath())
{
}

HRESULT CRxProfiler::Initialize(
    /* [in] */ IUnknown* pICorProfilerInfoUnk)
{
    return HandleExceptions([=] {
        RELTRACE("Initialize");
        ATLTRACE(L"Support assembly path: %s", m_supportAssemblyFolder.c_str());

        m_profilerInfo.Set(pICorProfilerInfoUnk);

        m_profilerInfo.SetEventMask(
            COR_PRF_MONITOR_MODULE_LOADS |
            COR_PRF_MONITOR_JIT_COMPILATION,
            COR_PRF_HIGH_ADD_ASSEMBLY_REFERENCES
        );
    });
}

HRESULT CRxProfiler::Shutdown()
{
    return HandleExceptions([] {
        RELTRACE("Shutdown");
        RemoveTransientRegistryKey();
    });
}

HRESULT CRxProfiler::ModuleLoadFinished(
    /* [in] */ ModuleID moduleId,
    /* [in] */ HRESULT hrStatus)
{
    return HandleExceptions([=] {
        ModuleInfo moduleInfo = m_profilerInfo.GetModuleInfo(moduleId);
        ATLTRACE(L"ModuleLoadFinished (%x): %s", hrStatus, moduleInfo.name.c_str());

        auto pPerModuleData = m_moduleInfoMap.add_or_get(moduleId, [] { return std::make_shared<PerModuleData>(); });
        CMetadataAssemblyImport mai = m_profilerInfo.GetMetadataAssemblyImport(moduleId, ofRead);

        std::lock_guard<std::mutex> lock_pmd(pPerModuleData->m_mutex);
        pPerModuleData->m_assemblyProps = mai.GetAssemblyProps();

        if (IsMscorlib(pPerModuleData->m_assemblyProps))
        {
            InstallAssemblyResolutionHandler(moduleId);
        }

        if (!IsExcludedAssembly(pPerModuleData->m_assemblyProps) &&
            ReferencesObservableInterfaces(moduleId, pPerModuleData->m_observableTypeRefs))
        {
            ATLTRACE(L"Adding support assembly reference to %s", moduleInfo.name.c_str());
            AddSupportAssemblyReference(moduleId, pPerModuleData->m_observableTypeRefs, pPerModuleData->m_supportAssemblyRefs);
            pPerModuleData->m_supportAssemblyReferenced = true;

            g_Store.AddModuleInfo(moduleId, moduleInfo.name);
        }
    });
}

HRESULT CRxProfiler::GetAssemblyReferences(
    const WCHAR* wszAssemblyPath, 
    ICorProfilerAssemblyReferenceProvider* pAsmRefProvider)
{
    return HandleExceptions([=] {
        std::wstring assemblyPath(wszAssemblyPath);
        std::wstring mscorlib(L"mscorlib.dll");
        if (lstrcmpi(assemblyPath.substr(assemblyPath.length() - mscorlib.length()).c_str(), mscorlib.c_str()) == 0)
        {
            ATLTRACE(L"GetAssemblyReferences: ignoring mscorlib");
            return;
        }

        ATLTRACE(L"GetAssemblyReferences: %s", assemblyPath.c_str());

        // Since this callback doesn't seem to get called, not much point writing any more code here...
    });
}

HRESULT CRxProfiler::JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock)
{
    return HandleExceptions([=] {
        FunctionInfo info = m_profilerInfo.GetFunctionInfo(functionId);
        std::shared_ptr<PerModuleData> pPerModuleData;
        if (!m_moduleInfoMap.try_get(info.moduleId, pPerModuleData) ||
            !pPerModuleData->m_supportAssemblyReferenced)
        {
            return;
        }

        CMetadataImport metadataImport = m_profilerInfo.GetMetadataImport(info.moduleId, ofRead);
        MethodProps props = metadataImport.GetMethodProps(info.functionToken);

        ATLTRACE(L"JITCompilationStarted for %s", props.name.c_str());

        InstrumentMethodBody(functionId, props, info, metadataImport, pPerModuleData);
    });
}

bool IsExcludedAssembly(const AssemblyProps& assemblyProps)
{
    if (IsMscorlib(assemblyProps))
    {
        return true;
    }

    if (lstrcmpi(assemblyProps.name.c_str(), GetSupportAssemblyName()) == 0)
    {
        return true;
    }

    std::wstring firstNsPart = assemblyProps.name.substr(0, assemblyProps.name.find_first_of(L'.'));
    if (lstrcmpi(firstNsPart.c_str(), L"System") == 0)
    {
        return true;
    }

    return false;
}

bool IsMscorlib(const AssemblyProps& assemblyProps)
{
    if (lstrcmpi(assemblyProps.name.c_str(), L"mscorlib") == 0)
    {
        return true;
    }

    return false;
}

bool CRxProfiler::ReferencesObservableInterfaces(ModuleID moduleId, ObservableTypeReferences& typeRefs)
{
    CMetadataImport metadataImport = m_profilerInfo.GetMetadataImport(moduleId, ofRead);
    CMetadataAssemblyImport metadataAssemblyImport = m_profilerInfo.GetMetadataAssemblyImport(moduleId, ofRead);
    mdTypeRef observableRef;
    for (auto assemblyEnum = metadataAssemblyImport.EnumAssemblyRefs(); assemblyEnum.MoveNext(); )
    {
        if (!typeRefs.m_IObservable && metadataImport.TryFindTypeRef(assemblyEnum.Current(), L"System.IObservable`1", observableRef))
        {
            typeRefs.m_IObservable = observableRef;
        }

        if (!typeRefs.m_IConnectableObservable && metadataImport.TryFindTypeRef(assemblyEnum.Current(), L"System.Reactive.Subjects.IConnectableObservable`1", observableRef))
        {
            typeRefs.m_IConnectableObservable = observableRef;
        }

        if (!typeRefs.m_IGroupedObservable && metadataImport.TryFindTypeRef(assemblyEnum.Current(), L"System.Reactive.Linq.IGroupedObservable`2", observableRef))
        {
            typeRefs.m_IGroupedObservable = observableRef;
        }

        if (typeRefs.m_IObservable && typeRefs.m_IConnectableObservable && typeRefs.m_IGroupedObservable)
        {
            break;
        }
    }

    return typeRefs.m_IObservable || typeRefs.m_IConnectableObservable || typeRefs.m_IGroupedObservable;
}
