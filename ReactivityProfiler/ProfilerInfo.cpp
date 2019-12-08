#include "pch.h"
#include "ProfilerInfo.h"

void CProfilerInfo::Set(IUnknown* profilerInfo)
{
    m_profilerInfo = profilerInfo;
    if (profilerInfo && !m_profilerInfo)
    {
        RELTRACE("Required profiling interface not available");
        throw E_FAIL;
    }
}

ModuleInfo CProfilerInfo::GetModuleInfo(ModuleID moduleId)
{
    ModuleInfo info;
    ULONG nameCount;
    CHECK_SUCCESS(m_profilerInfo->GetModuleInfo(moduleId, &info.baseLoadAddress, 0, &nameCount, nullptr, &info.assemblyId));
    std::vector<WCHAR> nameChars(nameCount);
    CHECK_SUCCESS(m_profilerInfo->GetModuleInfo(moduleId, &info.baseLoadAddress, nameCount, &nameCount, nameChars.data(), &info.assemblyId));

    info.name = std::wstring(nameChars.data(), nameCount - 1);

    return info;
}

FunctionInfo CProfilerInfo::GetFunctionInfo(FunctionID functionId)
{
    FunctionInfo info;
    CHECK_SUCCESS(m_profilerInfo->GetFunctionInfo(functionId, &info.classId, &info.moduleId, &info.functionToken));
    return info;
}

// Note: returns null span if it's not an IL function
simplespan<const byte> CProfilerInfo::GetILFunctionBody(ModuleID moduleId, mdMethodDef methodToken)
{
    const byte* data;
    ULONG size;

    HRESULT hr = m_profilerInfo->GetILFunctionBody(moduleId, methodToken, &data, &size);
    if (hr == CORPROF_E_FUNCTION_NOT_IL)
    {
        return {};
    }
    CHECK_SUCCESS_MSG(hr, "GetILFunctionBody");

    return { data, size };
}

simplespan<byte> CProfilerInfo::AllocateFunctionBody(ModuleID moduleId, size_t size)
{
    CComPtr<IMethodMalloc> allocator;
    CHECK_SUCCESS(m_profilerInfo->GetILFunctionBodyAllocator(moduleId, &allocator));
    
    return {
        static_cast<byte *>(allocator->Alloc(static_cast<ULONG>(size))),
        size
    };
}

void CProfilerInfo::SetILFunctionBody(ModuleID moduleId, mdMethodDef methodToken, const simplespan<byte>& body)
{
    CHECK_SUCCESS(m_profilerInfo->SetILFunctionBody(
        moduleId,
        methodToken,
        body.begin()
    ));
}

void CProfilerInfo::SetILInstrumentedCodeMap(FunctionID functionId, bool isFirstCallForFunc, const simplespan<COR_IL_MAP>& map)
{
    CHECK_SUCCESS(m_profilerInfo->SetILInstrumentedCodeMap(
        functionId,
        isFirstCallForFunc,
        static_cast<ULONG>(map.length()),
        map.begin()
    ));
}

CMetadataImport CProfilerInfo::GetMetadataImport(ModuleID moduleId, DWORD openFlags)
{
    IUnknown* metadataImport;
    CHECK_SUCCESS(m_profilerInfo->GetModuleMetaData(moduleId, openFlags, IID_IMetaDataImport2, &metadataImport));

    return metadataImport;
}

CMetadataAssemblyImport CProfilerInfo::GetMetadataAssemblyImport(ModuleID moduleId, DWORD openFlags)
{
    IUnknown* metadataImport;
    CHECK_SUCCESS(m_profilerInfo->GetModuleMetaData(moduleId, openFlags, IID_IMetaDataAssemblyImport, &metadataImport));

    return metadataImport;
}

CMetadataAssemblyEmit CProfilerInfo::GetMetadataAssemblyEmit(ModuleID moduleId, DWORD openFlags)
{
    IUnknown* metadataAssemblyEmit;
    CHECK_SUCCESS(m_profilerInfo->GetModuleMetaData(moduleId, openFlags, IID_IMetaDataAssemblyEmit, &metadataAssemblyEmit));

    return metadataAssemblyEmit;
}

