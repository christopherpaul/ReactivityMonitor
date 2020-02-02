#include "pch.h"
#include "RxProfiler.h"
#include "RxProfilerImpl.h"
#include "dllmain.h"
#include "Signature.h"

using namespace Instrumentation;

const wchar_t* GetSupportAssemblyName()
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

    return thisDllPath.substr(0, thisDllPath.find_last_of(L'\\') + 1);
}

static std::vector<COR_SIGNATURE> CreateInstrumentCallingSig()
{
    std::vector<COR_SIGNATURE> callingMethodSig;
    MethodSignatureWriter callingSigWriter(callingMethodSig, false, 1, 0);
    callingSigWriter.SetVoidReturn();
    callingSigWriter.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_I4);
    callingSigWriter.Complete();
    return callingMethodSig;
}

static std::vector<COR_SIGNATURE> CreateInstrumentArgumentSig(const ObservableTypeReferences& observableRefs)
{
    // Construct signature for the reference to Instrument.Argument
    // T Argument<T>(T, int)
    std::vector<COR_SIGNATURE> argumentMethodSig;
    MethodSignatureWriter sigWriter(argumentMethodSig, false, 2, 1); // <T>(,)
    sigWriter.WriteParam().SetMethodTypeVar(0); // returns T
    sigWriter.WriteParam().SetMethodTypeVar(0); // T
    sigWriter.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_I4);
    sigWriter.Complete();

#ifdef DEBUG
    std::stringstream sigDump;
    for (auto b : argumentMethodSig)
    {
        sigDump << std::hex << std::setfill('0') << std::setw(2) << (int)b << " ";
    }
    ATLTRACE("argumentMethodSig = %s", sigDump.str().c_str());
#endif

    return argumentMethodSig;
}


static std::vector<COR_SIGNATURE> CreateInstrumentReturnedSig(const ObservableTypeReferences& observableRefs)
{
    // Construct signature for the reference to Instrument.Returned
    // IObservable<T> Returned<T>(IObservable<T>, int)
    std::vector<COR_SIGNATURE> returnedMethodSig;
    MethodSignatureWriter sigWriter(returnedMethodSig, false, 2, 1); // <T>(,)
    auto returnTypeWriter = sigWriter.WriteParam();
    returnTypeWriter.SetGenericClass(observableRefs.m_IObservable, 1); // IObservable<>
    returnTypeWriter.WriteTypeArg().SetMethodTypeVar(0); // of T
    auto param1Writer = sigWriter.WriteParam();
    param1Writer.SetGenericClass(observableRefs.m_IObservable, 1); // IObservable<>
    param1Writer.WriteTypeArg().SetMethodTypeVar(0); // of T
    auto param2Writer = sigWriter.WriteParam();
    param2Writer.SetPrimitiveKind(ELEMENT_TYPE_I4);
    sigWriter.Complete();

#ifdef DEBUG
    std::stringstream sigDump;
    for (auto b : returnedMethodSig)
    {
        sigDump << std::hex << std::setfill('0') << std::setw(2) << (int)b << " ";
    }
    ATLTRACE("returnedMethodSig = %s", sigDump.str().c_str());
#endif

    return returnedMethodSig;
}

static std::vector<COR_SIGNATURE> CreateInstrumentReturnedSubinterfaceSig(const ObservableTypeReferences& observableRefs, mdTypeRef rtTypeHandle)
{
    // Construct signature for the reference to Instrument.Returned
    // IObservable<T> Returned<T>(IObservable<T>, int, RuntimeTypeHandle)
    std::vector<COR_SIGNATURE> returnedMethodSig;
    MethodSignatureWriter sigWriter(returnedMethodSig, false, 3, 1); // <T>(,,)
    auto returnTypeWriter = sigWriter.WriteParam();
    returnTypeWriter.SetGenericClass(observableRefs.m_IObservable, 1); // IObservable<>
    returnTypeWriter.WriteTypeArg().SetMethodTypeVar(0); // of T
    auto param1Writer = sigWriter.WriteParam();
    param1Writer.SetGenericClass(observableRefs.m_IObservable, 1); // IObservable<>
    param1Writer.WriteTypeArg().SetMethodTypeVar(0); // of T
    auto param2Writer = sigWriter.WriteParam();
    param2Writer.SetPrimitiveKind(ELEMENT_TYPE_I4);
    auto param3Writer = sigWriter.WriteParam();
    param3Writer.SetSimpleValueType(rtTypeHandle);
    sigWriter.Complete();

#ifdef DEBUG
    std::stringstream sigDump;
    for (auto b : returnedMethodSig)
    {
        sigDump << std::hex << std::setfill('0') << std::setw(2) << (int)b << " ";
    }
    ATLTRACE("returnedMethodSig = %s", sigDump.str().c_str());
#endif

    return returnedMethodSig;
}

