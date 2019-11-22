// RxProfiler.cpp : Implementation of CRxProfiler

#include "pch.h"
#include "RxProfiler.h"
#include "dllmain.h"
#include "Signature.h"

struct MethodCallInfo
{
    std::wstring name;
    simplespan<const COR_SIGNATURE> sigBlob;
    simplespan<const COR_SIGNATURE> genericInstBlob;
};

struct ObservableTypeReferences
{
    mdTypeRef m_IObservable = 0;
};

struct SupportAssemblyReferences
{
    mdAssemblyRef m_AssemblyRef = 0;
    mdTypeRef m_Instrument = 0;
    mdMemberRef m_Returned = 0;
};

struct PerModuleData
{
    std::mutex m_mutex;

    bool m_supportAssemblyReferenced = false;
    AssemblyProps m_assemblyProps;
    ObservableTypeReferences m_observableTypeRefs;
    SupportAssemblyReferences m_supportAssemblyRefs;

    std::atomic_int32_t m_instrumentationPointSource = 0;
};

static const wchar_t * GetSupportAssemblyName();
static std::wstring GetSupportAssemblyPath();
static MethodCallInfo GetMethodCallInfo(mdToken method, const CMetadataImport& metadata);
static bool IsExcludedAssembly(const AssemblyProps& assemblyProps);

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

        auto pPerModuleData = m_moduleInfoMap.add_or_get(moduleId, [] { return std::make_shared<PerModuleData>(); });
        CMetadataAssemblyImport mai = m_profilerInfo.GetMetadataAssemblyImport(moduleId, ofRead);

        std::lock_guard<std::mutex> lock_pmd(pPerModuleData->m_mutex);
        pPerModuleData->m_assemblyProps = mai.GetAssemblyProps();

        if (!IsExcludedAssembly(pPerModuleData->m_assemblyProps) &&
            ReferencesObservableInterfaces(moduleId, pPerModuleData->m_observableTypeRefs))
        {
            ATLTRACE(L"Adding support assembly reference to %s", moduleInfo.name.c_str());
            AddSupportAssemblyReference(moduleId, pPerModuleData->m_observableTypeRefs, pPerModuleData->m_supportAssemblyRefs);
            pPerModuleData->m_supportAssemblyReferenced = true;
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

        InstrumentMethodBody(props.name, info, metadataImport, pPerModuleData);
    });
}

