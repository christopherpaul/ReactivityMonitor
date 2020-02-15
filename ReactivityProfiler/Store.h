#pragma once

class StoreImpl;

class Store
{
public:
    Store();

    void AddModuleInfo(
        ModuleID moduleId, 
        const std::wstring& modulePath, 
        const std::wstring& assemblyName);

    void AddMethodInfo(
        int32_t instrumentedMethodId,
        ModuleID moduleId,
        mdToken functionToken,
        const std::wstring& owningTypeName,
        const std::wstring& name);

    void AddInstrumentationInfo(
        int32_t instrumentationPoint, 
        int32_t instrumentedMethodId,
        int32_t instructionOffset,
        const std::wstring& calledMethodName);

    void MethodInstrumentationDone(int32_t instrumentedMethodId);

    int32_t GetEventCount();
    simplespan<byte> ReadEvent(int32_t index);

private:
    std::unique_ptr<StoreImpl> m_pImpl;
};

extern Store g_Store;
