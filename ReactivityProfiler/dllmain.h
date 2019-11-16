// dllmain.h : Declaration of module class.

class CReactivityProfilerModule : public ATL::CAtlDllModuleT< CReactivityProfilerModule >
{
public :
	DECLARE_LIBID(LIBID_ReactivityProfilerLib)
	DECLARE_REGISTRY_APPID_RESOURCEID(IDR_REACTIVITYPROFILER, "{beb5385e-61a6-43aa-ad6e-02fd424fd82f}")
};

extern class CReactivityProfilerModule _AtlModule;
