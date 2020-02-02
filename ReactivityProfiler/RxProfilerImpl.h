#pragma once

struct ObservableTypeReferences
{
    mdTypeRef m_IObservable = 0;
    mdTypeRef m_IConnectableObservable = 0;
    mdTypeRef m_IGroupedObservable = 0;
};

struct SupportAssemblyReferences
{
    mdAssemblyRef m_AssemblyRef = 0;
    mdTypeRef m_Instrument = 0;
    mdMemberRef m_Argument = 0;
    mdMemberRef m_Calling = 0;
    mdMemberRef m_Returned = 0;
    mdMemberRef m_ReturnedSubinterface = 0;
};

struct RewrittenFunctionData
{
    simplespan<byte> m_rewrittenILBuffer;
    simplespan<COR_IL_MAP> m_instrumentedCodeMap;
};

struct PerModuleData
{
    std::mutex m_mutex;

    bool m_supportAssemblyReferenced = false;
    bool m_referencesObservableTypes = false;
    AssemblyProps m_assemblyProps;
    ObservableTypeReferences m_observableTypeRefs;
    SupportAssemblyReferences m_supportAssemblyRefs;
    std::unordered_map<mdToken, std::future<RewrittenFunctionData>> m_rewrittenFunctions;
};

extern const wchar_t* GetSupportAssemblyName();
extern std::wstring GetSupportAssemblyPath();
extern void RemoveTransientRegistryKey();
