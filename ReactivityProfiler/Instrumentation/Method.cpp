// This file originally taken from OpenCover project - see LICENSE_OPENCOVER
#include "pch.h"
#include "Method.h"
#include "ReleaseTrace.h"

namespace Instrumentation
{
    Method::Method() : Method(nullptr)
    {
    }

    Method::Method(const IMAGE_COR_ILMETHOD* pMethod)
	{
		memset(&m_header, 0, 3 * sizeof(DWORD));
		m_header.Size = 3;
		m_header.Flags = CorILMethod_FatFormat;
		m_header.MaxStack = 8;

		ReadMethod(pMethod);
	}

	Method::~Method()
	{
	}

	/// <summary>Read the full method from the supplied buffer.</summary>
	void Method::ReadMethod(const IMAGE_COR_ILMETHOD* pMethod)
	{
        BYTE justRet[] = { Operations::m_mapNameOperationDetails[CEE_RET].op2 };
        BYTE* pCode;
        if (!pMethod)
        {
            m_header.CodeSize = 1;
            pCode = justRet;
        }
        else
        {
            auto fatImage = static_cast<const COR_ILMETHOD_FAT*>(&pMethod->Fat);
            if (!fatImage->IsFat())
            {
                ATLTRACE(_T("TINY"));
                auto tinyImage = static_cast<const COR_ILMETHOD_TINY*>(&pMethod->Tiny);
                m_header.CodeSize = tinyImage->GetCodeSize();
                pCode = tinyImage->GetCode();
                m_originalHeaderSize = sizeof(IMAGE_COR_ILMETHOD_TINY);
                ATLTRACE(_T("TINY(%X) => (%d + 1) : %d"), m_header.CodeSize, m_header.CodeSize, m_header.MaxStack);
            }
            else
            {
                m_originalHeaderSize = fatImage->Size * sizeof(DWORD);
                memcpy(&m_header, pMethod, m_originalHeaderSize);
                pCode = fatImage->GetCode();
                ATLTRACE(_T("FAT(%X) => (%d + 12) : %d"), m_header.CodeSize, m_header.CodeSize, m_header.MaxStack);
            }
        }
		SetBuffer(pCode);
		ReadBody();
	}

	/// <summary>Write the method to a supplied buffer</summary>
	/// <remarks><para>The buffer must be of the size supplied by <c>GetMethodSize</c>.</para>
	/// <para>Currently only write methods with 'Fat' headers and 'Fat' Sections - simpler.</para>
	/// <para>The buffer will normally be allocated by a call to <c>IMethodMalloc::Alloc</c></para></remarks>
	void Method::WriteMethod(IMAGE_COR_ILMETHOD* pMethod)
	{
		BYTE* pCode;
		auto fatImage = static_cast<COR_ILMETHOD_FAT*>(&pMethod->Fat);

		m_header.Flags &= ~CorILMethod_MoreSects;
		if (m_exceptions.size() > 0)
		{
			m_header.Flags |= CorILMethod_MoreSects;
		}

		memcpy(fatImage, &m_header, m_header.Size * sizeof(DWORD));

		pCode = fatImage->GetCode();

		SetBuffer(pCode);

		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			auto& details = Operations::m_mapNameOperationDetails[(*it)->m_operation];
			if (details.op1 == REFPRE)
			{
				Write<BYTE>(details.op2);
			}
			else if (details.op1 == MOOT)
			{
				continue;
			}
			else
			{
				Write<BYTE>(details.op1);
				Write<BYTE>(details.op2);
			}

			switch (details.operandSize)
			{
			case Null:
				break;
			case Byte:
				Write<BYTE>(static_cast<BYTE>((*it)->m_operand));
				break;
			case Word:
				Write<USHORT>(static_cast<USHORT>((*it)->m_operand));
				break;
			case Dword:
				Write<ULONG>(static_cast<ULONG>((*it)->m_operand));
				break;
			case Qword:
				Write<ULONGLONG>((*it)->m_operand);
				break;
			default:
				break;
			}

			if ((*it)->m_operation == CEE_SWITCH)
			{
				for (auto offsetIter = (*it)->m_branchOffsets.begin(); offsetIter != (*it)->m_branchOffsets.end(); ++offsetIter)
				{
					Write<long>(*offsetIter);
				}
			}
		}

