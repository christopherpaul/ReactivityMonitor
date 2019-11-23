#include "pch.h"
#include "RxProfiler.h"
#include "RxProfilerImpl.h"
#include "Signature.h"

using namespace Instrumentation;

struct MethodCallInfo
{
    std::wstring name;
    SignatureBlob sigBlob;
    SignatureBlob genericInstBlob;
    SignatureBlob typeSpecBlob;
};

typedef std::variant<SignatureBlob, std::vector<COR_SIGNATURE>> SigSpanOrVector;
static SignatureBlob getSpan(const SigSpanOrVector& ssov)
{
    if (std::holds_alternative<SignatureBlob>(ssov))
    {
        return std::get<SignatureBlob>(ssov);
    }
    else
    {
        return std::get<std::vector<COR_SIGNATURE>>(ssov);
    }
}

struct ObservableCallInfo
{
    const Instruction* m_pInstruction = 0;
    SigSpanOrVector m_observableTypeArg;
};

class MethodBodyInstrumenter
{
public:
    MethodBodyInstrumenter(CProfilerInfo& profilerInfo, const MethodProps& props, const FunctionInfo& info, CMetadataImport& metadata, std::shared_ptr<PerModuleData>& pPerModuleData) :
        m_profilerInfo(profilerInfo),
        m_methodProps(props),
        m_functionInfo(info),
        m_metadataImport(metadata),
        m_pPerModuleData(pPerModuleData)
    {
        FetchPerModuleDataAtomic();
    }

    void Instrument();

private:
    void FetchPerModuleDataAtomic();
    bool TryFindObservableCalls();
    void InstrumentCall(ObservableCallInfo& call, CMetadataEmit& emit);
    MethodCallInfo GetMethodCallInfo(mdToken method);

    CProfilerInfo& m_profilerInfo;
    const MethodProps& m_methodProps;
    const FunctionInfo& m_functionInfo;
    CMetadataImport& m_metadataImport;
    std::shared_ptr<PerModuleData>& m_pPerModuleData;

    ObservableTypeReferences observableTypeRefs;
    SupportAssemblyReferences supportRefs;

    std::unique_ptr<Method> m_method;
    std::vector<ObservableCallInfo> m_observableCalls;
};

void CRxProfiler::InstrumentMethodBody(const MethodProps& props, const FunctionInfo& info, CMetadataImport& metadata, std::shared_ptr<PerModuleData>& pPerModuleData)
{
    MethodBodyInstrumenter instrumenter(m_profilerInfo, props, info, metadata, pPerModuleData);
    instrumenter.Instrument();
}

void MethodBodyInstrumenter::Instrument()
{
    simplespan<const byte> ilCode = m_profilerInfo.GetILFunctionBody(m_functionInfo.moduleId, m_functionInfo.functionToken);
    if (!ilCode)
    {
        ATLTRACE(L"%s is not an IL function", m_methodProps.name.c_str());
        return;
    }

    ATLTRACE(L"%s (%x) has %d bytes of IL starting at RVA 0x%x", m_methodProps.name.c_str(), m_functionInfo.functionToken, ilCode.length(), m_methodProps.codeRva);
    const byte* codeBytes = ilCode.begin();
    const IMAGE_COR_ILMETHOD* pMethodImage = reinterpret_cast<const IMAGE_COR_ILMETHOD*>(codeBytes);

    m_method = std::make_unique<Method>(pMethodImage);

    if (!TryFindObservableCalls())
    {
        return;
    }

    CMetadataEmit emit = m_profilerInfo.GetMetadataEmit(m_functionInfo.moduleId, ofRead | ofWrite);

    for (auto call : m_observableCalls)
    {
        InstrumentCall(call, emit);
    }

    // allow for the ldc.i4 instruction
    m_method->IncrementStackSize(1);

#ifdef DEBUG
    m_method->DumpIL(true);
#endif

    DWORD size = m_method->GetMethodSize();

    // buffer is owned by the runtime, we don't need to free it
    auto rewrittenILBuffer = m_profilerInfo.AllocateFunctionBody(m_functionInfo.moduleId, size);
    m_method->WriteMethod(reinterpret_cast<IMAGE_COR_ILMETHOD*>(rewrittenILBuffer.begin()));

    m_profilerInfo.SetILFunctionBody(m_functionInfo.moduleId, m_functionInfo.functionToken, rewrittenILBuffer);

    //should probably set the code map as well
}

