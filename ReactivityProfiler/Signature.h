#pragma once

class MethodSignatureReader;

class SignatureArrayShapeReaderState;
class SignatureTypeReaderState;
class SignatureParamReaderState;
class MethodSignatureReaderState;
class MethodSpecSignatureReaderState;

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
    MethodSignatureReader(const simplespan<const COR_SIGNATURE>& sigBlob);
    MethodSignatureReader(const std::shared_ptr<MethodSignatureReaderState>& state);

    bool HasThis();
    byte GetCallingConvention();
    ULONG GenericParamCount();
    ULONG ParamCount();

    bool MoveNextParam();

    SignatureParamReader GetParamReader();

private:
    const std::shared_ptr<MethodSignatureReaderState> m_state;
};

class MethodSpecSignatureReader
{
public:
    MethodSpecSignatureReader(const simplespan<const COR_SIGNATURE>& sigBlob);
    MethodSpecSignatureReader(const std::shared_ptr<MethodSpecSignatureReaderState>& state);

    ULONG TypeArgCount();

    bool MoveNextArgType();
    SignatureTypeReader GetArgTypeReader();

private:
    const std::shared_ptr<MethodSpecSignatureReaderState> m_state;
};
