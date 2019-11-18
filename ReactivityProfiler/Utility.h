#pragma once

#define CHECK_SUCCESS(hrExpr) { auto hr = hrExpr; if (!SUCCEEDED(hr)) { RELTRACE(#hrExpr, hr); return (hr); } }
