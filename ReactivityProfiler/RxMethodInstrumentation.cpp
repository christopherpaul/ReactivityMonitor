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
    int m_instructionOffset = 0; // if call instruction has prefix(es), this is the offset of the (first) prefix
    int m_instructionLength = 0; // includes length of any prefix(es)
    SigSpanOrVector m_returnType;
    mdToken m_returnObservableTypeRef = 0; // IObservable, IConnectedObservable, IGroupedObservable...?
    SigSpanOrVector m_returnTypeArg; // if call returns IObservable<T>, this is T

    // The follows vectors hold a value for each method argument starting with the first observable one.
    std::vector<bool> m_argIsObservable;
    // Span of the sig that applies to the parameter type
    std::vector<SigSpanOrVector> m_argTypeSpan;
};

class MethodBodyInstrumenter
{
public:
    MethodBodyInstrumenter(CProfilerInfo& profilerInfo, FunctionID functionId, const MethodProps& props, const FunctionInfo& info, CMetadataImport& metadata, std::shared_ptr<PerModuleData>& pPerModuleData) :
        m_profilerInfo(profilerInfo),
        m_functionId(functionId),
        m_methodProps(props),
        m_functionInfo(info),
        m_metadataImport(metadata),
        m_pPerModuleData(pPerModuleData),
        m_instrumentedMethodId(0)
    {
    }

    void Instrument();

private:
    RewrittenFunctionData GetOrCreateRewrittenFunctionData();
    RewrittenFunctionData CreateInstrumentedFunction();
    bool TryFindObservableCalls();
    void InstrumentCall(ObservableCallInfo& call, CMetadataEmit& emit);
    MethodCallInfo GetMethodCallInfo(mdToken method);

    CProfilerInfo& m_profilerInfo;
    FunctionID m_functionId;
    const MethodProps& m_methodProps;
    const FunctionInfo& m_functionInfo;
    CMetadataImport& m_metadataImport;
    std::shared_ptr<PerModuleData>& m_pPerModuleData;
    std::wstring m_owningTypeName;

    ObservableTypeReferences observableTypeRefs;
    SupportAssemblyReferences supportRefs;

    std::unique_ptr<Method> m_method;
    std::vector<ObservableCallInfo> m_observableCalls;

    int32_t m_instrumentedMethodId;
};

static std::atomic_int32_t s_instrumentationIdSource = 0;

void CRxProfiler::InstrumentMethodBody(FunctionID functionId, const MethodProps& props, const FunctionInfo& info, CMetadataImport& metadata, std::shared_ptr<PerModuleData>& pPerModuleData)
{
    try
    {
        MethodBodyInstrumenter instrumenter(m_profilerInfo, functionId, props, info, metadata, pPerModuleData);
        instrumenter.Instrument();
    }
    catch (std::exception ex)
    {
        ATLTRACE("Exception while instrumenting: %s", ex.what());
    }
}

RewrittenFunctionData MethodBodyInstrumenter::GetOrCreateRewrittenFunctionData()
{
    std::unique_lock<std::mutex> pmd_lock(m_pPerModuleData->m_mutex);

    observableTypeRefs = m_pPerModuleData->m_observableTypeRefs;
    supportRefs = m_pPerModuleData->m_supportAssemblyRefs;

    // task to instrument the function
    std::packaged_task<RewrittenFunctionData()> task([=] { return CreateInstrumentedFunction(); });

    // try and put the future result of the task into the map
    decltype(m_pPerModuleData->m_rewrittenFunctions)::value_type mapEntry = { m_functionInfo.functionToken, task.get_future() };
    auto insertResult = m_pPerModuleData->m_rewrittenFunctions.insert(std::move(mapEntry));

    pmd_lock.unlock();

    if (insertResult.second)
    {
        // we inserted our task's future result, so run the task.
        task();
    }
    else
    {
        ATLTRACE(L"%s has already been instrumented", m_methodProps.name.c_str());
    }

    // obtain the result from the task (either the one we inserted successfully, or one
    // inserted previously)
    auto insertedMapEntry = insertResult.first;
    auto& insertedFuture = insertedMapEntry->second;
    return insertedFuture.get();
}

