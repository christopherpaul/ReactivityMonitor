// This file originally taken from OpenCover project - see LICENSE_OPENCOVER
#pragma once

class CReleaseTrace
{
    public:
	    CReleaseTrace()
	    {
	}

	const char* PREFIX = "RxProfiler: ";
	const wchar_t* WPREFIX = L"RxProfiler: ";

#pragma warning(push)
#pragma warning(disable : 4793)
	void __cdecl operator()(
		const char *pszFmt, 
		...) const
	{		
		va_list ptr; va_start(ptr, pszFmt);
        size_t nBytes = _vscprintf(pszFmt, ptr);
        nBytes += 2;
        va_end(ptr);
				
		auto prefixLength = strlen(PREFIX);
		std::vector<char> buffer(nBytes + prefixLength);
		sprintf_s(&buffer[0], prefixLength + 1, "%s", PREFIX);

        va_start(ptr, pszFmt);
		_vsnprintf_s(&buffer[prefixLength], nBytes, nBytes - 2, pszFmt, ptr);
        va_end(ptr);

        buffer[prefixLength + nBytes - 2] = '\n';
        buffer[prefixLength + nBytes - 1] = '\0';

        ::OutputDebugStringA(&buffer[0]);

	}
#pragma warning(pop)

#pragma warning(push)
#pragma warning(disable : 4793)
	void __cdecl operator()(
		const wchar_t *pszFmt, 
		...) const
	{
		va_list ptr; va_start(ptr, pszFmt);
        size_t nBytes = _vscwprintf(pszFmt, ptr);
        nBytes += 2;
        va_end(ptr);

		auto prefixLength = wcslen(WPREFIX);
		std::vector<wchar_t> buffer(nBytes + prefixLength);
		swprintf_s(&buffer[0], prefixLength + 1, L"%s", WPREFIX);
		
        va_start(ptr, pszFmt);
		_vsnwprintf_s(&buffer[prefixLength], nBytes, nBytes - 2, pszFmt, ptr);
        va_end(ptr);

        buffer[prefixLength + nBytes - 2] = L'\n';
        buffer[prefixLength + nBytes - 1] = L'\0';

        ::OutputDebugStringW(&buffer[0]);
	}
#pragma warning(pop)

};

#define RELTRACE CReleaseTrace()

#ifdef _DEBUG
#undef ATLTRACE
#define ATLTRACE CReleaseTrace()
#endif