void CRxProfiler::AddSupportAssemblyReference(ModuleID moduleId, PerModuleData& perModuleData)
{
    SupportAssemblyReferences& refs = perModuleData.m_supportAssemblyRefs;

    CMetadataAssemblyEmit assemblyEmit = m_profilerInfo.GetMetadataAssemblyEmit(moduleId, ofRead | ofWrite);
    CMetadataEmit emit = m_profilerInfo.GetMetadataEmit(moduleId, ofRead | ofWrite);

    static byte c_mscorlibPublicKeyToken[] = { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89 };
    ASSEMBLYMETADATA mscorlibMetadata = {};
    mscorlibMetadata.usMajorVersion = 4;
    mscorlibMetadata.usMinorVersion = 0;
    mscorlibMetadata.usBuildNumber = 0;
    mscorlibMetadata.usRevisionNumber = 0;

    mdAssemblyRef mscorlib = assemblyEmit.DefineAssemblyRef({ c_mscorlibPublicKeyToken, sizeof c_mscorlibPublicKeyToken }, L"mscorlib", mscorlibMetadata, {});

    static const byte c_PublicKeyToken[] = { 0xa8, 0xb3, 0x93, 0x07, 0x28, 0x3e, 0x56, 0x3a };
    ASSEMBLYMETADATA metadata = {};
    metadata.usMajorVersion = 1;
    metadata.usMinorVersion = 0;
    metadata.usBuildNumber = 0;
    metadata.usRevisionNumber = 0;

    refs.m_AssemblyRef = assemblyEmit.DefineAssemblyRef({ c_PublicKeyToken, sizeof c_PublicKeyToken }, GetSupportAssemblyName(), metadata, {});
    refs.m_Instrument = emit.DefineTypeRefByName(refs.m_AssemblyRef, L"ReactivityProfiler.Support.Instrument");

    // Only need the references to Calling/Argument/Returned if there's something to instrument
    // in the module.
    if (perModuleData.m_referencesObservableTypes)
    {
        static auto c_callingMethodSig = CreateInstrumentCallingSig();
        refs.m_Calling = emit.DefineMemberRef({
            refs.m_Instrument,
            L"Calling",
            c_callingMethodSig
            });

        auto argumentMethodSig = CreateInstrumentArgumentSig(perModuleData.m_observableTypeRefs);
        refs.m_Argument = emit.DefineMemberRef({
            refs.m_Instrument,
            L"Argument",
            argumentMethodSig
            });

        auto returnedMethodSig = CreateInstrumentReturnedSig(perModuleData.m_observableTypeRefs);
        refs.m_Returned = emit.DefineMemberRef({
            refs.m_Instrument,
            L"Returned",
            returnedMethodSig
            });

        mdTypeRef rtTypeHandleToken = emit.DefineTypeRefByName(mscorlib, L"System.RuntimeTypeHandle");
        auto returnedSubinterfaceMethodSig = CreateInstrumentReturnedSubinterfaceSig(perModuleData.m_observableTypeRefs, rtTypeHandleToken);
        refs.m_ReturnedSubinterface = emit.DefineMemberRef({
            refs.m_Instrument,
            L"ReturnedSubinterface",
            returnedSubinterfaceMethodSig
            });
    }

    // Add a module initializer that calls EnsureHandler (see InstallAssemblyResolutionHandler below)
    // and Instrument.EnsureInitialised.
    // TODO - cope if one already exists (unlikely but you never know)
    ATLTRACE("Adding module initializer");
    std::function<void(mdMethodDef token, InstructionList & builder, const SignatureBlob & sig)> SetMethodBody = [&](mdMethodDef token, InstructionList& instructions, const SignatureBlob& sig) {
        Method builder;
        if (instructions.size() > 0)
        {
            builder.InsertInstructionsAtOffset(0, instructions);
        }

        if (sig)
        {
            mdSignature sigToken = emit.GetTokenFromSig(sig);
            builder.SetLocalsSignature(sigToken);
        }

#ifdef DEBUG
        builder.DumpIL(true);
#endif

        DWORD size = builder.GetMethodSize();

        // buffer is owned by the runtime, we don't need to free it
        auto buffer = m_profilerInfo.AllocateFunctionBody(moduleId, size);
        builder.WriteMethod(reinterpret_cast<IMAGE_COR_ILMETHOD*>(buffer.begin()));

        m_profilerInfo.SetILFunctionBody(moduleId, token, buffer);
    };

    auto voidVoidSig = MethodSignatureWriter::WriteStatic(0, [](MethodSignatureWriter w) { w.SetVoidReturn(); });

    // Need a separate method to call Instrument.EnsureInitialised as the assembly resolution
    // stuff needs to have been called before the call to EnsureInitialised is JITted.
    mdMethodDef callEnsureInitialisedMethod = emit.DefineMethod({
        mdTokenNil,
        L"RxProfiler.EnsureInitialisedProxy8D4F8AEF610C454CA3AD2F486B21285D",
        mdPrivate | mdStatic | mdHideBySig,
        voidVoidSig
        });

    InstructionList ensureInitInstr;
#define Instr(...) ensureInitInstr.push_back(std::make_unique<Instruction>(__VA_ARGS__))
    mdMemberRef ensureHandler = emit.DefineMemberRef({
        refs.m_Instrument,
        L"EnsureInitialised",
        voidVoidSig
        });

    Instr(CEE_CALL, ensureHandler);
#undef Instr

    SetMethodBody(callEnsureInitialisedMethod, ensureInitInstr, SignatureBlob());

    mdMethodDef moduleInitMethod = emit.DefineMethod({
        mdTokenNil,
        L".cctor",
        mdPrivate | mdStatic | mdHideBySig | mdSpecialName | mdRTSpecialName,
        voidVoidSig
        });

    InstructionList moduleInitInstr;
#define Instr(...) moduleInitInstr.push_back(std::make_unique<Instruction>(__VA_ARGS__))

    // Assembly resolution handler stuff is Framework-only (can't get it to work in Core so using startup hook instead)
    if (!m_runtimeInfo.isCore)
    {
        mdTypeRef supportAssemblyResolution = emit.DefineTypeRefByName(mscorlib, L"RxProfiler.SupportAssemblyResolution");

        mdMemberRef ensureHandler = emit.DefineMemberRef({
            supportAssemblyResolution,
            L"EnsureHandler",
            voidVoidSig
            });

        Instr(CEE_CALL, ensureHandler);
    }

    Instr(CEE_CALL, callEnsureInitialisedMethod);

#undef Instr

    SetMethodBody(moduleInitMethod, moduleInitInstr, SignatureBlob());
}