		WriteSections();
	}

	/// <summary>Write out the FAT sections</summary>
	void Method::WriteSections()
	{
		if (m_exceptions.size() > 0)
		{
			Align<DWORD>();
			IMAGE_COR_ILMETHOD_SECT_FAT section;
			section.Kind = CorILMethod_Sect_FatFormat;
			section.Kind |= CorILMethod_Sect_EHTable;
			section.DataSize = (m_exceptions.size() * 24) + 4;
			Write<IMAGE_COR_ILMETHOD_SECT_FAT>(section);
			for (auto it = m_exceptions.begin(); it != m_exceptions.end(); ++it)
			{
				Write<ULONG>((*it)->m_handlerType);
				Write<long>((*it)->m_tryStart->m_offset);
				Write<long>((*it)->m_tryEnd->m_offset - (*it)->m_tryStart->m_offset);
				Write<long>((*it)->m_handlerStart->m_offset);
				Write<long>((*it)->m_handlerEnd->m_offset - (*it)->m_handlerStart->m_offset);

				if (COR_ILEXCEPTION_CLAUSE_FILTER == (*it)->m_handlerType)
				{
					Write<long>((*it)->m_filterStart->m_offset);
				}
				else
				{
					Write<ULONG>((*it)->m_token);
				}
			}
		}
	}

    static void RecordBranchInfo(Instruction& instr, const OperationDetails& details)
    {
        // are we a branch or a switch
        instr.m_isBranch = (details.controlFlow == BRANCH || details.controlFlow == COND_BRANCH);

        if (instr.m_isBranch && instr.m_operation != CEE_SWITCH)
        {
            if (details.operandSize == 1)
            {
                instr.m_branchOffsets.push_back(static_cast<char>(static_cast<BYTE>(instr.m_operand)));
            }
            else
            {
                instr.m_branchOffsets.push_back(static_cast<ULONG>(instr.m_operand));
            }
        }
    }

	/// <summary>Read in a method body and any section handlers.</summary>
	/// <remarks>Also converts all short branches to long branches and calls <c>RecalculateOffsets</c></remarks>
	void Method::ReadBody()
	{
		_ASSERTE(m_header.CodeSize != 0);
		_ASSERTE(GetPosition() == 0);

		while (GetPosition() < m_header.CodeSize)
		{
			std::unique_ptr<Instruction> pInstruction(new Instruction());
			pInstruction->m_offset = GetPosition();
			pInstruction->m_origOffset = pInstruction->m_offset;

			BYTE op1 = REFPRE;
			BYTE op2 = Read<BYTE>();
			if (STP1 == op2)
			{
				op1 = STP1;
				op2 = Read<BYTE>();
			}

			OperationDetails &details = Operations::m_mapOpsOperationDetails[MAKEWORD(op1, op2)];
			pInstruction->m_operation = details.canonicalName;
			switch (details.operandSize)
			{
			case Null:
				break;
			case Byte:
				pInstruction->m_operand = Read<BYTE>();
				break;
			case Word:
				pInstruction->m_operand = Read<USHORT>();
				break;
			case Dword:
				pInstruction->m_operand = Read<ULONG>();
				break;
			case Qword:
				pInstruction->m_operand = Read<ULONGLONG>();
				break;
			default:
				break;
			}

			// are we a branch or a switch
            RecordBranchInfo(*pInstruction, details);

			if (pInstruction->m_operation == CEE_SWITCH)
			{
				auto numbranches = static_cast<DWORD>(pInstruction->m_operand);
				while (numbranches-- != 0) pInstruction->m_branchOffsets.push_back(Read<long>());
			}

			m_instructions.push_back(std::move(pInstruction));
		}

		ReadSections();

		SetBuffer(nullptr);

		ResolveBranches(m_instructions.begin(), m_instructions.end());

		ConvertShortBranches(m_instructions.begin(), m_instructions.end());

		RecalculateOffsets();
	}

	std::unique_ptr<ExceptionHandler> Method::ReadExceptionHandler(
		enum CorExceptionFlag type,
		long tryStart, long tryEnd,
		long handlerStart, long handlerEnd,
		long filterStart, ULONG token) {

		auto pSection = std::make_unique<ExceptionHandler>();
		pSection->m_handlerType = type;
		pSection->m_tryStart = GetInstructionAtOffset(tryStart);
		pSection->m_tryEnd = GetInstructionAtOffset(tryStart + tryEnd);
		pSection->m_handlerStart = GetInstructionAtOffset(handlerStart);
		pSection->m_handlerEnd = GetInstructionAtOffset(handlerStart + handlerEnd,
			(type & COR_ILEXCEPTION_CLAUSE_FINALLY) == COR_ILEXCEPTION_CLAUSE_FINALLY,
			(type & COR_ILEXCEPTION_CLAUSE_FAULT) == COR_ILEXCEPTION_CLAUSE_FAULT,
			(type & COR_ILEXCEPTION_CLAUSE_FILTER) == COR_ILEXCEPTION_CLAUSE_FILTER,
			(type & COR_ILEXCEPTION_CLAUSE_NONE) == COR_ILEXCEPTION_CLAUSE_NONE);

		if (filterStart != 0) {
			pSection->m_filterStart = GetInstructionAtOffset(filterStart);
		}

		pSection->m_token = token;
		return pSection;
	}

	template<class flag, class start, class end>
	void Method::ReadExceptionHandlers(int count) {
		for (auto i = 0; i < count; i++)
		{
			auto type = static_cast<CorExceptionFlag>(Read<flag>());
			long tryStart = Read<start>();
			long tryEnd = Read<end>();
			long handlerStart = Read<start>();
			long handlerEnd = Read<end>();
			long filterStart = 0;
			ULONG token = 0;

			if (COR_ILEXCEPTION_CLAUSE_FILTER == type)
			{
				filterStart = Read<long>();
			}
			else
			{
				token = Read<ULONG>();
			}
			m_exceptions.push_back(ReadExceptionHandler(type, tryStart, tryEnd, handlerStart, handlerEnd, filterStart, token));
		}
	}

	/// <summary>Read the exception handler section.</summary>
	/// <remarks>All 'Small' sections are to be converted to 'Fat' sections.</remarks>
	void Method::ReadSections()
	{
		if ((m_header.Flags & CorILMethod_MoreSects) == CorILMethod_MoreSects)
		{
			BYTE flags;
			do
			{
				Align<DWORD>(); // must be DWORD aligned
				flags = Read<BYTE>();
				_ASSERTE((flags & CorILMethod_Sect_EHTable) == CorILMethod_Sect_EHTable);
				if ((flags & CorILMethod_Sect_FatFormat) == CorILMethod_Sect_FatFormat)
				{
					Advance(-1);
					int count = ((Read<ULONG>() >> 8) / 24);
					ReadExceptionHandlers<ULONG, long, long>(count);
				}
				else
				{
					auto count = static_cast<int>(Read<BYTE>() / 12);
					Advance(2);
					ReadExceptionHandlers<USHORT, USHORT, BYTE>(count);
				}
			} while ((flags & CorILMethod_Sect_MoreSects) == CorILMethod_Sect_MoreSects);
		}
	}

	/// <summary>Gets the <c>Instruction</c> that has (is at) the specified offset.</summary>
	/// <param name="offset">The offset to look for.</param>
	/// <returns>An <c>Instruction</c> that exists at that location.</returns>
	/// <remarks>Ensure that the offsets are current by executing <c>RecalculateOffsets</c>
	/// beforehand</remarks>
	Instruction * Method::GetInstructionAtOffset(long offset)
	{
        return GetInstructionAtOffset(offset, m_instructions.begin(), m_instructions.end());
	}

	/// <summary>Gets the <c>Instruction</c> that has (is at) the specified offset.</summary>
	/// <param name="offset">The offset to look for.</param>
	/// <returns>An <c>Instruction</c> that exists at that location.</returns>
	/// <remarks>Ensure that the offsets are current by executing <c>RecalculateOffsets</c>
	/// beforehand. Only should be used when trying to find the instruction pointed to be a finally 
	/// block which may be beyond the bounds of the method itself</remarks>
	/// <example>
	///     void Method()
	///     {
	///            try
	///            {
	///                throw new Exception();
	///            }
	///            finally
	///            {
	///                Console.WriteLine("Finally");
	///            }
	///     }
	/// </example>
	Instruction * Method::GetInstructionAtOffset(long offset, bool isFinally, bool isFault, bool isFilter, bool isTyped)
	{
		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			if ((*it)->m_offset == offset)
			{
				return it->get();
			}
		}

		if (isFinally || isFault || isFilter || isTyped)
		{
			auto pLast = m_instructions.back().get();
			auto& details = Operations::m_mapNameOperationDetails[pLast->m_operation];
			if (offset == pLast->m_offset + details.length + details.operandSize)
			{
				// add a code label to hang the clause handler end off
				auto pInstruction = std::make_unique<Instruction>(CEE_CODE_LABEL);
				pInstruction->m_offset = offset;
                auto p = pInstruction.get();
				m_instructions.push_back(std::move(pInstruction));
				return p;
			}
		}
		_ASSERTE(FALSE);
		return nullptr;
	}

	/// <summary>Uses the current offsets and locates the instructions that reside that offset to 
	/// build a new list</summary>
	/// <remarks>This allows us to insert (or modify) instructions without losing the intended 'goto' 
	/// point. <c>RecalculateOffsets</c> is used to rebuild the new required operand(s) based on the
	/// offsets of the instructions being referenced</remarks>
	void Method::ResolveBranches(InstructionList::iterator begin, InstructionList::iterator end)
	{
		for (auto it = begin; it != end; ++it)
		{
			(*it)->m_branches.clear();
			auto& details = Operations::m_mapNameOperationDetails[(*it)->m_operation];
			auto baseOffset = (*it)->m_offset + details.length + details.operandSize;
			if ((*it)->m_operation == CEE_SWITCH)
			{
				baseOffset += (4 * static_cast<long>((*it)->m_operand));
			}

			for (auto offsetIter = (*it)->m_branchOffsets.begin(); offsetIter != (*it)->m_branchOffsets.end(); ++offsetIter)
			{
				auto offset = baseOffset + (*offsetIter);
				auto instruction = GetInstructionAtOffset(offset, begin, end);
				if (instruction != nullptr)
				{
					(*it)->m_branches.push_back(instruction);
				}
			}
			_ASSERTE((*it)->m_branchOffsets.size() == (*it)->m_branches.size());
		}
	}

	/// <summary>Pretty print the IL</summary>
	/// <remarks>Only works for Debug builds.</remarks>
	void Method::DumpIL(bool enableDump)
	{
		if (!enableDump)
			return;
		RELTRACE(_T("-+-+-+-+-+-+-+-+-+-+-+-+- START -+-+-+-+-+-+-+-+-+-+-+-+"));
		DumpInstructions();
		DumpExceptionFilters();
		RELTRACE(_T("-+-+-+-+-+-+-+-+-+-+-+-+-  END  -+-+-+-+-+-+-+-+-+-+-+-+"));
	}

	void Method::DumpInstructions()
	{
		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			auto& details = Operations::m_mapNameOperationDetails[(*it)->m_operation];
			if (details.operandSize == Null)
			{
				RELTRACE(_T("(IL_%04X) IL_%04X %s"), (*it)->m_origOffset, (*it)->m_offset, details.stringName);
			}
			else if (details.operandParam == ShortInlineBrTarget || details.operandParam == InlineBrTarget)
			{
				auto offset = (*it)->m_offset + (*it)->m_branchOffsets[0] + details.length + details.operandSize;
				RELTRACE(_T("(IL_%04X) IL_%04X %s IL_%04X"),
					(*it)->m_origOffset, (*it)->m_offset, details.stringName, offset);
			}
			else if (details.operandParam == InlineMethod || details.operandParam == InlineString)
			{
				RELTRACE(_T("(IL_%04X) IL_%04X %s (%02X)%02X%02X%02X"),
					(*it)->m_origOffset, (*it)->m_offset, details.stringName,
					(BYTE)((*it)->m_operand >> 24),
					(BYTE)((*it)->m_operand >> 16),
					(BYTE)((*it)->m_operand >> 8),
					(BYTE)((*it)->m_operand & 0xFF));
			}
			else if (details.operandSize == Byte)
			{
				RELTRACE(_T("(IL_%04X) IL_%04X %s %02X"),
					(*it)->m_origOffset, (*it)->m_offset, details.stringName, (*it)->m_operand);
			}
			else if (details.operandSize == Word)
			{
				RELTRACE(_T("(IL_%04X) IL_%04X %s %04X"),
					(*it)->m_origOffset, (*it)->m_offset, details.stringName, (*it)->m_operand);
			}
			else if (details.operandSize == Dword)
			{
				RELTRACE(_T("(IL_%04X) IL_%04X %s %08X"),
					(*it)->m_origOffset, (*it)->m_offset, details.stringName, (*it)->m_operand);
			}
			else
			{
				RELTRACE(_T("(IL_%04X) IL_%04X %s %X"),
					(*it)->m_origOffset, (*it)->m_offset, details.stringName, (*it)->m_operand);
			}
			
			for (auto offsetIter = (*it)->m_branchOffsets.begin(); offsetIter != (*it)->m_branchOffsets.end(); ++offsetIter)
			{
				if ((*it)->m_operation == CEE_SWITCH)
				{
					auto offset = (*it)->m_offset + (4 * static_cast<long>((*it)->m_operand)) + (*offsetIter) + details.length + details.operandSize;
					RELTRACE(_T("    IL_%04X"), offset);
				}
			}
		}
	}

    Instruction* Method::GetInstructionAtOffset(long offset, InstructionList::iterator begin, InstructionList::iterator end)
    {
        for (auto it = begin; it != end; ++it)
        {
            if ((*it)->m_offset == offset)
            {
                return it->get();
            }
        }
        _ASSERTE(FALSE);
        return nullptr;
    }

	void Method::DumpExceptionFilters()
	{
		int i = 0;
		for (auto it = m_exceptions.begin(); it != m_exceptions.end(); ++it)
		{
			auto tryStartOffset = (*it)->m_tryStart != nullptr ? (*it)->m_tryStart->m_offset : 0;
			auto tryEndOffset = (*it)->m_tryEnd != nullptr ? (*it)->m_tryEnd->m_offset : 0;
			auto handlerStartOffset = (*it)->m_handlerStart != nullptr ? (*it)->m_handlerStart->m_offset : 0;
			auto handlerEndOffset = (*it)->m_handlerEnd != nullptr ? (*it)->m_handlerEnd->m_offset : 0;
			auto filterStartOffset = (*it)->m_filterStart != nullptr ? (*it)->m_filterStart->m_offset : 0;

			RELTRACE(_T("Section %d: %d %04X %04X %04X %04X %04X %08X"),
				i, (*it)->m_handlerType, tryStartOffset, tryEndOffset,
				handlerStartOffset, handlerEndOffset, filterStartOffset,
				(*it)->m_token);

			++i;
		}
	}

    /// <summary>Converts all short branches to long branches.</summary>
	/// <remarks><para>After instrumentation most short branches will not have the capacity for
	/// the new required offset. Save time/effort and make all branches long ones.</para> 
	/// <para>Could add the capability to optimise long to short at a later date but consider 
	/// the benefits dubious after all the new instrumentation has been added.</para></remarks>
	void Method::ConvertShortBranches(InstructionList::iterator begin, InstructionList::iterator end)
	{
		for (auto it = begin; it != end; ++it)
		{
			OperationDetails &details = Operations::m_mapNameOperationDetails[(*it)->m_operation];
			if ((*it)->m_isBranch && details.operandSize == 1)
			{
				CanonicalName newOperation = (*it)->m_operation;
				switch ((*it)->m_operation)
				{
				case CEE_BR_S:
					newOperation = CEE_BR;
					break;
				case CEE_BRFALSE_S:
					newOperation = CEE_BRFALSE;
					break;
				case CEE_BRTRUE_S:
					newOperation = CEE_BRTRUE;
					break;
				case CEE_BEQ_S:
					newOperation = CEE_BEQ;
					break;
				case CEE_BGE_S:
					newOperation = CEE_BGE;
					break;
				case CEE_BGT_S:
					newOperation = CEE_BGT;
					break;
				case CEE_BLE_S:
					newOperation = CEE_BLE;
					break;
				case CEE_BLT_S:
					newOperation = CEE_BLT;
					break;
				case CEE_BNE_UN_S:
					newOperation = CEE_BNE_UN;
					break;
				case CEE_BGE_UN_S:
					newOperation = CEE_BGE_UN;
					break;
				case CEE_BGT_UN_S:
					newOperation = CEE_BGT_UN;
					break;
				case CEE_BLE_UN_S:
					newOperation = CEE_BLE_UN;
					break;
				case CEE_BLT_UN_S:
					newOperation = CEE_BLT_UN;
					break;
				case CEE_LEAVE_S:
					newOperation = CEE_LEAVE;
					break;
				default:
					break;
				}
				(*it)->m_operation = newOperation;
				(*it)->m_operand = UNSAFE_BRANCH_OPERAND;
			}

			(*it)->m_branchOffsets.clear();
		}
	}

    void Method::CalculateOffsets(InstructionList::iterator begin, InstructionList::iterator end)
    {
        long position = 0;
        for (auto it = begin; it != end; ++it)
        {
            auto& details = Operations::m_mapNameOperationDetails[(*it)->m_operation];
            (*it)->m_offset = position;
            position += details.length;
            position += details.operandSize;
            if ((*it)->m_operation == CEE_SWITCH)
            {
                position += 4 * static_cast<long>((*it)->m_operand);
            }
        }
    }

	/// <summary>Recalculate the offsets of each instruction taking into account the instruction
	/// size, the operand size and any extra datablocks CEE_SWITCH</summary>
	void Method::RecalculateOffsets()
	{
        CalculateOffsets(m_instructions.begin(), m_instructions.end());

		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			auto& details = Operations::m_mapNameOperationDetails[(*it)->m_operation];
			if ((*it)->m_isBranch)
			{
				(*it)->m_branchOffsets.clear();
				if ((*it)->m_operation == CEE_SWITCH)
				{
					auto offset = ((*it)->m_offset + details.length + details.operandSize + (4 * static_cast<long>((*it)->m_operand)));
					for (auto bit = (*it)->m_branches.begin(); bit != (*it)->m_branches.end(); ++bit)
					{
						(*it)->m_branchOffsets.push_back((*bit)->m_offset - offset);
					}
				}
				else
				{
#pragma warning(suppress:26451)
					(*it)->m_operand = (*it)->m_branches[0]->m_offset - ((*it)->m_offset + details.length + details.operandSize);
					(*it)->m_branchOffsets.push_back(static_cast<long>((*it)->m_operand));
				}
			}
		}
	}

	/// <summary>Calculates the size of the method which include the header size, 
	/// the code size and the (aligned) creitical sections if they exist. Use this
	/// to get the size required for allocating memory.</summary>
	/// <returns>The size of the method.</returns>
	/// <remarks>It is recomended that <c>RecalculateOffsets</c> should be called 
	/// beforehand if any instrumentation has been done</remarks>
	long Method::GetMethodSize()
	{
		auto lastInstruction = m_instructions.back().get();
		auto& details = Operations::m_mapNameOperationDetails[lastInstruction->m_operation];

		m_header.CodeSize = lastInstruction->m_offset + details.length + details.operandSize;
		long size = sizeof(IMAGE_COR_ILMETHOD_FAT) + m_header.CodeSize;

		m_header.Flags &= ~CorILMethod_MoreSects;
		if (m_exceptions.size() > 0)
		{
			m_header.Flags |= CorILMethod_MoreSects;
			long align = sizeof(DWORD) - 1;
#pragma warning(suppress:26451)
            size = ((size + align) & ~align);
#pragma warning(suppress:26451)
            size += ((static_cast<long>(m_exceptions.size()) * 6) + 1) * sizeof(long);
		}

		return size;
	}

	/// <summary>Test if a method has already been instrumented by comparing a list of instructions at that location</summary>
	/// <param name="offset">The offset to look for.</param>
	/// <param name="instructions">The list of instructions to compare with at that location.</param>
	bool Method::IsInstrumented(long offset, const InstructionList &instructions)
	{
		bool foundInstructionAtOffset = false;
		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			if ((*it)->m_origOffset == offset)
			{
				foundInstructionAtOffset = true;
				for (auto it2 = instructions.begin(); it2 != instructions.end(); ++it2)
				{
					if (!(*it2)->Equivalent(*(*it)))
						return false;
					++it;
				}
				break;
			}
		}

		return foundInstructionAtOffset;
	}

	/// <summary>Insert a sequence of instructions at a specific offset</summary>
	/// <param name="offset">The offset to look for.</param>
	/// <param name="instructions">The list of instructions to insert at that location.</param>
	/// <remarks>Original pointer references are maintained by inserting the sequence of instructions 
	/// after the intended target and then using a copy operator on the <c>Instruction</c> objects to 
	/// copy the data between them</remarks>
	void Method::InsertInstructionsAtOffset(long offset, const InstructionList &instructions)
	{
		InstructionList clone;
		for (auto it = instructions.begin(); it != instructions.end(); ++it)
		{
            auto clonedInstr = std::make_unique<Instruction>(*(*it));
            RecordBranchInfo(*clonedInstr, Operations::m_mapNameOperationDetails[clonedInstr->m_operation]);
			clone.push_back(std::move(clonedInstr));
		}

		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			if ((*it)->m_offset == offset)
			{
				++it;
				m_instructions.insert(it, std::make_move_iterator(clone.begin()), std::make_move_iterator(clone.end()));
				break;
			}
		}

        InstructionList::iterator begin, end;
		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			if ((*it)->m_origOffset == offset)
			{
                begin = it;
				auto orig = *(*it);
				for (unsigned int i = 0; i < clone.size(); i++)
				{
					auto temp = it;
					++it;
					*(*temp) = *(*it);
				}
				*(*it) = orig;
                end = it;
				break;
			}
		}

        CalculateOffsets(begin, end); // assign temporary offsets within the new block so ResolveBranches works
        ResolveBranches(begin, end);
        ConvertShortBranches(begin, end);

		RecalculateOffsets();
	}

	/// <summary>Insert a sequence of instructions at a sequence point</summary>
	/// <param name="origOffset">The original (as in before any instrumentation) offset to look for.</param>
	/// <param name="instructions">The list of instructions to insert at that location.</param>
	/// <remarks>Original pointer references are maintained by inserting the sequence of instructions 
	/// after the intended target and then using a copy operator on the <c>Instruction</c> objects to 
	/// copy the data between them</remarks>
	void Method::InsertInstructionsAtOriginalOffset(long origOffset, const InstructionList &instructions)
	{
		InstructionList clone;
		for (auto it = instructions.begin(); it != instructions.end(); ++it)
		{
            auto clonedInstr = std::make_unique<Instruction>(*(*it));
            RecordBranchInfo(*clonedInstr, Operations::m_mapNameOperationDetails[clonedInstr->m_operation]);
			clone.push_back(std::move(clonedInstr));
		}

		long actualOffset = 0;
		Instruction* actualInstruction = nullptr;
		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			if ((*it)->m_origOffset == origOffset)
			{
				actualInstruction = it->get();
				actualOffset = (actualInstruction)->m_offset;
				++it;
				m_instructions.insert(it, std::make_move_iterator(clone.begin()), std::make_move_iterator(clone.end()));
				break;
			}
		}

        InstructionList::iterator begin, end;
        if (!DoesTryHandlerPointToOffset(actualOffset))
		{
			for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
			{
				if (it->get() == actualInstruction)
				{
                    begin = it;
					Instruction orig = *(*it);
					for (unsigned int i = 0; i < clone.size(); i++)
					{
						auto temp = it;
						++it;
						*(*temp) = *(*it);
					}
					*(*it) = orig;
                    end = it;
					break;
				}
			}
		}

        CalculateOffsets(begin, end); // temporary offsets
        ResolveBranches(begin, end);
        ConvertShortBranches(begin, end);

        RecalculateOffsets();
    }

	/// <summary>Test if we have an exception where the handler start points to the 
	/// instruction at the supplied offset</summary>
	/// <param name="offset">The offset to look for.</param>
	/// <returns>An <c>Instruction</c> that exists at that location.</returns>
	bool Method::DoesTryHandlerPointToOffset(long offset)
	{
		for (auto it = m_exceptions.begin(); it != m_exceptions.end(); ++it)
		{
			if ((*it)->m_handlerType == COR_ILEXCEPTION_CLAUSE_NONE
				&& ((*it)->m_handlerStart->m_offset == offset 
				&& (*it)->m_handlerStart->m_operand == CEE_THROW))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Get the size of the COR_IL_MAP block</summary>
	/// <returns>The size (number of elements) of the array to allocate</returns>
	/// <remarks>Used with PopulateILMap to allocate and populate an array of COR_IL_MAP
	/// which when used with SetILInstrumentedCodeMap can be used to inform any attached 
	/// debugger where the new debug points are.</remarks>
	ULONG Method::GetILMapSize()
	{
		ULONG mapSize = 0;
		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			if ((*it)->m_origOffset != -1)
				++mapSize;
		}
		return mapSize;
	}

	/// <summary>Populate a supplied COR_IL_MAP block</summary>
	/// <param name="mapSize">The size of the array.</param>
	/// <param name="maps">The preallocated (CoTaskMemAlloc) array to populate.</param>
	/// <remarks>Used with ULONG Method::GetILMapSize to allocate and populate an array of COR_IL_MAP
	/// which when used with SetILInstrumentedCodeMap can be used to inform any attached 
	/// debugger where the new debug points are.</remarks>
	void Method::PopulateILMap(ULONG mapSize, COR_IL_MAP* maps)
	{
		_ASSERTE(GetILMapSize() == mapSize);

		mapSize = 0;
		for (auto it = m_instructions.begin(); it != m_instructions.end(); ++it)
		{
			if ((*it)->m_origOffset != -1)
			{
				maps[mapSize].fAccurate = TRUE;
				maps[mapSize].oldOffset = (*it)->m_origOffset;
				maps[mapSize].newOffset = (*it)->m_offset;

				++mapSize;
			}
		}
	}
}
