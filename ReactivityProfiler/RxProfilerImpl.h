#pragma once

struct ObservableTypeReferences
{
    mdTypeRef m_IObservable = 0;
};

struct SupportAssemblyReferences
{
    mdAssemblyRef m_AssemblyRef = 0;
    mdTypeRef m_Instrument = 0;
    mdMemberRef m_Argument = 0;
    mdMemberRef m_Calling = 0;
    mdMemberRef m_Returned = 0;
};

struct PerModuleData
{
    std::mutex m_mutex;

    bool m_supportAssemblyReferenced = false;
    AssemblyProps m_assemblyProps;
    ObservableTypeReferences m_observableTypeRefs;
    SupportAssemblyReferences m_supportAssemblyRefs;

    std::atomic_int32_t m_instrumentationPointSource = 0;
};

extern const wchar_t* GetSupportAssemblyName();
extern std::wstring GetSupportAssemblyPath();
extern bool IsExcludedAssembly(const AssemblyProps& assemblyProps);