CMetadataEmit CProfilerInfo::GetMetadataEmit(ModuleID moduleId, DWORD openFlags)
{
    IUnknown* metadataEmit;
    CHECK_SUCCESS(m_profilerInfo->GetModuleMetaData(moduleId, openFlags, IID_IMetaDataEmit2, &metadataEmit));

    return metadataEmit;
}

CCorEnum<IMetaDataImport2, mdModuleRef> CMetadataImport::EnumModuleRefs() const
{
    CCorEnum<IMetaDataImport2, mdModuleRef> e(m_metadata.p, [=](auto imp, auto e, auto arr, auto c, auto pc) { return imp->EnumModuleRefs(e, arr, c, pc); });
    return e;
}

bool CMetadataImport::TryFindTypeDef(const std::wstring& name, mdToken enclosingTypeToken, mdTypeDef& typeDef)
{
    return SUCCEEDED(m_metadata->FindTypeDefByName(name.c_str(), enclosingTypeToken, &typeDef));
}

bool CMetadataImport::TryFindTypeRef(mdToken scope, const std::wstring& name, mdTypeRef& typeRef) const
{
    return SUCCEEDED(m_metadata->FindTypeRef(scope, name.c_str(), &typeRef));
}

bool CMetadataImport::TryFindMethod(mdTypeDef typeToken, const std::wstring& name, const SignatureBlob& sigBlob, mdMethodDef& methodDef) const
{
    return SUCCEEDED(m_metadata->FindMethod(typeToken, name.c_str(), sigBlob.begin(), static_cast<ULONG>(sigBlob.length()), &methodDef));
}

MethodProps CMetadataImport::GetMethodProps(mdMethodDef methodDefToken) const
{
    MethodProps props;
    ULONG nameLength;
    const COR_SIGNATURE* pSigBlob;
    ULONG sigBlobSize;
    CHECK_SUCCESS(m_metadata->GetMethodProps(
        methodDefToken,
        &props.classDefToken,
        nullptr,
        0,
        &nameLength,
        &props.attrFlags,
        &pSigBlob,
        &sigBlobSize,
        &props.codeRva,
        &props.implFlags
    ));

    std::vector<wchar_t> nameChars(nameLength);
    CHECK_SUCCESS(m_metadata->GetMethodProps(
        methodDefToken,
        &props.classDefToken,
        nameChars.data(),
        nameLength,
        &nameLength,
        &props.attrFlags,
        &pSigBlob,
        &sigBlobSize,
        &props.codeRva,
        &props.implFlags
    ));

    props.name = std::wstring(nameChars.data(), nameChars.size() - 1);
    props.sigBlob = { pSigBlob, sigBlobSize };
    return props;
}

MemberRefProps CMetadataImport::GetMemberRefProps(mdMemberRef memberRefToken) const
{
    MemberRefProps props;
    ULONG nameLength;
    const COR_SIGNATURE* pSigBlob;
    ULONG sigBlobSize;

    CHECK_SUCCESS(m_metadata->GetMemberRefProps(
        memberRefToken,
        &props.declToken,
        nullptr,
        0,
        &nameLength,
        &pSigBlob,
        &sigBlobSize));

    std::vector<wchar_t> nameChars(nameLength);
    CHECK_SUCCESS(m_metadata->GetMemberRefProps(
        memberRefToken,
        &props.declToken,
        nameChars.data(),
        nameLength,
        &nameLength,
        &pSigBlob,
        &sigBlobSize));

    props.name = { nameChars.data(), nameChars.size() - 1 };
    props.sigBlob = { pSigBlob, sigBlobSize };
    return props;
}

MethodSpecProps CMetadataImport::GetMethodSpecProps(mdMethodSpec methodSpecToken) const
{
    const COR_SIGNATURE* pSigBlob;
    ULONG sigBlobSize;
    mdToken parentToken;

    CHECK_SUCCESS(m_metadata->GetMethodSpecProps(
        methodSpecToken,
        &parentToken,
        &pSigBlob,
        &sigBlobSize));

    return {
        parentToken,
        { pSigBlob, sigBlobSize }
    };
}

