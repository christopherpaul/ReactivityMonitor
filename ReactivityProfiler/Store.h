#pragma once

class StoreImpl;

class Store
{
public:
    Store();

    void AddModuleInfo(ModuleID moduleId, const std::wstring& modulePath);
    void AddInstrumentationInfo(
        int32_t instrumentationPoint, 
        ModuleID moduleId, 
        mdToken functionToken,
        int32_t instructionOffset,
        const std::wstring& calledMethodName);

    int32_t GetEventCount();
    simplespan<byte> ReadEvent(int32_t index);

private:
    std::unique_ptr<StoreImpl> m_pImpl;
};

extern Store g_Store;
