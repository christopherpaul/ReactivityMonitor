#include "pch.h"
#include "RxProfiler.h"
#include "RxProfilerImpl.h"
#include "Signature.h"
#include "Store.h"

using namespace Instrumentation;

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

struct MethodCallInfo
{
    std::wstring name;
    SignatureBlob sigBlob;
    SignatureBlob genericInstBlob;
    mdToken typeToken;
    SignatureBlob typeSpecBlob;
};

struct ObservableCallInfo
{
    std::wstring m_calledMethodName;
    int m_instructionOffset = 0;
    int m_instructionLength = 0;
    SigSpanOrVector m_returnTypeArg; // if call returns IObservable<T>, this is T

    // The follows vectors hold a value for each method argument starting with the first observable one.
    std::vector<bool> m_argIsObservable;
    // Span of the sig that applies to the parameter type
    std::vector<SigSpanOrVector> m_argTypeSpan;
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
    try
    {
        MethodBodyInstrumenter instrumenter(m_profilerInfo, props, info, metadata, pPerModuleData);
        instrumenter.Instrument();
    }
    catch (std::exception ex)
    {
        ATLTRACE("Exception while instrumenting: %s", ex.what());
    }
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

        // If there's any generic stuff going on, we need to fabricate new bits of
        // sig using the type/method type arguments rather than just returning the
        // appropriate span of the method sig. Set up a function to do this.
        std::function<SigSpanOrVector(SignatureTypeReader&)> getSigSpanOrVector;
        if (!methodTypeArgs.empty() || !typeTypeArgs.empty())
        {
            getSigSpanOrVector = [&typeTypeArgs, &methodTypeArgs](SignatureTypeReader& tr) {
                return tr.SubstituteTypeArgs(typeTypeArgs, methodTypeArgs);
            };
        }
        else
        {
            getSigSpanOrVector = [](SignatureTypeReader& tr) {
                return tr.GetSigSpan();
            };
        }

        ObservableCallInfo callInfo;
        callInfo.m_calledMethodName = methodCallInfo.name;
        callInfo.m_instructionOffset = pInstr->m_origOffset;
        callInfo.m_instructionLength = pInstr->length();

        // Record the IObservable type argument for the return type
        returnTypeReader.MoveNextTypeArg();
        callInfo.m_returnTypeArg = getSigSpanOrVector(returnTypeReader.GetTypeReader());

        // Now look at the arguments
        while (sigReader.MoveNextParam())
        {
            auto paramReader = sigReader.GetParamReader();
            if (paramReader.IsTypedByRef() || paramReader.IsByRef())
            {
                // Avoid dealing with anything except by-value args for now. This means
                // we can't do anything with earlier args either.
                callInfo.m_argIsObservable.clear();
                callInfo.m_argTypeSpan.clear();
                continue;
            }

            auto paramTypeReader = paramReader.GetTypeReader();
            bool isObservable = 
                paramTypeReader.GetTypeKind() == ELEMENT_TYPE_GENERICINST &&
                paramTypeReader.GetToken() == observableTypeRefs.m_IObservable;

            // No need to start recording arg info until the first observable arg
            if (isObservable || !callInfo.m_argIsObservable.empty())
            {
                callInfo.m_argIsObservable.push_back(isObservable);
                callInfo.m_argTypeSpan.push_back(getSigSpanOrVector(paramTypeReader));
            }
        }

        ATLTRACE(L"%s has %d observable args", methodCallInfo.name.c_str(),
            std::count(callInfo.m_argIsObservable.begin(), callInfo.m_argIsObservable.end(), true));

        m_observableCalls.push_back(std::move(callInfo));
    }

    return !m_observableCalls.empty();
}

