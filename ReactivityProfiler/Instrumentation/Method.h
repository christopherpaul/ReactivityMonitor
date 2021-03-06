// This file originally taken from OpenCover project - see LICENSE_OPENCOVER
#pragma once
#include "Instruction.h"
#include "ExceptionHandler.h"
#include "MethodBuffer.h"

namespace Instrumentation
{
	/// <summary>The <c>Method</c> entity builds a 'model' of the IL that can then be modified</summary>
	class Method :
		public MethodBuffer
	{
	public:
        explicit Method(); // produces a method with just a RET in (which is only valid as-is if method is void)
		explicit Method(const IMAGE_COR_ILMETHOD* pMethod);
		~Method();

	public:
		long GetMethodSize();
		void WriteMethod(IMAGE_COR_ILMETHOD* pMethod);
		void InsertInstructionsAtOriginalOffset(long origOffset, const InstructionList &instructions);
		void InsertInstructionsAtOffset(long offset, const InstructionList &instructions);
		void DumpIL(bool enableDump);
		ULONG GetILMapSize();
		void PopulateILMap(ULONG mapSize, COR_IL_MAP* maps);

		bool IsInstrumented(long offset, const InstructionList &instructions);
        ULONG GetOriginalHeaderSize() const { return m_originalHeaderSize; } // lets us find original RVA of instructions
        mdSignature GetLocalsSignature() const { return m_header.LocalVarSigTok; }
        void SetLocalsSignature(mdSignature signatureToken) { m_header.LocalVarSigTok = signatureToken; }

	public:
		void SetMinimumStackSize(unsigned int minimumStackSize)
		{
			if (m_header.MaxStack < minimumStackSize)
			{
				m_header.MaxStack = minimumStackSize;
			}
		}

		void IncrementStackSize(unsigned int extraStackSize)
		{
			m_header.MaxStack += extraStackSize;
		}

		DWORD GetCodeSize() const { return m_header.CodeSize; }


	public:
		void RecalculateOffsets();

	private:
		void ReadMethod(const IMAGE_COR_ILMETHOD* pMethod);
		void ReadBody();

        static void CalculateOffsets(InstructionList::iterator begin, InstructionList::iterator end);
		static void ConvertShortBranches(InstructionList::iterator begin, InstructionList::iterator end);
		static void ResolveBranches(InstructionList::iterator begin, InstructionList::iterator end);
		void DumpExceptionFilters();
		void DumpInstructions();
        static Instruction* GetInstructionAtOffset(long offset, InstructionList::iterator begin, InstructionList::iterator end);
		Instruction * GetInstructionAtOffset(long offset);
		Instruction * GetInstructionAtOffset(long offset, bool isFinally, bool isFault, bool isFilter, bool isTyped);
		void ReadSections();

		template<class flag, class start, class end>
		void ReadExceptionHandlers(int count);

		std::unique_ptr<ExceptionHandler> ReadExceptionHandler(enum CorExceptionFlag type, long tryStart, long tryEnd, long handlerStart, long handlerEnd, long filterStart, ULONG token);

		void WriteSections();
		bool DoesTryHandlerPointToOffset(long offset);

	private:
		// all instrumented methods will be FAT (with FAT SECTIONS if exist) regardless
		IMAGE_COR_ILMETHOD_FAT m_header;
        ULONG m_originalHeaderSize;

#ifdef TEST_FRAMEWORK
	public:
		ExceptionHandlerList m_exceptions;
#else
	private:
		ExceptionHandlerList m_exceptions;
#endif
	public:
		InstructionList m_instructions;

		int GetNumberOfInstructions() const
		{
			return static_cast<int>(m_instructions.size());
		}

		int GetNumberOfExceptions() const
		{
			return static_cast<int>(m_exceptions.size());
		}
	};
}