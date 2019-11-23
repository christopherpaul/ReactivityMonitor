#include "pch.h"
#include "RxProfiler.h"
#include "RxProfilerImpl.h"
#include "dllmain.h"
#include "Signature.h"

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

    return thisDllPath.substr(0, thisDllPath.find_last_of(L'\\') + 1) + GetSupportAssemblyName() + L".dll";
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

void CRxProfiler::AddSupportAssemblyReference(ModuleID moduleId, const ObservableTypeReferences& observableRefs, SupportAssemblyReferences& refs)
{
    static const byte c_PublicKeyToken[] = { 0xa8, 0xb3, 0x93, 0x07, 0x28, 0x3e, 0x56, 0x3a };

    CMetadataAssemblyEmit assemblyEmit = m_profilerInfo.GetMetadataAssemblyEmit(moduleId, ofRead | ofWrite);
    CMetadataEmit emit = m_profilerInfo.GetMetadataEmit(moduleId, ofRead | ofWrite);

    ASSEMBLYMETADATA metadata = {};
    metadata.usMajorVersion = 1;
    metadata.usMinorVersion = 0;
    metadata.usBuildNumber = 0;
    metadata.usRevisionNumber = 0;

    refs.m_AssemblyRef = assemblyEmit.DefineAssemblyRef({ c_PublicKeyToken, sizeof c_PublicKeyToken }, GetSupportAssemblyName(), metadata, {});
    refs.m_Instrument = emit.DefineTypeRefByName(refs.m_AssemblyRef, L"ReactivityProfiler.Support.Instrument");

    static auto c_callingMethodSig = CreateInstrumentCallingSig();
    refs.m_Calling = emit.DefineMemberRef({
        refs.m_Instrument,
        L"Calling",
        c_callingMethodSig
        });

    auto returnedMethodSig = CreateInstrumentReturnedSig(observableRefs);
    refs.m_Argument = emit.DefineMemberRef({
        refs.m_Instrument,
        L"Argument",
        returnedMethodSig
        });
    refs.m_Returned = emit.DefineMemberRef({
        refs.m_Instrument,
        L"Returned",
        returnedMethodSig
        });
}
