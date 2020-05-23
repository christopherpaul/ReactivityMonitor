#pragma once

using namespace ATL;

class HostInteraction
{
public:
    HostInteraction(const std::wstring& runtimeVersion);

    DWORD ExecuteInDefaultAppDomain(const std::wstring& assemblyPath, const std::wstring& typeName, const std::wstring& methodName, const std::wstring& stringArg);

private:
    CComPtr<ICLRRuntimeInfo> m_pClrRuntimeInfo;
    CComPtr<ICLRRuntimeHost> m_pClrRuntimeHost;
};