void CRxProfiler::InstallAssemblyResolutionHandler(ModuleID hostModuleId)
{
    ATLTRACE("InstallAssemblyResolutionHandler");

    // This method injects a class equivalent to the following into the host module.
    //
    //     static class DummySupportAssemblyResolution
    //     {
    //         [SecuritySafeCritical]
    //         static DummySupportAssemblyResolution()
    //         {
    //             AppDomain.CurrentDomain.AssemblyResolve += ResolveSupportAssembly;
    //         }
    //     
    //         public static void EnsureHandler() { }
    //     
    //         private static Assembly ResolveSupportAssembly(object sender, ResolveEventArgs args)
    //         {
    //             var name = new AssemblyName(args.Name);
    //             string path = "base path\\" + name.Name + ".dll";
    //             if (File.Exists(path))
    //             {
    //                 return Assembly.LoadFrom(path);
    //             }
    //     
    //             return null;
    //         }
    //     }

    CMetadataImport metadataImport = m_profilerInfo.GetMetadataImport(hostModuleId, ofRead);
    CMetadataEmit metadataEmit = m_profilerInfo.GetMetadataEmit(hostModuleId, ofRead | ofWrite);
    CMetadataAssemblyImport metadataAssemblyImport = m_profilerInfo.GetMetadataAssemblyImport(hostModuleId, ofRead);

    std::function<void(mdMethodDef token, InstructionList& builder, const SignatureBlob& sig)> SetMethodBody = [&](mdMethodDef token, InstructionList& instructions, const SignatureBlob& sig) {
        Method builder;
        if (instructions.size() > 0)
        {
            builder.InsertInstructionsAtOffset(0, instructions);
        }

        if (sig)
        {
            mdSignature sigToken = metadataEmit.GetTokenFromSig(sig);
            builder.SetLocalsSignature(sigToken);
        }

#ifdef DEBUG
        builder.DumpIL(true);
#endif
        
        DWORD size = builder.GetMethodSize();

        // buffer is owned by the runtime, we don't need to free it
        auto buffer = m_profilerInfo.AllocateFunctionBody(hostModuleId, size);
        builder.WriteMethod(reinterpret_cast<IMAGE_COR_ILMETHOD*>(buffer.begin()));

        m_profilerInfo.SetILFunctionBody(hostModuleId, token, buffer);
    };

    std::function<mdToken(const std::wstring & typeName)> GetTypeToken = [&](const std::wstring& typeName) {
        mdTypeDef typeDefToken;
        if (metadataImport.TryFindTypeDef(typeName, mdTokenNil, typeDefToken))
        {
            return typeDefToken;
        }

        RELTRACE(L"Could not find type definition: %s", typeName.c_str());
        throw std::domain_error("Could not find type definition");
    };

    std::function<mdToken(const std::wstring & typeName, const std::wstring & methodName, const SignatureBlob& sig)> GetMethodTokenSig = [&](const std::wstring& typeName, const std::wstring& methodName, const SignatureBlob& sig) {
        mdToken typeToken = GetTypeToken(typeName);
        mdMethodDef methodDefToken;
        if (!metadataImport.TryFindMethod(GetTypeToken(typeName), methodName, sig, methodDefToken))
        {
            RELTRACE(L"Could not find method definition: %s::%s (with sig)", typeName.c_str(), methodName.c_str());
            throw std::domain_error("Could not find method definition");
        }
        return methodDefToken;
    };

    std::function<mdToken(const std::wstring & typeName, const std::wstring & methodName)> GetMethodToken = [&](const std::wstring& typeName, const std::wstring& methodName) {
        return GetMethodTokenSig(typeName, methodName, SignatureBlob());
    };

    mdToken mdObjectType = GetTypeToken(L"System.Object");

    mdTypeDef handlerClass = metadataEmit.DefineTypeDef({
            L"RxProfiler.SupportAssemblyResolution",
            tdPublic | tdAbstract | tdSealed, // static class
            mdObjectType
        }, simplespan<mdToken>());

    // EnsureHandler method does nothing first call will force type initializer to run
    auto voidVoidSig = MethodSignatureWriter::WriteStatic(0, [](MethodSignatureWriter w) { w.SetVoidReturn(); });
    mdMethodDef ensureHandlerMethod = metadataEmit.DefineMethod({
        handlerClass,
        L"EnsureHandler",
        mdPublic | mdStatic | mdHideBySig,
        voidVoidSig
        });
    InstructionList ensureHandlerInstrs; // body defaults to a simple RET
    SetMethodBody(ensureHandlerMethod, ensureHandlerInstrs, SignatureBlob());

    // HandleAssemblyResolve
    mdToken assemblyClass = GetTypeToken(L"System.Reflection.Assembly");
    auto handlerSig = MethodSignatureWriter::WriteStatic(2, [&](MethodSignatureWriter w) {
        w.WriteParam().SetSimpleClass(assemblyClass);
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_OBJECT);
        w.WriteParam().SetSimpleClass(GetTypeToken(L"System.ResolveEventArgs"));
    });
    mdMethodDef handleAssemblyResolveMethod = metadataEmit.DefineMethod({
        handlerClass,
        L"HandleAssemblyResolve",
        mdPrivate | mdStatic | mdHideBySig,
        handlerSig
        });
    auto instVoidStringSig = MethodSignatureWriter::WriteInstance(1, [&](MethodSignatureWriter w) {
        w.SetVoidReturn();
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_STRING);
    });
    auto instStringSig = MethodSignatureWriter::WriteInstance(0, [&](MethodSignatureWriter w) {
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_STRING);
    });
    auto statString3StringsSig = MethodSignatureWriter::WriteStatic(3, [&](MethodSignatureWriter w) {
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_STRING);
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_STRING);
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_STRING);
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_STRING);
    });
    auto statBoolStringSig = MethodSignatureWriter::WriteStatic(1, [&](MethodSignatureWriter w) {
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_BOOLEAN);
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_STRING);
    });
    auto statAssemblyStringSig = MethodSignatureWriter::WriteStatic(1, [&](MethodSignatureWriter w) {
        w.WriteParam().SetSimpleClass(assemblyClass);
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_STRING);
    });
    InstructionList handleAssemblyResolveInstrs;
