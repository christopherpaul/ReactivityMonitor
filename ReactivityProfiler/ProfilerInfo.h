#pragma once

using namespace ATL;

template<typename Token, size_t batchSize = 10>
class CCorEnum
{
public:
    typedef std::function<HRESULT(IMetaDataImport2 *, HCORENUM*, Token*, ULONG, ULONG*)> enumFunction_t;

    CCorEnum(IMetaDataImport2* metadata, const enumFunction_t& enumFunction) :
        m_metadata(metadata),
        m_enumFunction(enumFunction),
        m_enumHandle(0),
        m_vector(batchSize),
        m_countInVector(0),
        m_current(0)
    {
    }

    ~CCorEnum()
    {
        if (m_enumHandle)
        {
            m_metadata->CloseEnum(m_enumHandle);
        }
    }

    CCorEnum(const CCorEnum& other) = delete;
    CCorEnum& operator=(const CCorEnum& other) = delete;

    CCorEnum(CCorEnum&& other) noexcept :
        m_metadata(other.m_metadata),
        m_enumFunction(other.m_enumFunction),
        m_enumHandle(0),
        m_vector(std::move(other.m_vector)),
        m_countInVector(other.m_countInVector),
        m_current(other.m_current)
    {
        std::swap(m_enumHandle, other.m_enumHandle);
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
    IMetaDataImport2* const m_metadata;
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

struct ModuleInfo
{
    LPCBYTE baseLoadAddress = nullptr;
    AssemblyID assemblyId = 0;
    std::wstring name;
};

class CMetadataImport
{
public:
    CMetadataImport(IUnknown* metadataImport) : m_metadata(metadataImport)
    {
    }

    CCorEnum<mdModule> EnumModuleRefs();

    bool TryFindTypeRef(mdToken scope, const std::wstring& name, mdTypeRef& typeRef);

private:
    CComQIPtr<IMetaDataImport2, &IID_IMetaDataImport2> m_metadata;
};

class CProfilerInfo
{
public:
    void Set(IUnknown* profilerInfo);

    void SetEventMask(COR_PRF_MONITOR dwEventsLow, COR_PRF_HIGH_MONITOR dwEventsHigh = COR_PRF_HIGH_MONITOR_NONE)
    {
        CHECK_SUCCESS(m_profilerInfo->SetEventMask2(dwEventsLow, dwEventsHigh));
    }

    ModuleInfo GetModuleInfo(ModuleID moduleId);

    CMetadataImport GetMetadataImport(ModuleID moduleId, CorOpenFlags openFlags);

private:
    CComQIPtr<ICorProfilerInfo5> m_profilerInfo;
};
