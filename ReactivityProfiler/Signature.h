#pragma once
 
#include "common.h"

class MethodSignatureReader;

class SignatureArrayShapeReaderState;
class SignatureTypeReaderState;
class SignatureParamReaderState;
class MethodSignatureReaderState;
class MethodSpecSignatureReaderState;
class LocalsSignatureReaderState;

class SignatureTypeWriterState;
class SignatureParamWriterState;
class MethodSignatureWriterState;
class MethodSpecSignatureWriterState;

class SignatureArrayShapeReader
{
public:
    SignatureArrayShapeReader(const std::shared_ptr<SignatureArrayShapeReaderState>& state);

    ULONG GetRank();
    ULONG GetNumSizes();
    bool MoveNextSize();
    ULONG GetSize();
    ULONG GetNumLoBounds();
    bool MoveNextLoBound();
    LONG GetLoBound();

private:
    const std::shared_ptr<SignatureArrayShapeReaderState> m_state;
};

class SignatureTypeReader
{
public:
    SignatureTypeReader(const std::shared_ptr<SignatureTypeReaderState>& state);
    SignatureTypeReader(const SignatureBlob& typeSpecSig);

    CorElementType GetTypeKind();
    CorElementType GetGenericInstKind();
    bool MoveNextCustomModifier(); // for PTR, SZARRAY
    std::pair<mdToken, bool> GetCustomModifier();
    mdToken GetToken(); // for CLASS, GENERICINST, VALUETYPE
    bool IsVoidPointer(); // for PTR
    SignatureTypeReader GetTypeReader(); // for ARRAY, PTR, SZARRAY, GENERICINST
    SignatureArrayShapeReader GetArrayShapeReader(); // for ARRAY
    MethodSignatureReader GetMethodSignatureReader(); // for FNPTR
    ULONG GetVariableNumber(); // for VAR, MVAR
    LONG GetGenArgCount(); // for GENERICINST
    bool MoveNextTypeArg(); // for GENERICINST

    std::vector<SignatureBlob> GetTypeArgSpans();

    SignatureBlob GetSigSpan();
    std::vector<COR_SIGNATURE> SubstituteTypeArgs(
        const std::vector<SignatureBlob>& typeTypeArgs,
        const std::vector<SignatureBlob>& methodTypeArgs);

private:
    const std::shared_ptr<SignatureTypeReaderState> m_state;
};

class SignatureParamReader
{
public:
    SignatureParamReader(const std::shared_ptr<SignatureParamReaderState>& state);

    bool IsReturn();
    bool IsVarArg();
    bool MoveNextCustomModifier();
    std::pair<mdToken, bool> GetCustomModifier();
    bool IsTypedByRef();
    bool IsVoid();
    bool IsByRef();
    bool HasType();
    SignatureTypeReader GetTypeReader();

private:
    const std::shared_ptr<SignatureParamReaderState> m_state;
};

class MethodSignatureReader
{
public:
    MethodSignatureReader(const SignatureBlob& sigBlob);
    MethodSignatureReader(const std::shared_ptr<MethodSignatureReaderState>& state);

    bool HasThis();
    byte GetCallingConvention();
    ULONG GenericParamCount();
    ULONG ParamCount();

    bool MoveNextParam();

    SignatureParamReader GetParamReader();

    static void Check(const SignatureBlob& sigBlob);

private:
    const std::shared_ptr<MethodSignatureReaderState> m_state;
};

class MethodSpecSignatureReader
{
public:
    MethodSpecSignatureReader(const SignatureBlob& sigBlob);
    MethodSpecSignatureReader(const std::shared_ptr<MethodSpecSignatureReaderState>& state);

    ULONG TypeArgCount();

    bool MoveNextArgType();
    SignatureTypeReader GetArgTypeReader();

    static void Check(const SignatureBlob& sigBlob);
    static std::vector<SignatureBlob> GetTypeArgSpans(const SignatureBlob& sigBlob);

private:
    const std::shared_ptr<MethodSpecSignatureReaderState> m_state;
};

typedef SignatureParamReader SignatureLocalReader;

class LocalsSignatureReader
{
public:
    LocalsSignatureReader(const SignatureBlob& sigBlob);

    uint16_t GetCount();
    bool MoveNext();
    SignatureLocalReader GetLocalReader();

    std::vector<COR_SIGNATURE> AppendLocals(const std::vector<SignatureBlob>& additionalLocals);

private:
    const std::shared_ptr<LocalsSignatureReaderState> m_state;
};

class SignatureTypeWriter
{
public:
    SignatureTypeWriter(const std::shared_ptr<SignatureTypeWriterState>& state);

    void Write(const SignatureBlob& typeSigSpan);
    void SetPrimitiveKind(CorElementType kind);
    void SetSimpleClass(mdToken typeDefOrRef);
    void SetGenericClass(mdToken typeDefOrRef, ULONG typeArgCount);
    void SetMethodTypeVar(ULONG varNumber);
    
    SignatureTypeWriter WriteTypeArg();

private:
    const std::shared_ptr<SignatureTypeWriterState> m_state;
};

class MethodSignatureWriter
{
public:
    MethodSignatureWriter(std::vector<COR_SIGNATURE>& buffer, bool hasThis, ULONG paramCount, ULONG genericParamCount);
    MethodSignatureWriter(const std::shared_ptr<MethodSignatureWriterState>& state);

    void SetVoidReturn();
    SignatureTypeWriter WriteParam();
    void Complete();

private:
    const std::shared_ptr<MethodSignatureWriterState> m_state;
};

class MethodSpecSignatureWriter
{
public:
    MethodSpecSignatureWriter(std::vector<COR_SIGNATURE>& buffer, ULONG typeArgCount);

    void AddTypeArg(const SignatureBlob& sigBlobSpan);

private:
    const std::shared_ptr<MethodSpecSignatureWriterState> m_state;
};
