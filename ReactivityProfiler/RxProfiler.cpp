// RxProfiler.cpp : Implementation of CRxProfiler

#include "pch.h"
#include "RxProfiler.h"
#include "RxProfilerImpl.h"

#include "Signature.h"
#include "Store.h"

static bool IsSystemAssembly(const AssemblyProps& assemblyProps);
static bool IsMscorlib(const AssemblyProps& assemblyProps);
static bool IsSupportAssembly(const AssemblyProps& assemblyProps);

// CRxProfiler

CRxProfiler::CRxProfiler() : m_supportAssemblyFolder(GetSupportAssemblyFolderPath()),
    m_supportAssemblyModuleId(0)
{
}

HRESULT CRxProfiler::Initialize(
    /* [in] */ IUnknown* pICorProfilerInfoUnk)
{
    return HandleExceptions([=] {
        DoInitialize(pICorProfilerInfoUnk);
    });
}

HRESULT __stdcall CRxProfiler::InitializeForAttach(IUnknown* pICorProfilerInfoUnk, void* pvClientData, UINT cbClientData)
{
    return HandleExceptions([=] {
        ATLTRACE(L"InitializeForAttach: %d bytes of client data", cbClientData);

        std::string data(static_cast<char*>(pvClientData), cbClientData);
        ATLTRACE("InitializeForAttach: clientData: %s", FormatBytes(data).c_str());
        std::istringstream input(data);

        for (int32_t nameLen; !input.read(reinterpret_cast<char*>(&nameLen), sizeof(int32_t)).eof(); )
        {
            std::vector<wchar_t> nameBuf(nameLen);
            if (input.read(reinterpret_cast<char*>(nameBuf.data()), nameLen * sizeof(wchar_t)).eof())
                throw std::logic_error("Unexpected end of client data reading variable name");

            std::wstring name(nameBuf.data(), nameLen);

            int32_t valueLen;
            if (input.read(reinterpret_cast<char*>(&valueLen), sizeof(int32_t)).eof())
                throw std::logic_error("Unexpected end of client data reading variable value length");

            std::vector<wchar_t> valueBuf(valueLen);
            if (input.read(reinterpret_cast<char*>(valueBuf.data()), valueLen * sizeof(wchar_t)).eof())
                throw std::logic_error("Unexpected end of client data reading variable value");

            std::wstring value(valueBuf.data(), valueLen);
            ATLTRACE(L"InitializeForAttach: %s=%s", name.c_str(), value.c_str());

            if (!SetEnvironmentVariableW(name.c_str(), value.c_str()))
                throw std::logic_error("Failed to set environment variable");
        }

        DoInitialize(pICorProfilerInfoUnk);
    });
}

void CRxProfiler::DoInitialize(IUnknown* pICorProfilerInfoUnk)
{
    RELTRACE("Initializing RxProfiler");
    ATLTRACE(L"Support assembly path: %s", m_supportAssemblyFolder.c_str());

    m_profilerInfo.Set(pICorProfilerInfoUnk);

    m_runtimeInfo = m_profilerInfo.GetRuntimeInfo();
    RELTRACE(L"Runtime info: %s version %s", m_runtimeInfo.isCore ? L"CoreCLR" : L"CLR", m_runtimeInfo.versionString.c_str());

    m_hostInteraction = std::make_unique<HostInteraction>(m_runtimeInfo.versionString);

    m_profilerInfo.SetEventMask(
        COR_PRF_MONITOR_MODULE_LOADS |
        COR_PRF_MONITOR_JIT_COMPILATION,
        COR_PRF_HIGH_ADD_ASSEMBLY_REFERENCES
    );
}

HRESULT __stdcall CRxProfiler::ProfilerAttachComplete()
{
    return HandleExceptions([&] {
        ATLTRACE("ProfilerAttachComplete");

        m_profilerInfo.ForEachModule([&](ModuleID moduleId) {
            ModuleLoadFinished(moduleId, S_OK);
            return true;
        });

        auto supportAssemblyPath = GetSupportAssemblyFolderPath() + L"\\" + GetSupportAssemblyName() + L".dll";
        m_hostInteraction->ExecuteInDefaultAppDomain(supportAssemblyPath, L"ProfilerStartupHook", L"Initialize", L"");
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

        if (IsMscorlib(pPerModuleData->m_assemblyProps) && !m_runtimeInfo.isCore)
        {
            InstallAssemblyResolutionHandler(moduleId);
        }

        if (IsSupportAssembly(pPerModuleData->m_assemblyProps))
        {
            m_supportAssemblyModuleId = moduleId;
            ATLTRACE(L"Support assembly loaded");
        }
        else if (!IsSystemAssembly(pPerModuleData->m_assemblyProps))
        {
            pPerModuleData->m_referencesObservableTypes = ReferencesObservableInterfaces(moduleId, pPerModuleData->m_observableTypeRefs);

            // If the support assembly isn't loaded yet, add it as a reference even if
            // we're not going to be instrumenting any methods in this module, so that
            // we can get it loaded and activate the server as soon as possible.
            if (pPerModuleData->m_referencesObservableTypes || !m_supportAssemblyModuleId)
            {
                ATLTRACE(L"Adding support assembly reference to %s", moduleInfo.name.c_str());
                AddSupportAssemblyReference(moduleId, *pPerModuleData);
                pPerModuleData->m_supportAssemblyReferenced = true;
            }

            // But we don't need to report modules with no Rx involvement
            if (pPerModuleData->m_referencesObservableTypes)
            {
                g_Store.AddModuleInfo(moduleId, moduleInfo.name, pPerModuleData->m_assemblyProps.name);
            }
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
            !pPerModuleData->m_referencesObservableTypes)
        {
            return;
        }

        CMetadataImport metadataImport = m_profilerInfo.GetMetadataImport(info.moduleId, ofRead);
        MethodProps props = metadataImport.GetMethodProps(info.functionToken);

        ATLTRACE(L"JITCompilationStarted for %s (function ID %p, token %x)", props.name.c_str(), functionId, info.functionToken);

        InstrumentMethodBody(functionId, props, info, metadataImport, pPerModuleData);
    });
}

bool IsSystemAssembly(const AssemblyProps& assemblyProps)
{
    if (IsMscorlib(assemblyProps))
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

bool IsSupportAssembly(const AssemblyProps& assemblyProps)
{
    if (lstrcmpi(assemblyProps.name.c_str(), GetSupportAssemblyName()) == 0)
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
