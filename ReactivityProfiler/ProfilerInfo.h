#pragma once

using namespace ATL;

template<typename Metadata, typename Token, size_t batchSize = 10>
class CCorEnum
{
public:
    typedef std::function<HRESULT(Metadata*, HCORENUM*, Token*, ULONG, ULONG*)> enumFunction_t;

private:
    class Impl
    {
    public:
        Impl(Metadata* metadata, const enumFunction_t& enumFunction) :
            m_metadata(metadata),
            m_enumFunction(enumFunction),
            m_enumHandle(0),
            m_vector(batchSize),
            m_countInVector(0),
            m_current(0)
        {
        }

        Impl(const Impl&) = delete;
        Impl& operator=(const Impl&) = delete;

        ~Impl()
        {
            if (m_enumHandle)
            {
                m_metadata->CloseEnum(m_enumHandle);
            }
        }

        size_t Count()
        {
            if (!m_enumHandle)
            {
                FetchBatch();
            }

            ULONG count;
            CHECK_SUCCESS(m_metadata->CountEnum(m_enumHandle.get(), &count));
            return count;
        }

        bool MoveNext()
        {
            if (!m_enumHandle)
            {
                FetchBatch();
            }
            else
            {
                m_current++;
            }

            if (m_current >= m_countInVector)
            {
                if (m_countInVector < batchSize)
                {
                    return false;
                }

                FetchBatch();
            }

            return m_current < m_countInVector;
        }

        Token Current() const
        {
            if (!m_enumHandle)
            {
                RELTRACE("Current called before MoveNext");
                throw E_FAIL;
            }
            else if (m_current >= m_countInVector)
            {
                RELTRACE("Current called after end of enumeration");
                throw E_FAIL;
            }

            return m_vector[m_current];
        }

    private:
        Metadata* const m_metadata;
        const enumFunction_t m_enumFunction;
        HCORENUM m_enumHandle;
        std::vector<Token> m_vector;
        int m_current;
        size_t m_countInVector;

        void FetchBatch()
        {
            ULONG count;
            CHECK_SUCCESS(m_enumFunction(m_metadata, &m_enumHandle, m_vector.data(), batchSize, &count));
            m_countInVector = count;
            m_current = 0;
        }
    };

public:
    CCorEnum(Metadata* metadata, const enumFunction_t& enumFunction) :
        m_pImpl(new Impl(metadata, enumFunction))
    {
    }

    CCorEnum(const CCorEnum& other) = delete;
    CCorEnum& operator=(const CCorEnum& other) = delete;

    CCorEnum(CCorEnum&& other) noexcept
    {
        std::swap(m_pImpl, other.m_pImpl);
    }

    size_t Count() { return m_pImpl->Count(); }
    bool MoveNext() { return m_pImpl->MoveNext(); }
    Token Current() { return m_pImpl->Current(); }

private:
    std::unique_ptr<Impl> m_pImpl;
};

struct ModuleInfo
{
    LPCBYTE baseLoadAddress = nullptr;
    AssemblyID assemblyId = 0;
    std::wstring name;
};

struct FunctionInfo
{
    ClassID classId = 0;
    ModuleID moduleId = 0;
    mdToken functionToken = 0;
};

struct MethodProps
{
    mdTypeDef classDefToken = 0;
    std::wstring name;
    DWORD attrFlags = 0;
    simplespan<const COR_SIGNATURE> sigBlob;
    ULONG codeRva = 0;
    DWORD implFlags = 0;
};

struct MemberRefProps
{
    mdToken declToken = 0; // token for declaring class/declaring module class/method def
    std::wstring name;
    simplespan<const COR_SIGNATURE> sigBlob;
};

struct MethodSpecProps
{
    mdToken genericMethodToken;
    simplespan<const COR_SIGNATURE> sigBlob;
};

struct AssemblyProps
{
    simplespan<const byte> publicKey;
    ULONG hashAlgId = 0;
    std::wstring name;
    ASSEMBLYMETADATA metadata = {};
    DWORD flags = 0;
};

class CMetadataImport
{
public:
    CMetadataImport(IUnknown* metadataImport = nullptr) : m_metadata(metadataImport)
    {
    }

    CCorEnum<IMetaDataImport2, mdModuleRef> EnumModuleRefs() const;

    bool TryFindTypeRef(mdToken scope, const std::wstring& name, mdTypeRef& typeRef) const;
    MethodProps GetMethodProps(mdMethodDef methodDefToken) const;
    MemberRefProps GetMemberRefProps(mdMemberRef memberRefToken) const;
    MethodSpecProps GetMethodSpecProps(mdMethodSpec methodSpecToken) const;
    mdModule GetCurrentModule() const;

    operator bool() const { return m_metadata; }

private:
    CComQIPtr<IMetaDataImport2, &IID_IMetaDataImport2> m_metadata;
};

class CMetadataAssemblyImport
{
public:
    CMetadataAssemblyImport(IUnknown* metadataAssemblyImport) : m_metadata(metadataAssemblyImport)
    {
    }

    CCorEnum<IMetaDataAssemblyImport, mdAssemblyRef> EnumAssemblyRefs();
    mdAssembly GetCurrentAssembly();
    AssemblyProps GetAssemblyProps(mdAssembly assemblyToken = mdTokenNil);

private:
    CComQIPtr<IMetaDataAssemblyImport, &IID_IMetaDataAssemblyImport> m_metadata;
};

class CMetadataAssemblyEmit
{
public:
    CMetadataAssemblyEmit(IUnknown* metadataAssemblyEmit) : m_metadata(metadataAssemblyEmit)
    {
    }

    mdAssemblyRef DefineAssemblyRef(
        const std::vector<byte>& publicKeyOrToken,
        const std::wstring& name,
        const ASSEMBLYMETADATA& metadata,
        const std::vector<byte>& hash,
        CorAssemblyFlags flags = afPA_None);

private:
    CComQIPtr<IMetaDataAssemblyEmit, &IID_IMetaDataAssemblyEmit> m_metadata;
};

class CProfilerInfo
{
public:
    void Set(IUnknown* profilerInfo);

    void SetEventMask(DWORD dwEventsLow, DWORD dwEventsHigh = COR_PRF_HIGH_MONITOR_NONE)
    {
        CHECK_SUCCESS(m_profilerInfo->SetEventMask2(dwEventsLow, dwEventsHigh));
    }

    ModuleInfo GetModuleInfo(ModuleID moduleId);
    FunctionInfo GetFunctionInfo(FunctionID functionId);
    simplespan<const byte> GetILFunctionBody(ModuleID moduleId, mdMethodDef methodToken);

    CMetadataImport GetMetadataImport(ModuleID moduleId, CorOpenFlags openFlags);
    CMetadataAssemblyImport GetMetadataAssemblyImport(ModuleID moduleId, CorOpenFlags openFlags);
    CMetadataAssemblyEmit GetMetadataAssemblyEmit(ModuleID moduleId, CorOpenFlags openFlags);

private:
    CComQIPtr<ICorProfilerInfo6> m_profilerInfo;
};