TypeDefProps CMetadataImport::GetTypeDefProps(mdTypeDef typeDefToken) const
{
    ULONG nameLength;
    DWORD attrFlags;
    mdToken extendsTypeToken;

    CHECK_SUCCESS(m_metadata->GetTypeDefProps(
        typeDefToken,
        nullptr,
        0,
        &nameLength,
        &attrFlags,
        &extendsTypeToken));

    std::vector<wchar_t> nameChars(nameLength);
    CHECK_SUCCESS(m_metadata->GetTypeDefProps(
        typeDefToken,
        nameChars.data(),
        nameLength,
        &nameLength,
        &attrFlags,
        &extendsTypeToken));

    return {
        { nameChars.data(), nameChars.size() - 1 },
        attrFlags,
        extendsTypeToken
    };
}

mdTypeDef CMetadataImport::GetParentTypeDef(mdTypeDef nestedTypeDefToken) const
{
    mdTypeDef parentToken;
    CHECK_SUCCESS(m_metadata->GetNestedClassProps(nestedTypeDefToken, &parentToken));

    return parentToken;
}

SignatureBlob CMetadataImport::GetTypeSpecFromToken(mdTypeSpec typeSpecToken) const
{
    const COR_SIGNATURE* pSigBlob;
    ULONG sigBlobSize;

    CHECK_SUCCESS(m_metadata->GetTypeSpecFromToken(
        typeSpecToken,
        &pSigBlob,
        &sigBlobSize));

    return { pSigBlob, sigBlobSize };
}

SignatureBlob CMetadataImport::GetSigFromToken(mdSignature sigTok) const
{
    const COR_SIGNATURE* pSigBlob;
    ULONG sigBlobSize;

    CHECK_SUCCESS(m_metadata->GetSigFromToken(
        sigTok,
        &pSigBlob,
        &sigBlobSize));

    return { pSigBlob, sigBlobSize };
}

mdModule CMetadataImport::GetCurrentModule() const
{
    mdModule token;
    CHECK_SUCCESS(m_metadata->GetModuleFromScope(&token));
    return token;
}

CCorEnum<IMetaDataAssemblyImport, mdAssemblyRef> CMetadataAssemblyImport::EnumAssemblyRefs()
{
    CCorEnum<IMetaDataAssemblyImport, mdAssemblyRef> e(m_metadata.p, [=](auto imp, auto e, auto arr, auto c, auto pc) { return imp->EnumAssemblyRefs(e, arr, c, pc); });
    return e;
}

mdAssembly CMetadataAssemblyImport::GetCurrentAssembly()
{
    mdAssembly token;
    CHECK_SUCCESS(m_metadata->GetAssemblyFromScope(&token));
    return token;
}

AssemblyProps CMetadataAssemblyImport::GetAssemblyProps(mdAssembly assemblyToken)
{
    if (assemblyToken == mdTokenNil)
    {
        assemblyToken = GetCurrentAssembly();
    }

    ULONG nameLength = 0;
    AssemblyProps props;
    const void* pk = nullptr;
    ULONG pkSize = 0;
    CHECK_SUCCESS(m_metadata->GetAssemblyProps(
        assemblyToken,
        &pk,
        &pkSize,
        &props.hashAlgId,
        nullptr,
        0,
        &nameLength,
        &props.metadata,
        &props.flags
    ));

    std::vector<wchar_t> nameChars(nameLength);
    CHECK_SUCCESS(m_metadata->GetAssemblyProps(
        assemblyToken,
        &pk,
        &pkSize,
        &props.hashAlgId,
        nameChars.data(),
        nameLength,
        &nameLength,
        &props.metadata,
        &props.flags
    ));

    props.publicKey = { static_cast<const byte*>(pk), pkSize };
    props.name = { nameChars.data(), nameChars.size() };

    return props;
}

mdAssemblyRef CMetadataAssemblyEmit::DefineAssemblyRef(const simplespan<const byte>& publicKeyOrToken, const std::wstring& name, const ASSEMBLYMETADATA& metadata, const simplespan<const byte>& hash, CorAssemblyFlags flags)
{
    mdAssemblyRef token;
    CHECK_SUCCESS(m_metadata->DefineAssemblyRef(
        publicKeyOrToken.begin(),
        static_cast<ULONG>(publicKeyOrToken.length()),
        name.c_str(),
        &metadata,
        hash.begin(),
        static_cast<ULONG>(hash.length()),
        flags,
        &token
    ));

    ATLTRACE(L"DefineAssemblyRef: %s -> %x", name.c_str(), token);

    return token;
}

