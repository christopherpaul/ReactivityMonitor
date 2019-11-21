// This file originally taken from OpenCover project - see LICENSE_OPENCOVER
#include "Operations.h"

#pragma once

#define UNSAFE_BRANCH_OPERAND 0xDEADBABE

namespace Instrumentation {
	class Instruction;
	class Method;

	typedef std::vector<std::unique_ptr<Instruction>> InstructionList;
    typedef std::vector<Instruction*> InstructionReferenceList;

	/// <summary>A representation of an IL instruction.</summary>
	class Instruction
	{
	public:
		Instruction(CanonicalName operation, ULONGLONG operand);
		explicit Instruction(CanonicalName operation);

		protected:
		Instruction();
		Instruction& operator = (const Instruction& b);
		bool Equivalent(const Instruction& b);

#ifdef TEST_FRAMEWORK
	public:
#else
	public:
#endif
		long m_offset;
		CanonicalName m_operation;
		ULONGLONG m_operand;
		bool m_isBranch;

		std::vector<long> m_branchOffsets;
		InstructionReferenceList m_branches;
		InstructionReferenceList m_joins;

		long m_origOffset;

        long length() const
        {
            return Operations::m_mapNameOperationDetails[m_operation].totalLength();
        }

	public:

		friend class Method;
	};
}