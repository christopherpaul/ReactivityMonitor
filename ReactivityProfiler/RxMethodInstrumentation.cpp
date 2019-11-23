#include "pch.h"
#include "RxProfiler.h"
#include "RxProfilerImpl.h"
#include "Signature.h"



void CRxProfiler::InstrumentMethodBody(const MethodProps& props, const FunctionInfo& info, const CMetadataImport& metadata, const std::shared_ptr<PerModuleData>& pPerModuleData)
{
    using namespace Instrumentation;

    simplespan<const byte> ilCode = m_profilerInfo.GetILFunctionBody(info.moduleId, info.functionToken);
    if (!ilCode)
    {
        ATLTRACE(L"%s is not an IL function", props.name.c_str());
        return;
    }

    ATLTRACE(L"%s (%x) has %d bytes of IL starting at RVA 0x%x", props.name.c_str(), info.functionToken, ilCode.length(), props.codeRva);
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
        std::variant<
            SignatureBlob,
            std::vector<COR_SIGNATURE>> m_observableTypeArg;
    };

    std::vector<ObservableCallInfo> observableCalls;

    for (auto it = method.m_instructions.begin(); it < method.m_instructions.end(); it++)
    {
        auto pInstr = it->get();
        auto operation = pInstr->m_operation;
        if (operation == CEE_CALL || operation == CEE_CALLVIRT)
        {
            mdToken calledMethodToken = static_cast<mdToken>(pInstr->m_operand);

            MethodCallInfo methodCallInfo = GetMethodCallInfo(calledMethodToken, metadata);

            ATLTRACE(L"%s calls %s (RVA %x)", props.name.c_str(), methodCallInfo.name.c_str(),
                props.codeRva + pInstr->m_origOffset);

            try
            {
#ifdef DEBUG
                MethodSignatureReader::Check(methodCallInfo.sigBlob);
#endif

                MethodSignatureReader sigReader(methodCallInfo.sigBlob);
                sigReader.MoveNextParam(); // move to the return value "parameter"
                auto returnReader = sigReader.GetParamReader();
                if (returnReader.HasType())
                {
                    auto returnTypeReader = returnReader.GetTypeReader();
                    if (returnTypeReader.GetTypeKind() == ELEMENT_TYPE_GENERICINST)
                    {
                        if (returnTypeReader.GetToken() == observableTypeRefs.m_IObservable)
                        {
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

                            if (!methodTypeArgs.empty() || !typeTypeArgs.empty())
                            {
                                auto substSig = returnTypeReader
                                    .GetTypeReader()
                                    .SubstituteTypeArgs(typeTypeArgs, methodTypeArgs);
                                observableCalls.push_back({ pInstr, substSig });
                            }
                            else
                            {
                                observableCalls.push_back({ pInstr, returnTypeReader.GetTypeReader().GetSigSpan() });
                            }
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
        // Generate a call to Instrument.Returned(retval, n) to be inserted right after
        // the call.
        std::vector<COR_SIGNATURE> sig;
        MethodSpecSignatureWriter sigWriter(sig, 1);
        SignatureBlob argBlob;
        if (std::holds_alternative<SignatureBlob>(call.m_observableTypeArg))
        {
            argBlob = std::get<SignatureBlob>(call.m_observableTypeArg);
        }
        else
        {
            argBlob = std::get<std::vector<COR_SIGNATURE>>(call.m_observableTypeArg);
        }
        sigWriter.AddTypeArg(argBlob);

        // we'll probably end up asking for the same combinations many times,
        // but (empirically) DefineMethodSpec is smart enough to return the
        // same token each time.
        mdMethodSpec methodSpecToken = emit.DefineMethodSpec({
            supportRefs.m_Returned,
            sig
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

MethodCallInfo GetMethodCallInfo(mdToken method, const CMetadataImport& metadata)
{
    auto methodTokenType = TypeFromToken(method);
    mdToken methodDefOrRef;
    simplespan<const COR_SIGNATURE> sigBlob;
    simplespan<const COR_SIGNATURE> genericInstBlob;
    if (methodTokenType == mdtMethodSpec)
    {
        // Generic method instance
        MethodSpecProps specProps = metadata.GetMethodSpecProps(method);
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
        auto defProps = metadata.GetMethodProps(methodDefOrRef);
        typeToken = defProps.classDefToken;
        methodName = defProps.name;
        sigBlob = defProps.sigBlob;
    }
    break;
    case mdtMemberRef:
    {
        auto refProps = metadata.GetMemberRefProps(methodDefOrRef);
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
        typeSpecSig = metadata.GetTypeSpecFromToken(typeToken);
    }

    return { methodName, sigBlob, genericInstBlob, typeSpecSig };
}
