// This file originally taken from OpenCover project - see LICENSE_OPENCOVER
#include "pch.h"
#include "ExceptionHandler.h"

namespace Instrumentation
{
	ExceptionHandler::ExceptionHandler()
	{
		m_tryStart = nullptr;
		m_tryEnd = nullptr;
		m_handlerStart = nullptr;
		m_handlerEnd = nullptr;
		m_filterStart = nullptr;
		m_token = 0;
		m_handlerType = COR_ILEXCEPTION_CLAUSE_NONE;
	}
}