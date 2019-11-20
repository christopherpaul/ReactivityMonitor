// RxProfiler.cpp : Implementation of CRxProfiler

#include "pch.h"
#include "RxProfiler.h"
#include "dllmain.h"

struct MethodCallInfo
{
    std::wstring name;
    simplespan<const COR_SIGNATURE> sigBlob;
};

static const wchar_t * GetSupportAssemblyName();
static std::wstring GetSupportAssemblyPath();
static MethodCallInfo GetMethodCallInfo(mdToken method, const CMetadataImport& metadata);

// CRxProfiler

CRxProfiler::CRxProfiler() : m_supportAssemblyPath(GetSupportAssemblyPath())
{
}

HRESULT CRxProfiler::Initialize(
    /* [in] */ IUnknown* pICorProfilerInfoUnk)
{
    return HandleExceptions([=] {
        RELTRACE("Initialize");
        ATLTRACE(L"Support assembly path: %s", m_supportAssemblyPath.c_str());

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
    });
}

HRESULT CRxProfiler::ModuleLoadFinished(
    /* [in] */ ModuleID moduleId,
    /* [in] */ HRESULT hrStatus)
{
    return HandleExceptions([=] {
        ModuleInfo moduleInfo = m_profilerInfo.GetModuleInfo(moduleId);
        ATLTRACE(L"ModuleLoadFinished (%x): %s", hrStatus, moduleInfo.name.c_str());

        if (!IsExcludedAssembly(moduleId) && 
            ReferencesObservableInterfaces(moduleId))
        {
            ATLTRACE(L"Adding support assembly reference to %s", moduleInfo.name.c_str());
            AddSupportAssemblyReference(moduleId);
            m_moduleInfoMap.try_add(moduleId, true);
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
        bool supportAssemblyReferenced;
        if (!m_moduleInfoMap.try_get(info.moduleId, supportAssemblyReferenced) ||
            !supportAssemblyReferenced)
        {
            return;
        }

        CMetadataImport metadataImport = m_profilerInfo.GetMetadataImport(info.moduleId, ofRead);
        MethodProps props = metadataImport.GetMethodProps(info.functionToken);

        ATLTRACE(L"JITCompilationStarted for %s", props.name.c_str());

        InstrumentMethodBody(props.name, info, metadataImport);
    });
}

bool CRxProfiler::IsExcludedAssembly(ModuleID moduleId)
{
    CMetadataAssemblyImport mai = m_profilerInfo.GetMetadataAssemblyImport(moduleId, ofRead);
    AssemblyProps assemblyProps = mai.GetAssemblyProps();

    if (lstrcmpi(assemblyProps.name.c_str(), L"mscorlib") == 0)
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

bool CRxProfiler::ReferencesObservableInterfaces(ModuleID moduleId)
{
    CMetadataImport metadataImport = m_profilerInfo.GetMetadataImport(moduleId, ofRead);
    CMetadataAssemblyImport metadataAssemblyImport = m_profilerInfo.GetMetadataAssemblyImport(moduleId, ofRead);
    mdTypeRef observableRef;
    for (auto assemblyEnum = metadataAssemblyImport.EnumAssemblyRefs(); assemblyEnum.MoveNext(); )
    {
        if (metadataImport.TryFindTypeRef(assemblyEnum.Current(), L"System.IObservable`1", observableRef))
        {
            return true;
        }
    }

    return false;
}

void CRxProfiler::AddSupportAssemblyReference(ModuleID moduleId)
{
    CMetadataAssemblyEmit assemblyEmit = m_profilerInfo.GetMetadataAssemblyEmit(moduleId, ofReadWriteMask);

    ASSEMBLYMETADATA metadata = {};
    metadata.usMajorVersion = 1;
    metadata.usMinorVersion = 0;
    metadata.usBuildNumber = 0;
    metadata.usRevisionNumber = 0;

    assemblyEmit.DefineAssemblyRef({}, GetSupportAssemblyName(), metadata, {});
}

void CRxProfiler::InstrumentMethodBody(const std::wstring& name, const FunctionInfo& info, const CMetadataImport& metadata)
{
    simplespan<const byte> ilCode = m_profilerInfo.GetILFunctionBody(info.moduleId, info.functionToken);
    if (!ilCode)
    {
        ATLTRACE(L"%s is not an IL function", name.c_str());
        return;
    }

    ATLTRACE(L"%s has %d bytes of IL", name.c_str(), ilCode.length());
    const byte* codeBytes = ilCode.begin();
    const IMAGE_COR_ILMETHOD* pMethodImage = reinterpret_cast<const IMAGE_COR_ILMETHOD*>(codeBytes);

    Instrumentation::Method method(pMethodImage);

    for (auto it = method.m_instructions.begin(); it < method.m_instructions.end(); it++)
    {
        auto pInstr = *it;
        auto operation = pInstr->m_operation;
        if (operation == CEE_CALL || operation == CEE_CALLVIRT)
        {
            mdToken method = static_cast<mdToken>(pInstr->m_operand);

            MethodCallInfo methodCallInfo = GetMethodCallInfo(method, metadata);

            ATLTRACE(L"%s calls %s", name.c_str(), methodCallInfo.name.c_str());
        }
    }
}

const wchar_t * GetSupportAssemblyName()
{
    return L"ReactivityProfiler.Support";
}

std::wstring GetSupportAssemblyPath()
{
    std::vector<wchar_t> buffer(100);
    HRESULT hr;
    while ((hr = GetModuleFileName(g_profilerModule, buffer.data(), static_cast<DWORD>(buffer.size()))) == ERROR_INSUFFICIENT_BUFFER)
    {
        buffer = std::vector<wchar_t>(buffer.size() * 2);
    }

    std::wstring thisDllPath(buffer.data());

    return thisDllPath.substr(0, thisDllPath.find_last_of(L'\\') + 1) + GetSupportAssemblyName() + L".dll";
}

MethodCallInfo GetMethodCallInfo(mdToken method, const CMetadataImport& metadata)
{
    auto tokenType = TypeFromToken(method);
    mdToken methodDefOrRef;
    simplespan<const COR_SIGNATURE> sigBlob;
    if (tokenType == mdtMethodSpec)
    {
        // Generic
        MethodSpecProps specProps = metadata.GetMethodSpecProps(method);
        methodDefOrRef = specProps.genericMethodToken;
        sigBlob = specProps.sigBlob;
    }
    else
    {
        methodDefOrRef = method;
    }

    std::wstring methodName;
    switch (TypeFromToken(methodDefOrRef))
    {
    case mdtMethodDef:
    {
        auto defProps = metadata.GetMethodProps(methodDefOrRef);
        methodName = defProps.name;
        if (tokenType == mdtMethodDef)
        {
            sigBlob = defProps.sigBlob;
        }
    }
    break;
    case mdtMemberRef:
    {
        auto refProps = metadata.GetMemberRefProps(methodDefOrRef);
        methodName = refProps.name;
        if (tokenType == mdtMemberRef)
        {
            sigBlob = refProps.sigBlob;
        }
    }
    break;

    default:
        // Unexpected - ignore
        ATLTRACE(L"Unexpected token type in CALL(VIRT). Token: %x - ignoring instruction", methodDefOrRef);
        return {};
    }

    return { methodName, sigBlob };
}