void MethodBodyInstrumenter::InstrumentCall(ObservableCallInfo& call, CMetadataEmit& emit)
{
    static std::atomic_int32_t m_instrumentationPointSource = 0;

    int32_t instrumentationPoint = ++m_instrumentationPointSource;

    InstructionList preCallInstrs;
    if (!call.m_argIsObservable.empty())
    {
        // Calling Instrument.Argument(arg, n) on each observable arg means we need to
        // stash the stacked argument values somewhere temporarily, so add some extra
        // locals.
        // (Currently not taking account of the possibility of sharing these locals between
        // multiple instrumentations.)
        mdSignature localsSigTok = m_method->GetLocalsSignature();
        SignatureBlob localsSigBlob = m_metadataImport.GetSigFromToken(localsSigTok);
        ATLTRACE("Got locals sig: %s", FormatBytes(localsSigBlob).c_str());
        LocalsSignatureReader localsSigReader(localsSigBlob);
        int existingLocalsCount = localsSigReader.GetCount();
        int argCount = static_cast<int>(call.m_argIsObservable.size()); // not necessarily all args, but the ones we're dealing with
        std::vector<SignatureBlob> argTypeSpans;
        std::transform(
            call.m_argTypeSpan.begin(), call.m_argTypeSpan.end(), 
            std::back_inserter(argTypeSpans),
            getSpan);
        std::vector<COR_SIGNATURE> extendedLocalsSig = localsSigReader.AppendLocals(argTypeSpans);
        mdSignature extendedLocalsTok = emit.GetTokenFromSig(extendedLocalsSig);
        ATLTRACE("Got extended locals token: %x for %s", extendedLocalsTok, FormatBytes(extendedLocalsSig).c_str());
        m_method->SetLocalsSignature(extendedLocalsTok);

        // Step 1: working backwards through the args, store each arg into its local.
        // Don't do arg 0 as we'd just have to load it again.
        for (int arg = argCount - 1; arg > 0; arg--)
        {
            ATLTRACE("DBG: Step 1, arg %d", arg);
            preCallInstrs.push_back(std::make_unique<Instruction>(CEE_STLOC, existingLocalsCount + arg));
        }
        // Step 2: working forwards through the args, load each arg, and call Instrument.Argument if observable.
        for (int arg = 0; arg < argCount; arg++)
        {
            ATLTRACE("DBG: Step 2, arg %d", arg);
            if (arg > 0)
            {
                preCallInstrs.push_back(std::make_unique<Instruction>(CEE_LDLOC, existingLocalsCount + arg));
            }

            if (call.m_argIsObservable[arg])
            {
                ATLTRACE("DBG: Argument instrs for arg %d: A: %s", arg, FormatBytes(getSpan(call.m_argTypeSpan[arg])).c_str());
                SignatureTypeReader argTypeReader(getSpan(call.m_argTypeSpan[arg]));
                ATLTRACE("DBG: Argument instrs for arg %d: B", arg);
                argTypeReader.MoveNextTypeArg(); // we know it's an IObservable<T> and we want the T
                ATLTRACE("DBG: Argument instrs for arg %d: C", arg);
                std::vector<COR_SIGNATURE> argumentCallSig;
                MethodSpecSignatureWriter(argumentCallSig, 1).AddTypeArg(argTypeReader.GetTypeReader().GetSigSpan());
                ATLTRACE("DBG: Argument instrs for arg %d: D", arg);

                mdMethodSpec argumentMethodSpecToken = emit.DefineMethodSpec({ supportRefs.m_Argument, argumentCallSig });

                preCallInstrs.push_back(std::make_unique<Instruction>(CEE_LDC_I4, instrumentationPoint));
                preCallInstrs.push_back(std::make_unique<Instruction>(CEE_CALL, argumentMethodSpecToken));
            }
        }
    }

    // Generate a call to Instrument.Calling(n) to be inserted right before the call.
    preCallInstrs.push_back(std::make_unique<Instruction>(CEE_LDC_I4, instrumentationPoint));
    preCallInstrs.push_back(std::make_unique<Instruction>(CEE_CALL, supportRefs.m_Calling));

    ATLTRACE("Inserting %d instructions at %x", preCallInstrs.size(), call.m_instructionOffset);
    m_method->InsertInstructionsAtOriginalOffset(
        call.m_instructionOffset,
        preCallInstrs);

    // Generate a call to Instrument.Returned(retval, n) to be inserted right after
    // the call.
    std::vector<COR_SIGNATURE> sig;
    MethodSpecSignatureWriter sigWriter(sig, 1);
    SignatureBlob argBlob = getSpan(call.m_returnTypeArg);
    sigWriter.AddTypeArg(argBlob);

    // we'll probably end up asking for the same combinations many times,
    // but (empirically) DefineMethodSpec is smart enough to return the
    // same token each time.
    mdMethodSpec methodSpecToken = emit.DefineMethodSpec({ supportRefs.m_Returned, sig });

    InstructionList postCallInstrs;
    postCallInstrs.push_back(std::make_unique<Instruction>(CEE_LDC_I4, instrumentationPoint));
    postCallInstrs.push_back(std::make_unique<Instruction>(CEE_CALL, methodSpecToken));

    long offsetToInsertAt = call.m_instructionOffset + call.m_instructionLength;
    ATLTRACE("Inserting %d instructions at %x", postCallInstrs.size(), offsetToInsertAt);
    m_method->InsertInstructionsAtOriginalOffset(
        offsetToInsertAt,
        postCallInstrs);

    g_Store.AddInstrumentationInfo(
        instrumentationPoint, 
        m_functionInfo.moduleId, 
        m_functionInfo.functionToken,
        call.m_instructionOffset,
        call.m_calledMethodName);
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
        // Generic type instance (or other constructed type, e.g. an array)
        typeSpecSig = m_metadataImport.GetTypeSpecFromToken(typeToken);
    }

    return { methodName, sigBlob, genericInstBlob, typeToken, typeSpecSig };
}