void MethodBodyInstrumenter::Instrument()
{
    RewrittenFunctionData data = GetOrCreateRewrittenFunctionData();

    if (data.m_rewrittenILBuffer)
    {
        m_profilerInfo.SetILFunctionBody(m_functionInfo.moduleId, m_functionInfo.functionToken, data.m_rewrittenILBuffer);
        m_profilerInfo.SetILInstrumentedCodeMap(m_functionId, true, data.m_instrumentedCodeMap);
    }
}

RewrittenFunctionData MethodBodyInstrumenter::CreateInstrumentedFunction()
{
    simplespan<const byte> ilCode = m_profilerInfo.GetILFunctionBody(m_functionInfo.moduleId, m_functionInfo.functionToken);
    if (!ilCode)
    {
        ATLTRACE(L"%s is not an IL function", m_methodProps.name.c_str());
        return {};
    }

    ATLTRACE(L"%s (%x) has %d bytes of IL starting at RVA 0x%x", m_methodProps.name.c_str(), m_functionInfo.functionToken, ilCode.length(), m_methodProps.codeRva);
    const byte* codeBytes = ilCode.begin();
    const IMAGE_COR_ILMETHOD* pMethodImage = reinterpret_cast<const IMAGE_COR_ILMETHOD*>(codeBytes);

    m_method = std::make_unique<Method>(pMethodImage);

    if (!TryFindObservableCalls())
    {
        return {};
    }

    m_instrumentedMethodId = ++s_instrumentationIdSource;

    auto owningTypeProps = m_metadataImport.GetTypeDefProps(m_methodProps.classDefToken);
    m_owningTypeName = owningTypeProps.name;
    mdTypeDef typeDefToken = m_methodProps.classDefToken;
    while (IsTdNested(owningTypeProps.attrFlags))
    {
        typeDefToken = m_metadataImport.GetParentTypeDef(typeDefToken);
        owningTypeProps = m_metadataImport.GetTypeDefProps(typeDefToken);
        m_owningTypeName = owningTypeProps.name + L"+" + m_owningTypeName;
    }

    g_Store.AddMethodInfo(
        m_instrumentedMethodId,
        m_functionInfo.moduleId,
        m_functionInfo.functionToken,
        m_owningTypeName,
        m_methodProps.name);

    CMetadataEmit emit = m_profilerInfo.GetMetadataEmit(m_functionInfo.moduleId, ofRead | ofWrite);

    for (auto call : m_observableCalls)
    {
        InstrumentCall(call, emit);
    }

    // allow for up to two additional arguments pushed in post-call instrumentation
    m_method->IncrementStackSize(2);

#ifdef DEBUG
    m_method->DumpIL(true);
#endif

    DWORD size = m_method->GetMethodSize();

    // buffer is owned by the runtime, we don't need to free it
    auto rewrittenILBuffer = m_profilerInfo.AllocateFunctionBody(m_functionInfo.moduleId, size);
    m_method->WriteMethod(reinterpret_cast<IMAGE_COR_ILMETHOD*>(rewrittenILBuffer.begin()));

    ULONG mapSize = m_method->GetILMapSize();
    COR_IL_MAP* ilMapEntries = static_cast<COR_IL_MAP*>(CoTaskMemAlloc(mapSize * sizeof(COR_IL_MAP)));
    m_method->PopulateILMap(mapSize, ilMapEntries);

    g_Store.MethodInstrumentationDone(m_instrumentedMethodId);

    return { rewrittenILBuffer, { ilMapEntries, mapSize } };
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
        if (returnTypeReader.GetTypeKind() != ELEMENT_TYPE_GENERICINST)
        {
            // All the types we care about are generic.
            // We only look for methods that return specific interfaces, not arbitrary subinterfaces
            // or implementations of them.
            continue;
        }

        mdToken returnTypeRef = returnTypeReader.GetToken();
        if (returnTypeRef != observableTypeRefs.m_IObservable &&
            returnTypeRef != observableTypeRefs.m_IConnectableObservable &&
            returnTypeRef != observableTypeRefs.m_IGroupedObservable)
        {
            continue;
        }

        ATLTRACE(L"%s returns an I[Connectable|Grouped]Observable!", methodCallInfo.name.c_str());

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
        int prefixLength = 0;
        auto prefixIt = it;
        while (prefixIt != m_method->m_instructions.begin())
        {
            prefixIt--;
            auto pPotentialPrefixInstr = prefixIt->get();
            if (Operations::m_mapNameOperationDetails[pPotentialPrefixInstr->m_operation].opcodeKind != IPrefix)
            {
                break;
            }

            prefixLength += pPotentialPrefixInstr->length();
        }

        callInfo.m_returnObservableTypeRef = returnTypeRef;
        callInfo.m_calledMethodName = methodCallInfo.name;
        callInfo.m_instructionOffset = pInstr->m_origOffset - prefixLength;
        callInfo.m_instructionLength = prefixLength + pInstr->length();

        // Record the return type
        returnTypeReader.MoveNextTypeArg();
        if (returnTypeRef == observableTypeRefs.m_IGroupedObservable)
        {
            // First type arg to IGroupedObservable is TKey, we want the second, TElement.
            returnTypeReader.MoveNextTypeArg();
        }
        callInfo.m_returnTypeArg = getSigSpanOrVector(returnTypeReader.GetTypeReader());
        callInfo.m_returnType = getSigSpanOrVector(returnTypeReader);

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
            auto paramTypeKind = paramTypeReader.GetTypeKind();
            // Interesting arguments could include observables, delegates returning observables,
            // Tasks returning observables, arrays of observables, and probably others.
            // Here we just filter out value types and some other less common stuff, and leave
            // it to the Instrument methods in the support assembly to decide whether to do anything
            // with the rest.
            bool isObservable =
                paramTypeKind == ELEMENT_TYPE_CLASS ||
                paramTypeKind == ELEMENT_TYPE_GENERICINST ||
                paramTypeKind == ELEMENT_TYPE_SZARRAY;

            // No need to start recording arg info until the first observable arg
            if (isObservable || !callInfo.m_argIsObservable.empty())
            {
                callInfo.m_argIsObservable.push_back(isObservable);
                callInfo.m_argTypeSpan.push_back(getSigSpanOrVector(paramTypeReader));
            }
        }

        ATLTRACE(L"%s has %d interesting args", methodCallInfo.name.c_str(),
            std::count(callInfo.m_argIsObservable.begin(), callInfo.m_argIsObservable.end(), true));

        m_observableCalls.push_back(std::move(callInfo));
    }

    return !m_observableCalls.empty();
}