#define Instr(...)    handleAssemblyResolveInstrs.push_back(std::make_unique<Instruction>(__VA_ARGS__))
    Instr(CEE_LDARG_1);
    Instr(CEE_CALLVIRT, GetMethodToken(L"System.ResolveEventArgs", L"get_Name"));
    Instr(CEE_NEWOBJ, GetMethodTokenSig(L"System.Reflection.AssemblyName", L".ctor", instVoidStringSig));
    Instr(CEE_STLOC_0);
    Instr(CEE_LDSTR, metadataEmit.DefineString(m_supportAssemblyFolder));
    Instr(CEE_LDLOC_0);
    Instr(CEE_CALLVIRT, GetMethodTokenSig(L"System.Reflection.AssemblyName", L"get_Name", instStringSig));
    Instr(CEE_LDSTR, metadataEmit.DefineString(L".dll"));
    Instr(CEE_CALL, GetMethodTokenSig(L"System.String", L"Concat", statString3StringsSig));
    Instr(CEE_STLOC_1);
    Instr(CEE_LDLOC_1);
    Instr(CEE_CALL, GetMethodTokenSig(L"System.IO.File", L"Exists", statBoolStringSig));
    Instr(CEE_STLOC_2);
    Instr(CEE_LDLOC_2);
    Instr(CEE_BRFALSE, 12); // -> L1
    Instr(CEE_LDLOC_1);
    Instr(CEE_CALL, GetMethodTokenSig(L"System.Reflection.Assembly", L"LoadFrom", statAssemblyStringSig));
    Instr(CEE_STLOC_3);
    Instr(CEE_BR, 2); // -> L2
    Instr(CEE_LDNULL); // L1
    Instr(CEE_STLOC_3);
    Instr(CEE_LDLOC_3); // L2
