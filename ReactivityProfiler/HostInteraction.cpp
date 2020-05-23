#include "pch.h"
#include "HostInteraction.h"

#pragma comment(lib, "mscoree.lib")

HostInteraction::HostInteraction(const std::wstring& runtimeVersion)
{
    CComPtr<ICLRMetaHost> pMetaHost;
    CHECK_SUCCESS(CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (LPVOID*)&pMetaHost));
    CHECK_SUCCESS(pMetaHost->GetRuntime(runtimeVersion.c_str(), IID_ICLRRuntimeInfo, (LPVOID*)&m_pClrRuntimeInfo));
    CHECK_SUCCESS(m_pClrRuntimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, (LPVOID*)&m_pClrRuntimeHost));
}

DWORD HostInteraction::ExecuteInDefaultAppDomain(const std::wstring& assemblyPath, const std::wstring& typeName, const std::wstring& methodName, const std::wstring& stringArg)
{
    DWORD result;
    CHECK_SUCCESS(m_pClrRuntimeHost->ExecuteInDefaultAppDomain(assemblyPath.c_str(), typeName.c_str(), methodName.c_str(), stringArg.c_str(), &result));
    return result;
}