void MethodBodyInstrumenter::InstrumentCall(ObservableCallInfo& call, CMetadataEmit& emit)
{
    int32_t instrumentationPoint = ++s_instrumentationIdSource;

    InstructionList preCallInstrs;
    if (!call.m_argIsObservable.empty())
    {
        // Calling Instrument.Argument(arg, n) on each observable arg means we need to
        // stash the stacked argument values somewhere temporarily, so add some extra
        // locals.
        // (Currently not taking account of the possibility of sharing these locals between
        // multiple instrumentations.)
        std::vector<SignatureBlob> argTypeSpans;
        std::transform(
            call.m_argTypeSpan.begin(), call.m_argTypeSpan.end(),
            std::back_inserter(argTypeSpans),
            getSpan);
        int argCount = static_cast<int>(call.m_argIsObservable.size()); // not necessarily all args, but the ones we're dealing with

        mdSignature localsSigTok = m_method->GetLocalsSignature();
        std::vector<COR_SIGNATURE> extendedLocalsSig;
        int existingLocalsCount;
        if (IsNilToken(localsSigTok))
        {
            existingLocalsCount = 0;
            extendedLocalsSig = LocalsSignatureWriter::MakeSig(argCount, [&](LocalsSignatureWriter& w) {
                std::for_each(argTypeSpans.begin(), argTypeSpans.end(), 
                    [&](SignatureBlob b) { w.WriteLocal().Write(b); });
            });
        }
        else
        {
            SignatureBlob localsSigBlob;
            localsSigBlob = m_metadataImport.GetSigFromToken(localsSigTok);
            LocalsSignatureReader localsSigReader(localsSigBlob);
            existingLocalsCount = localsSigReader.GetCount();
            extendedLocalsSig = localsSigReader.AppendLocals(argTypeSpans);
        }

        mdSignature extendedLocalsTok = emit.GetTokenFromSig(extendedLocalsSig);
        ATLTRACE("Got extended locals token: %x for %s", extendedLocalsTok, FormatBytes(extendedLocalsSig).c_str());
        m_method->SetLocalsSignature(extendedLocalsTok);

        // Step 1: working backwards through the args, store each arg into its local.
        // Don't do arg 0 as we'd just have to load it again.
        for (int arg = argCount - 1; arg > 0; arg--)
        {
            preCallInstrs.push_back(std::make_unique<Instruction>(CEE_STLOC, existingLocalsCount + arg));
        }
        // Step 2: working forwards through the args, load each arg, and call Instrument.Argument if observable.
        for (int arg = 0; arg < argCount; arg++)
        {
            if (arg > 0)
            {
                preCallInstrs.push_back(std::make_unique<Instruction>(CEE_LDLOC, existingLocalsCount + arg));
            }

            if (call.m_argIsObservable[arg])
            {
                std::vector<COR_SIGNATURE> argumentCallSig;
                MethodSpecSignatureWriter(argumentCallSig, 1).AddTypeArg(getSpan(call.m_argTypeSpan[arg]));

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

    // Generate a call to Instrument.Returned(retval, n) or ReturnedSubinterface(retval, n, type)
    // to be inserted right after the call.
    std::vector<COR_SIGNATURE> sig;
    MethodSpecSignatureWriter sigWriter(sig, 1);
    sigWriter.AddTypeArg(getSpan(call.m_returnTypeArg));

    InstructionList postCallInstrs;
    // Initially on the stack is the returned value from the call, and this will be the first
    // argument to our generated call. Push the instrumentation point ID as the second argument.
    postCallInstrs.push_back(std::make_unique<Instruction>(CEE_LDC_I4, instrumentationPoint));

    if (call.m_returnObservableTypeRef == observableTypeRefs.m_IObservable)
    {
        mdMethodSpec methodSpecToken = emit.DefineMethodSpec({ supportRefs.m_Returned, sig });
        postCallInstrs.push_back(std::make_unique<Instruction>(CEE_CALL, methodSpecToken));
    }
    else
    {
        mdTypeSpec returnTypeSpecToken = emit.DefineTypeSpec(getSpan(call.m_returnType));

        // Push the type handle of the return type as the third argument.
        postCallInstrs.push_back(std::make_unique<Instruction>(CEE_LDTOKEN, returnTypeSpecToken));
        mdMethodSpec methodSpecToken = emit.DefineMethodSpec({ supportRefs.m_ReturnedSubinterface, sig });
        postCallInstrs.push_back(std::make_unique<Instruction>(CEE_CALL, methodSpecToken));

        // Need to cast the return value to the expected subtype
        postCallInstrs.push_back(std::make_unique<Instruction>(CEE_CASTCLASS, returnTypeSpecToken));
    }

    long offsetToInsertAt = call.m_instructionOffset + call.m_instructionLength;
    ATLTRACE("Inserting %d instructions at %x", postCallInstrs.size(), offsetToInsertAt);
    m_method->InsertInstructionsAtOriginalOffset(
        offsetToInsertAt,
        postCallInstrs);

    g_Store.AddInstrumentationInfo(
        instrumentationPoint, 
        m_instrumentedMethodId,
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