bool IsExcludedAssembly(const AssemblyProps& assemblyProps)
{
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

bool CRxProfiler::ReferencesObservableInterfaces(ModuleID moduleId, ObservableTypeReferences& typeRefs)
{
    CMetadataImport metadataImport = m_profilerInfo.GetMetadataImport(moduleId, ofRead);
    CMetadataAssemblyImport metadataAssemblyImport = m_profilerInfo.GetMetadataAssemblyImport(moduleId, ofRead);
    mdTypeRef observableRef;
    for (auto assemblyEnum = metadataAssemblyImport.EnumAssemblyRefs(); assemblyEnum.MoveNext(); )
    {
        if (metadataImport.TryFindTypeRef(assemblyEnum.Current(), L"System.IObservable`1", observableRef))
        {
            typeRefs.m_IObservable = observableRef;
            return true;
        }
    }

    return false;
}

void CRxProfiler::AddSupportAssemblyReference(ModuleID moduleId, const ObservableTypeReferences& observableRefs, SupportAssemblyReferences& refs)
{
    // would be nice to sync this automatically
    static const COR_SIGNATURE c_ReturnedSig[] = { 
        0x10, 0x01, 0x02, 0x15, 0x12, 0x35, 0x01, 0x1e,
        0x00, 0x15, 0x12, 0x35, 0x01, 0x1e, 0x00, 0x08
    };
    static const byte c_PublicKeyToken[] = { 0xa8, 0xb3, 0x93, 0x07, 0x28, 0x3e, 0x56, 0x3a };

    CMetadataAssemblyEmit assemblyEmit = m_profilerInfo.GetMetadataAssemblyEmit(moduleId, ofRead | ofWrite);
    CMetadataEmit emit = m_profilerInfo.GetMetadataEmit(moduleId, ofRead | ofWrite);

    ASSEMBLYMETADATA metadata = {};
    metadata.usMajorVersion = 1;
    metadata.usMinorVersion = 0;
    metadata.usBuildNumber = 0;
    metadata.usRevisionNumber = 0;

    refs.m_AssemblyRef = assemblyEmit.DefineAssemblyRef({c_PublicKeyToken, sizeof c_PublicKeyToken}, GetSupportAssemblyName(), metadata, {});
    refs.m_Instrument = emit.DefineTypeRefByName(refs.m_AssemblyRef, L"ReactivityProfiler.Support.Instrument");
    refs.m_Returned = emit.DefineMemberRef({
        refs.m_Instrument,
        L"Returned",
        {c_ReturnedSig, sizeof c_ReturnedSig}
    });
}

void CRxProfiler::InstrumentMethodBody(const std::wstring& name, const FunctionInfo& info, const CMetadataImport& metadata, const std::shared_ptr<PerModuleData>& pPerModuleData)
{
    using namespace Instrumentation;

    simplespan<const byte> ilCode = m_profilerInfo.GetILFunctionBody(info.moduleId, info.functionToken);
    if (!ilCode)
    {
        ATLTRACE(L"%s is not an IL function", name.c_str());
        return;
    }

    ATLTRACE(L"%s has %d bytes of IL", name.c_str(), ilCode.length());
    const byte* codeBytes = ilCode.begin();
    const IMAGE_COR_ILMETHOD* pMethodImage = reinterpret_cast<const IMAGE_COR_ILMETHOD*>(codeBytes);

    Method method(pMethodImage);

    ObservableTypeReferences observableTypeRefs;
    SupportAssemblyReferences supportRefs;
    std::unique_lock<std::mutex> pmd_lock(pPerModuleData->m_mutex);
    observableTypeRefs = pPerModuleData->m_observableTypeRefs;
    supportRefs = pPerModuleData->m_supportAssemblyRefs;
    pmd_lock.unlock();

    struct ObservableCallInfo
    {
        const Instruction* m_pInstruction = 0;
        simplespan<const COR_SIGNATURE> m_observableTypeArgSigSpan;
    };

    std::vector<ObservableCallInfo> observableCalls;

    for (auto it = method.m_instructions.begin(); it < method.m_instructions.end(); it++)
    {
        auto pInstr = it->get();
        auto operation = pInstr->m_operation;
        if (operation == CEE_CALL || operation == CEE_CALLVIRT)
        {
            mdToken method = static_cast<mdToken>(pInstr->m_operand);

            MethodCallInfo methodCallInfo = GetMethodCallInfo(method, metadata);

            ATLTRACE(L"%s calls %s", name.c_str(), methodCallInfo.name.c_str());

            try
            {
#ifdef DEBUG
                MethodSignatureReader::Check(methodCallInfo.sigBlob);
#endif

                MethodSignatureReader sigReader(methodCallInfo.sigBlob);
                sigReader.MoveNextParam(); // move to the return value "parameter"
                auto returnReader = sigReader.GetParamReader();
                if (!returnReader.IsVoid())
                {
                    auto returnTypeReader = returnReader.GetTypeReader();
                    if (returnTypeReader.GetTypeKind() == ELEMENT_TYPE_GENERICINST)
                    {
                        if (returnTypeReader.GetToken() == observableTypeRefs.m_IObservable)
                        {
                            ATLTRACE(L"%s returns an IObservable!", methodCallInfo.name.c_str());

                            if (methodCallInfo.genericInstBlob)
                            {
                                // Invoking a generic method, so need to construct the instantiated sig
#ifdef DEBUG
                                MethodSpecSignatureReader::Check(methodCallInfo.genericInstBlob);
#endif

                                MethodSpecSignatureReader specReader(methodCallInfo.genericInstBlob);
                                std::vector<SignatureBlob> argTypeSpans;
                                while (specReader.MoveNextArgType())
                                {
                                    argTypeSpans.push_back(specReader.GetArgTypeReader().GetSigSpan());
                                }

                                ATLTRACE(L"Skipping generic method call");
                                continue;
                            }

                            returnTypeReader.MoveNextTypeArg();
                            observableCalls.push_back({ pInstr, returnTypeReader.GetTypeReader().GetSigSpan() });
                        }
                    }
                }
            }
            catch (std::exception ex)
            {
                RELTRACE("Failed to check signature blob: %s", ex.what());
            }
        }
    }

    if (observableCalls.empty())
    {
        return;
    }

    CMetadataEmit emit = m_profilerInfo.GetMetadataEmit(info.moduleId, ofRead | ofWrite);

    for (auto call : observableCalls)
    {
        // for now just add a MethodSpec token regardless - but really ought to avoid duplicates
        std::vector<COR_SIGNATURE> sig;
        MethodSpecSignatureWriter sigWriter(sig, 1);
        sigWriter.AddTypeArg(call.m_observableTypeArgSigSpan);
        mdMethodSpec methodSpecToken = emit.DefineMethodSpec({
            supportRefs.m_Returned,
            {sig.data(), sig.size()}
        });

        int32_t instrumentationPoint = ++pPerModuleData->m_instrumentationPointSource;
        InstructionList instrs;
        instrs.push_back(std::make_unique<Instruction>(CEE_LDC_I4, instrumentationPoint));
        instrs.push_back(std::make_unique<Instruction>(CEE_CALL, methodSpecToken));

        long offsetToInsertAt = call.m_pInstruction->m_origOffset + call.m_pInstruction->length();
        ATLTRACE("Inserting at %lx: %d; %x", offsetToInsertAt, instrumentationPoint, methodSpecToken);
        method.InsertInstructionsAtOriginalOffset(
            offsetToInsertAt,
            instrs);
    }

    // allow for the ldc.i4 instruction
    method.IncrementStackSize(1);
    
#ifdef DEBUG
    method.DumpIL(true);
#endif

    DWORD size = method.GetMethodSize();

    // buffer is owned by the runtime, we don't need to free it
    auto rewrittenILBuffer = m_profilerInfo.AllocateFunctionBody(info.moduleId, size);
    method.WriteMethod(reinterpret_cast<IMAGE_COR_ILMETHOD*>(rewrittenILBuffer.begin()));

    m_profilerInfo.SetILFunctionBody(info.moduleId, info.functionToken, rewrittenILBuffer);

    //should probably set the code map as well
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
    simplespan<const COR_SIGNATURE> genericInstBlob;
    if (tokenType == mdtMethodSpec)
    {
        // Generic
        MethodSpecProps specProps = metadata.GetMethodSpecProps(method);
        methodDefOrRef = specProps.genericMethodToken;
        genericInstBlob = specProps.sigBlob;
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
        sigBlob = defProps.sigBlob;
    }
    break;
    case mdtMemberRef:
    {
        auto refProps = metadata.GetMemberRefProps(methodDefOrRef);
        methodName = refProps.name;
        sigBlob = refProps.sigBlob;
    }
    break;

    default:
        // Unexpected - ignore
        ATLTRACE(L"Unexpected token type in CALL(VIRT). Token: %x - ignoring instruction", methodDefOrRef);
        return {};
    }

    return { methodName, sigBlob, genericInstBlob };
}