void MethodBodyInstrumenter::FetchPerModuleDataAtomic()
{
    std::lock_guard<std::mutex> pmd_lock(m_pPerModuleData->m_mutex);
    observableTypeRefs = m_pPerModuleData->m_observableTypeRefs;
    supportRefs = m_pPerModuleData->m_supportAssemblyRefs;
}

bool MethodBodyInstrumenter::TryFindObservableCalls()
{
    for (auto it = m_method->m_instructions.begin(); it < m_method->m_instructions.end(); it++)
    {
        auto pInstr = it->get();
        auto operation = pInstr->m_operation;
        if (operation != CEE_CALL && operation != CEE_CALLVIRT)
        {
            // We're only interested in call instructions
            continue;
        }

        mdToken calledMethodToken = static_cast<mdToken>(pInstr->m_operand);
        MethodCallInfo methodCallInfo = GetMethodCallInfo(calledMethodToken);

        ATLTRACE(L"%s calls %s (RVA %x)", m_methodProps.name.c_str(), methodCallInfo.name.c_str(),
            m_methodProps.codeRva + pInstr->m_origOffset);

#ifdef DEBUG
        MethodSignatureReader::Check(methodCallInfo.sigBlob);
#endif

        MethodSignatureReader sigReader(methodCallInfo.sigBlob);
        sigReader.MoveNextParam(); // move to the return value "parameter"
        auto returnReader = sigReader.GetParamReader();

        if (!returnReader.HasType())
        {
            // Method returns void (or something without a static type).
            continue;
        }

        auto returnTypeReader = returnReader.GetTypeReader();
        if (returnTypeReader.GetTypeKind() != ELEMENT_TYPE_GENERICINST ||
            returnTypeReader.GetToken() != observableTypeRefs.m_IObservable)
        {
            // We only care about IObservable<T>, which is a generic instantiation
            // (Maybe in future we'll also look for types that implement this, but
            // that would be a lot more effort and isn't very common.)
            continue;
        }

        ATLTRACE(L"%s returns an IObservable!", methodCallInfo.name.c_str());

        returnTypeReader.MoveNextTypeArg();

        std::vector<SignatureBlob> typeTypeArgs, methodTypeArgs;

        if (methodCallInfo.typeSpecBlob)
        {
            // Invoking a method on a constructed type instance
            SignatureTypeReader typeSpecReader(methodCallInfo.typeSpecBlob);
            if (typeSpecReader.GetTypeKind() == ELEMENT_TYPE_GENERICINST)
            {
                typeTypeArgs = typeSpecReader.GetTypeArgSpans();
            }
        }

        if (methodCallInfo.genericInstBlob)
        {
            // Invoking a generic method instance
#ifdef DEBUG
            MethodSpecSignatureReader::Check(methodCallInfo.genericInstBlob);
#endif

            methodTypeArgs = MethodSpecSignatureReader::GetTypeArgSpans(methodCallInfo.genericInstBlob);
        }

        ObservableCallInfo callInfo;
        callInfo.m_pInstruction = pInstr;

        // Record the IObservable type argument for the return type
        if (!methodTypeArgs.empty() || !typeTypeArgs.empty())
        {
            callInfo.m_observableTypeArg = returnTypeReader
                .GetTypeReader()
                .SubstituteTypeArgs(typeTypeArgs, methodTypeArgs);
        }
        else
        {
            callInfo.m_observableTypeArg = returnTypeReader.GetTypeReader().GetSigSpan();
        }

        m_observableCalls.push_back(std::move(callInfo));
    }

    return !m_observableCalls.empty();
}

