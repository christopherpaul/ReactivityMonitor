#include "pch.h"
#include "RxProfiler.h"
#include "RxProfilerImpl.h"
#include <atlbase.h>

static const wchar_t* const c_Software = L"Software";
static const wchar_t* const c_ProductName = L"RxProfiler";
static const wchar_t* const c_Servers = L"Servers";
static const wchar_t* const c_PipeName = L"PipeName";

using namespace ATL;

static void OpenServersKey(CRegKey& servers)
{
    CRegKey software;
    CHECK_SUCCESS(software.Open(HKEY_CURRENT_USER, c_Software, KEY_READ | KEY_CREATE_SUB_KEY));

    CRegKey product;
    CHECK_SUCCESS(product.Create(software, c_ProductName, REG_NONE, REG_OPTION_VOLATILE, KEY_READ | KEY_CREATE_SUB_KEY));

    CHECK_SUCCESS(servers.Create(product, c_Servers, REG_NONE, REG_OPTION_VOLATILE, KEY_READ | KEY_CREATE_SUB_KEY | DELETE));
}

void OpenTransientRegistryKey(CRegKey& key)
{
    CRegKey servers;
    OpenServersKey(servers);

    DWORD procId = GetCurrentProcessId();
    std::wstringstream procIdStrm;
    procIdStrm << procId;

    CHECK_SUCCESS(key.Create(servers, procIdStrm.str().c_str(), REG_NONE, REG_OPTION_VOLATILE, KEY_READ | KEY_WRITE));
}

void RemoveTransientRegistryKey()
{
    CRegKey servers;
    OpenServersKey(servers);

    DWORD procId = GetCurrentProcessId();
    std::wstringstream procIdStrm;
    procIdStrm << procId;

    servers.DeleteSubKey(procIdStrm.str().c_str());
}

STDAPI_(void) SetChannelPipeName(const wchar_t* pszPipeName)
{
    CRegKey key;
    OpenTransientRegistryKey(key);

    if (pszPipeName != nullptr)
    {
        CHECK_SUCCESS(key.SetStringValue(c_PipeName, pszPipeName));
    }
    else
    {
        key.DeleteValue(c_PipeName);
    }
}
