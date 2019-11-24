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

    int32_t GetStoreLength();
    int32_t ReadStore(int32_t start, int32_t length, byte* buffer);

private:
    std::unique_ptr<StoreImpl> m_pImpl;
};

extern Store g_Store;
