// dllmain.cpp : Implementation of DllMain.

#include "pch.h"
#include "framework.h"
#include "resource.h"
#include "ReactivityProfiler_i.h"
#include "dllmain.h"

CReactivityProfilerModule _AtlModule;
HINSTANCE g_profilerModule;

// DLL Entry Point
extern "C" BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
	g_profilerModule = hInstance;
	return _AtlModule.DllMain(dwReason, lpReserved);
}