void MethodBodyInstrumenter::InstrumentCall(ObservableCallInfo& call, CMetadataEmit& emit)
{
    int32_t instrumentationPoint = ++m_pPerModuleData->m_instrumentationPointSource;

    // Generate a call to Instrument.Calling(n) to be inserted right before the call.
    InstructionList callCallingInstrs;
    callCallingInstrs.push_back(std::make_unique<Instruction>(CEE_LDC_I4, instrumentationPoint));
    callCallingInstrs.push_back(std::make_unique<Instruction>(CEE_CALL, supportRefs.m_Calling));

    ATLTRACE("Inserting at %lx: %d", call.m_pInstruction->m_origOffset, instrumentationPoint);
    m_method->InsertInstructionsAtOriginalOffset(
        call.m_pInstruction->m_origOffset,
        callCallingInstrs);

    // Generate a call to Instrument.Returned(retval, n) to be inserted right after
    // the call.
    std::vector<COR_SIGNATURE> sig;
    MethodSpecSignatureWriter sigWriter(sig, 1);
    SignatureBlob argBlob = getSpan(call.m_observableTypeArg);
    sigWriter.AddTypeArg(argBlob);

    // we'll probably end up asking for the same combinations many times,
    // but (empirically) DefineMethodSpec is smart enough to return the
    // same token each time.
    mdMethodSpec methodSpecToken = emit.DefineMethodSpec({ supportRefs.m_Returned, sig });

    InstructionList callReturnedInstrs;
    callReturnedInstrs.push_back(std::make_unique<Instruction>(CEE_LDC_I4, instrumentationPoint));
    callReturnedInstrs.push_back(std::make_unique<Instruction>(CEE_CALL, methodSpecToken));

    long offsetToInsertAt = call.m_pInstruction->m_origOffset + call.m_pInstruction->length();
    ATLTRACE("Inserting at %lx: %d; %x", offsetToInsertAt, instrumentationPoint, methodSpecToken);
    m_method->InsertInstructionsAtOriginalOffset(
        offsetToInsertAt,
        callReturnedInstrs);
}

MethodCallInfo MethodBodyInstrumenter::GetMethodCallInfo(mdToken method)
{
    auto methodTokenType = TypeFromToken(method);
    mdToken methodDefOrRef;
    simplespan<const COR_SIGNATURE> sigBlob;
    simplespan<const COR_SIGNATURE> genericInstBlob;
    if (methodTokenType == mdtMethodSpec)
    {
        // Generic method instance
        MethodSpecProps specProps = m_metadataImport.GetMethodSpecProps(method);
        methodDefOrRef = specProps.genericMethodToken;
        genericInstBlob = specProps.sigBlob;
    }
    else
    {
        methodDefOrRef = method;
    }

    std::wstring methodName;
    mdToken typeToken;
    switch (TypeFromToken(methodDefOrRef))
    {
    case mdtMethodDef:
    {
        auto defProps = m_metadataImport.GetMethodProps(methodDefOrRef);
        typeToken = defProps.classDefToken;
        methodName = defProps.name;
        sigBlob = defProps.sigBlob;
    }
    break;
    case mdtMemberRef:
    {
        auto refProps = m_metadataImport.GetMemberRefProps(methodDefOrRef);
        typeToken = refProps.declToken;
        methodName = refProps.name;
        sigBlob = refProps.sigBlob;
    }
    break;

    default:
        // Unexpected - ignore
        ATLTRACE(L"Unexpected token type in CALL(VIRT). Token: %x - ignoring instruction", methodDefOrRef);
        return {};
    }

    SignatureBlob typeSpecSig;
    auto typeTokenType = TypeFromToken(typeToken);
    if (typeTokenType == mdtTypeSpec)
    {
        // Generic type instance
        typeSpecSig = m_metadataImport.GetTypeSpecFromToken(typeToken);
    }

    return { methodName, sigBlob, genericInstBlob, typeSpecSig };
}