mdTypeRef CMetadataEmit::DefineTypeRefByName(mdToken scope, const std::wstring& typeName)
{
    mdTypeRef token;
    CHECK_SUCCESS(m_metadata->DefineTypeRefByName(
        scope,
        typeName.c_str(),
        &token
    ));

    ATLTRACE(L"DefineTypeRefByName: %x %s -> %x", scope, typeName.c_str(), token);

    return token;
}

mdMemberRef CMetadataEmit::DefineMemberRef(const MemberRefProps& props)
{
    mdMemberRef token;
    CHECK_SUCCESS(m_metadata->DefineMemberRef(
        props.declToken,
        props.name.c_str(),
        props.sigBlob.begin(),
        static_cast<ULONG>(props.sigBlob.length()),
        &token
    ));

    ATLTRACE(L"DefineMemberRef: %x %s -> %x", props.declToken, props.name.c_str(), token);

    return token;
}

mdMethodSpec CMetadataEmit::DefineMethodSpec(const MethodSpecProps& props)
{
    mdMethodSpec token;
    CHECK_SUCCESS(m_metadata->DefineMethodSpec(
        props.genericMethodToken,
        props.sigBlob.begin(),
        static_cast<ULONG>(props.sigBlob.length()),
        &token
    ));

    ATLTRACE(L"DefineMethodSpec: %x -> %x", props.genericMethodToken, token);

    return token;
}

mdSignature CMetadataEmit::GetTokenFromSig(const SignatureBlob& sigBlob)
{
    mdSignature token;
    CHECK_SUCCESS(m_metadata->GetTokenFromSig(
        sigBlob.begin(),
        static_cast<ULONG>(sigBlob.length()),
        &token
    ));

    ATLTRACE(L"GetTokenFromSig: %x", token);

    return token;
}

mdTypeSpec CMetadataEmit::DefineTypeSpec(const SignatureBlob& sigBlob)
{
    mdTypeSpec token;
    CHECK_SUCCESS(m_metadata->GetTokenFromTypeSpec(
        sigBlob.begin(),
        static_cast<ULONG>(sigBlob.length()),
        &token
    ));

    ATLTRACE(L"GetTokenFromTypeSpec: %x", token);

    return token;
}

mdTypeDef CMetadataEmit::DefineTypeDef(const TypeDefProps& props, const simplespan<mdToken>& interfaces)
{
    mdTypeDef token;
    std::vector<mdToken> interfacesWithTerminator;
    if (interfaces)
    {
        std::copy(interfaces.begin(), interfaces.end(), std::back_inserter(interfacesWithTerminator));
    }
    interfacesWithTerminator.push_back(mdTokenNil);
    
    CHECK_SUCCESS(m_metadata->DefineTypeDef(
        props.name.c_str(),
        props.attrFlags,
        props.extendsTypeToken,
        interfacesWithTerminator.data(),
        &token));

    return token;
}

mdMethodDef CMetadataEmit::DefineMethod(const MethodProps& props)
{
    mdMethodDef token;
    CHECK_SUCCESS(m_metadata->DefineMethod(
        props.classDefToken,
        props.name.c_str(),
        props.attrFlags,
        props.sigBlob.begin(),
        static_cast<ULONG>(props.sigBlob.length()),
        props.codeRva,
        props.implFlags,
        &token));

    ATLTRACE(L"DefineMethod: %s -> %x", props.name.c_str(), token);

    return token;
}

mdString CMetadataEmit::DefineString(const std::wstring& s)
{
    mdString token;
    CHECK_SUCCESS(m_metadata->DefineUserString(
        s.c_str(),
        static_cast<ULONG>(s.length()),
        &token
    ));

    return token;
}

mdCustomAttribute CMetadataEmit::DefineCustomAttribute(mdToken owner, mdToken attributeType, const simplespan<const byte>& attrData)
{
    mdCustomAttribute token;
    CHECK_SUCCESS(m_metadata->DefineCustomAttribute(
        owner,
        attributeType,
        attrData.begin(),
        static_cast<ULONG>(attrData.length()),
        &token));

    return token;
}