#undef Instr
    SetMethodBody(handleAssemblyResolveMethod, handleAssemblyResolveInstrs, LocalsSignatureWriter::MakeSig(4, [&](LocalsSignatureWriter& w) {
        w.WriteLocal().SetSimpleClass(GetTypeToken(L"System.Reflection.AssemblyName"));
        w.WriteLocal().SetPrimitiveKind(ELEMENT_TYPE_STRING);
        w.WriteLocal().SetPrimitiveKind(ELEMENT_TYPE_BOOLEAN);
        w.WriteLocal().SetSimpleClass(GetTypeToken(L"System.Reflection.Assembly"));
    }));

    // Type initializer
    mdMethodDef typeInitMethod = metadataEmit.DefineMethod({
        handlerClass,
        L".cctor",
        mdPrivate | mdStatic | mdHideBySig | mdSpecialName | mdRTSpecialName,
        voidVoidSig
        });
    auto instVoidObjNativeIntSig = MethodSignatureWriter::WriteInstance(2, [&](MethodSignatureWriter w) {
        w.SetVoidReturn();
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_OBJECT);
        w.WriteParam().SetPrimitiveKind(ELEMENT_TYPE_I);
    });
    InstructionList typeInitInstrs;
#define Instr(...) typeInitInstrs.push_back(std::make_unique<Instruction>(__VA_ARGS__))
    Instr(CEE_CALL, GetMethodToken(L"System.AppDomain", L"get_CurrentDomain"));
    Instr(CEE_LDNULL);
    Instr(CEE_LDFTN, handleAssemblyResolveMethod);
    Instr(CEE_NEWOBJ, GetMethodTokenSig(L"System.ResolveEventHandler", L".ctor", instVoidObjNativeIntSig));
    Instr(CEE_CALLVIRT, GetMethodToken(L"System.AppDomain", L"add_AssemblyResolve"));
#undef Instr
    SetMethodBody(typeInitMethod, typeInitInstrs, SignatureBlob());

    mdTypeDef securitySafeCriticalAttributeType = GetMethodToken(L"System.Security.SecuritySafeCriticalAttribute", L".ctor");
    std::vector<byte> attributeNoArgs = { 0x01, 0x00, 0x00, 0x00 };

    metadataEmit.DefineCustomAttribute(typeInitMethod, securitySafeCriticalAttributeType, attributeNoArgs);
}
