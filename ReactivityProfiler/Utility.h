#pragma once

#define CHECK_SUCCESS(hrExpr) { auto hr__ = (hrExpr); if (FAILED(hr__)) { RELTRACE(#hrExpr, hr__); throw hr__; } }
#define CHECK_SUCCESS_MSG(hrExpr, msg) { auto hr__ = (hrExpr); if (FAILED(hr__)) { RELTRACE(msg, hr__); throw hr__; } }

inline HRESULT HandleExceptions(const std::function<void()>& f)
{
    try
    {
        f();
        return S_OK;
    }
    catch (HRESULT hr)
    {
        return hr;
    }
    catch (std::exception ex)
    {
        RELTRACE("%s", ex.what());
        return E_FAIL;
    }
    catch (...)
    {
        return E_FAIL;
    }
}