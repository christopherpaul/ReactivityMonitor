#include "pch.h"

#include "Signature.h"

typedef const COR_SIGNATURE* sigPtr;

class ReaderBase
{
public:
    ReaderBase(sigPtr start, sigPtr limit) :
        m_start(start),
        m_limit(limit),
        m_ptr(start)
    {
    }

    ReaderBase(const SignatureBlob& span) : ReaderBase(span.begin(), span.end())
    {
    }

    byte ReadByte()
    {
        if (m_ptr >= m_limit)
        {
            LimitExceeded();
        }

        return *m_ptr++;
    }

    byte PeekByte()
    {
        if (m_ptr >= m_limit)
        {
            LimitExceeded();
        }

        return *m_ptr;
    }

    ULONG ReadCompressedUnsigned();
    LONG ReadCompressedSigned();
    mdToken ReadTypeDefOrRefEncoded();

    const sigPtr m_start;
    const sigPtr m_limit;

    sigPtr m_ptr;

    void LimitExceeded()
    {
        throw std::domain_error("ReaderBase: ran beyond end of sig blob");
    }
};

class CustomModListReader
{
public:
    CustomModListReader(ReaderBase& reader) :
        m_reader(reader)
    {
    }

    bool TryRead()
    {
        byte b = *m_reader.m_ptr;
        if (b != ELEMENT_TYPE_CMOD_OPT && b != ELEMENT_TYPE_CMOD_REQD)
        {
            return false;
        }

        m_cmod = m_reader.ReadByte();
        m_typeDefOrRef = m_reader.ReadTypeDefOrRefEncoded();
        return true;
    }

    std::pair<mdToken, bool> GetAsPair()
    {
        return { m_typeDefOrRef, m_cmod == ELEMENT_TYPE_CMOD_REQD };
    }

    ReaderBase& m_reader;
    byte m_cmod = 0;
    mdToken m_typeDefOrRef = 0;
};

class MethodSignatureReaderState : private ReaderBase
{
public:
    MethodSignatureReaderState(sigPtr start, sigPtr limit);
    MethodSignatureReaderState(const SignatureBlob& sigBlob) :
        MethodSignatureReaderState(sigBlob.begin(), sigBlob.end())
    {
    }

    byte GetCallConvByte()
    {
        return *m_start;
    }

    bool MoveNextParam();

    void MoveToEnd(sigPtr& endPtr);

    ULONG m_paramCount;
    ULONG m_genericParamCount;

    ULONG m_currentParam; // 0 = return, first actual parameter is 1
    bool m_inVarArgParams;

    std::shared_ptr<SignatureParamReaderState> m_paramReader;

    enum
    {
        INIT,
        PARAM,
        END
    } m_where;
};

enum class ParamKind
{
    Normal,
    Return,
    VarArg
};

class SignatureParamReaderState : private ReaderBase
{
public:
    SignatureParamReaderState(sigPtr start, sigPtr limit, ParamKind kind);

    bool MoveNextCustomModifier();
    std::pair<mdToken, bool> GetCustomModifier();

    void AdvanceToType();

    void MoveToEnd(sigPtr& endPtr);

    ParamKind m_kind;
    CustomModListReader m_modsReader;
    bool m_isTypedByRef;
    bool m_isByRef;
    bool m_isVoid;

    std::shared_ptr<SignatureTypeReaderState> m_typeReader;

    enum
    {
        INIT,
        CUSTOM_MOD,
        PRE_TYPE,
        TYPE,
        END
    } m_where;
};

class SignatureTypeReaderState : private ReaderBase
{
public:
    SignatureTypeReaderState(sigPtr start, sigPtr limit);

    CorElementType GetTypeKind()
    {
        return static_cast<CorElementType>(*m_start);
    }

    CorElementType GetGenericInstKind()
    {
        return static_cast<CorElementType>(*(m_start + 1));
    }

    mdToken GetToken();
    ULONG GetTypeVarNumber();
    LONG GetTypeArgCount();

    bool MoveNextCustomModifier();
    std::pair<mdToken, bool> GetCustomModifier();

    void AdvanceToArrayShape();

    bool MoveNextTypeArg();

    void AdvanceToType(); // for PTR/SZARRAY (no-op otherwise)

    void MoveToEnd(sigPtr& endPtr);
    SignatureBlob GetSigSpan()
    {
        sigPtr endPtr;
        MoveToEnd(endPtr);
        return { m_start, endPtr };
    }

    std::shared_ptr<SignatureTypeReaderState> m_typeReader;
    std::shared_ptr<MethodSignatureReaderState> m_methodSigReader;
    std::shared_ptr<SignatureArrayShapeReaderState> m_arrayShapeReader;
    bool m_isVoidPtr;

private:
    CustomModListReader m_modsReader;
    mdToken m_token;
    LONG m_typeArgCount;
    ULONG m_typeVarNumber;
    LONG m_currentTypeArg;

    enum
    {
        ARRAY_TYPE,
        ARRAY_SHAPE,
        FNPTR_METHOD_SIG,
        GENERICINST_INIT,
        GENERICINST_TYPE,
        PTR_INIT,
        PTR_CUSTOM_MOD,
        PTR_PRE_TYPE,
        PTR_TYPE,
        SZARRAY_INIT,
        SZARRAY_CUSTOM_MOD,
        SZARRAY_PRE_TYPE,
        SZARRAY_TYPE,
        END
    } m_where;
};

class SignatureArrayShapeReaderState : ReaderBase
{
public:
    SignatureArrayShapeReaderState(sigPtr start, sigPtr limit);

    bool MoveNextSize();
    ULONG GetLoBoundsCount();
    bool MoveNextLoBound();

    void MoveToEnd(sigPtr& endPtr);

    ULONG m_rank;
    ULONG m_sizesCount;
    ULONG m_currentSize;
    LONG m_currentLoBound;

private:
    ULONG m_loboundsCount;
    ULONG m_current;
    enum
    {
        INIT,
        SIZE,
        POST_SIZES,
        PRE_LOBOUNDS,
        LOBOUND,
        END
    } m_where;
};


MethodSignatureReaderState::MethodSignatureReaderState(sigPtr start, sigPtr limit) :
    ReaderBase(start, limit),
    m_currentParam(0),
    m_inVarArgParams(0),
    m_where(INIT)
{
    const uint16_t c_allowedCallConvs = 0x0511; // excludes localvar, field, property, genericinst & undefined values
    auto ccByte = ReadByte();
    auto callConv = ccByte & IMAGE_CEE_CS_CALLCONV_MASK;
    if (!((1 << callConv) & c_allowedCallConvs))
    {
        throw std::domain_error("MethodSignatureReaderState: sigBlob is not a MethodDefSig/MethodRefSig");
    }

    if (ccByte & IMAGE_CEE_CS_CALLCONV_GENERIC)
    {
        m_genericParamCount = ReadCompressedUnsigned();
    }
    else
    {
        m_genericParamCount = 0;
    }

    m_paramCount = ReadCompressedUnsigned();
}

bool MethodSignatureReaderState::MoveNextParam()
{
    if (m_where == END)
    {
        return false;
    }

    if (m_where == INIT)
    {
        // first "param" is return, which is always present
        m_currentParam = 0;
        m_paramReader.reset(new SignatureParamReaderState(m_ptr, m_limit, ParamKind::Return));
        m_where = PARAM;
        return true;
    }

    // m_where == PARAM
    m_paramReader->MoveToEnd(m_ptr);
    m_paramReader.reset();

    m_currentParam++;
    if (m_currentParam > m_paramCount)
    {
        m_where = END;
        return false;
    }

    if (PeekByte() == ELEMENT_TYPE_SENTINEL)
    {
        ReadByte();
        m_inVarArgParams = true;
    }

    m_paramReader.reset(new SignatureParamReaderState(m_ptr, m_limit, m_inVarArgParams ? ParamKind::VarArg : ParamKind::Normal));
    return true;
}

void MethodSignatureReaderState::MoveToEnd(sigPtr& endPtr)
{
    while (MoveNextParam()) {}
    endPtr = m_ptr;
}

SignatureParamReaderState::SignatureParamReaderState(sigPtr start, sigPtr limit, ParamKind kind) :
    ReaderBase(start, limit),
    m_kind(kind),
    m_where(INIT),
    m_modsReader(*this),
    m_isTypedByRef(false),
    m_isByRef(false),
    m_isVoid(false)
{
}

bool SignatureParamReaderState::MoveNextCustomModifier()
{
    if (m_where > CUSTOM_MOD)
    {
        return false;
    }

    bool hasMod = m_modsReader.TryRead();
    m_where = hasMod ? CUSTOM_MOD : PRE_TYPE;
    return hasMod;
}

std::pair<mdToken, bool> SignatureParamReaderState::GetCustomModifier()
{
    if (m_where != CUSTOM_MOD)
    {
        throw std::logic_error("SignatureParamReaderState::GetCustomModifier");
    }

    return m_modsReader.GetAsPair();
}

void SignatureParamReaderState::AdvanceToType()
{
    if (m_where > PRE_TYPE)
    {
        return;
    }

    if (m_where < PRE_TYPE)
    {
        while (MoveNextCustomModifier()) {}
        if (m_where != PRE_TYPE)
        {
            throw std::logic_error("SignatureParamReaderState::EnsureFlagsRead - bad internal state");
        }
    }

    byte b = PeekByte();
    if (b == ELEMENT_TYPE_TYPEDBYREF)
    {
        ReadByte();
        m_isTypedByRef = true;
        m_where = END;
        return;
    }

    if (b == ELEMENT_TYPE_BYREF)
    {
        ReadByte();
        m_isByRef = true;
    }

    if (b == ELEMENT_TYPE_VOID)
    {
        ReadByte();

        if (m_kind != ParamKind::Return)
        {
            throw std::domain_error("SignatureParamReaderState::EnsureFlagsRead - Void element in parameter");
        }

        m_isVoid = true;
        m_where = END;
        return;
    }

    m_where = TYPE;
    m_typeReader = std::make_shared<SignatureTypeReaderState>(m_ptr, m_limit);
}

void SignatureParamReaderState::MoveToEnd(sigPtr& endPtr)
{
    AdvanceToType();
    if (m_where < END)
    {
        m_typeReader->MoveToEnd(m_ptr);
    }

    endPtr = m_ptr;
}

SignatureTypeReaderState::SignatureTypeReaderState(sigPtr start, sigPtr limit) :
    ReaderBase(start, limit),
    m_token(0),
    m_typeArgCount(0),
    m_modsReader(*this),
    m_currentTypeArg(0),
    m_isVoidPtr(false)
{
    ReadByte(); // advance past the type kind byte
    switch (GetTypeKind())
    {
    case ELEMENT_TYPE_ARRAY:
        m_typeReader = std::make_shared<SignatureTypeReaderState>(m_ptr, m_limit);
        m_where = ARRAY_TYPE;
        break;

    case ELEMENT_TYPE_CLASS:
    case ELEMENT_TYPE_VALUETYPE:
        m_token = ReadTypeDefOrRefEncoded();
        m_where = END;
        break;

    case ELEMENT_TYPE_FNPTR:
        m_methodSigReader = std::make_shared<MethodSignatureReaderState>(m_ptr, m_limit);
        m_where = FNPTR_METHOD_SIG;
        break;

    case ELEMENT_TYPE_GENERICINST:
        ReadByte(); // advance past the class/valuetype byte
        m_token = ReadTypeDefOrRefEncoded();
        m_typeArgCount = ReadCompressedUnsigned(); // doc says it's signed but it doesn't appear to be
        m_where = GENERICINST_INIT;
        break;

    case ELEMENT_TYPE_MVAR:
    case ELEMENT_TYPE_VAR:
        m_typeVarNumber = ReadCompressedUnsigned();
        m_where = END;
        break;

    case ELEMENT_TYPE_PTR:
        m_where = PTR_INIT;
        break;

    case ELEMENT_TYPE_SZARRAY:
        m_where = SZARRAY_INIT;
        break;

    case ELEMENT_TYPE_BOOLEAN:
    case ELEMENT_TYPE_CHAR:
    case ELEMENT_TYPE_I1:
    case ELEMENT_TYPE_I2:
    case ELEMENT_TYPE_I4:
    case ELEMENT_TYPE_I8:
    case ELEMENT_TYPE_U1:
    case ELEMENT_TYPE_U2:
    case ELEMENT_TYPE_U4:
    case ELEMENT_TYPE_U8:
    case ELEMENT_TYPE_R4:
    case ELEMENT_TYPE_R8:
    case ELEMENT_TYPE_I:
    case ELEMENT_TYPE_U:
    case ELEMENT_TYPE_OBJECT:
    case ELEMENT_TYPE_STRING:
        m_where = END;
        break;

    default:
        throw std::domain_error("SignatureTypeReaderState - bad type element");
    }
}

bool SignatureTypeReaderState::MoveNextCustomModifier()
{
    decltype(m_where) where_custom_mod, where_next;
    if (m_where == PTR_INIT || m_where == PTR_CUSTOM_MOD)
    {
        where_custom_mod = PTR_CUSTOM_MOD;
        where_next = PTR_PRE_TYPE;
    }
    else if (m_where == SZARRAY_INIT || m_where == SZARRAY_CUSTOM_MOD)
    {
        where_custom_mod = SZARRAY_CUSTOM_MOD;
        where_next = SZARRAY_PRE_TYPE;
    }
    else
    {
        return false;
    }

    if (m_modsReader.TryRead())
    {
        m_where = where_custom_mod;
        return true;
    }
    else
    {
        m_where = where_next;
        return false;
    }
}

std::pair<mdToken, bool> SignatureTypeReaderState::GetCustomModifier()
{
    if (m_where != PTR_CUSTOM_MOD && m_where != SZARRAY_CUSTOM_MOD)
    {
        throw std::logic_error("SignatureTypeReaderState::GetCustomModifier - bad call");
    }
    return m_modsReader.GetAsPair();
}

mdToken SignatureTypeReaderState::GetToken()
{
    auto kind = GetTypeKind();
    if (kind != ELEMENT_TYPE_CLASS && kind != ELEMENT_TYPE_VALUETYPE && kind != ELEMENT_TYPE_GENERICINST)
    {
        throw std::logic_error("SignatureTypeReaderState::GetToken - invalid for this kind of type");
    }
    return m_token;
}

ULONG SignatureTypeReaderState::GetTypeVarNumber()
{
    auto kind = GetTypeKind();
    if (kind != ELEMENT_TYPE_MVAR && kind != ELEMENT_TYPE_VAR)
    {
        throw std::logic_error("SignatureTypeReaderState::GetTypeVarNumber - invalid for this kind of type");
    }
    return m_typeVarNumber;
}

LONG SignatureTypeReaderState::GetTypeArgCount()
{
    auto kind = GetTypeKind();
    if (kind != ELEMENT_TYPE_GENERICINST)
    {
        throw std::logic_error("SignatureTypeReaderState::GetTypeArgCount - invalid for this kind of type");
    }
    return m_typeArgCount;
}

void SignatureTypeReaderState::AdvanceToArrayShape()
{
    if (m_where == ARRAY_TYPE)
    {
        m_typeReader->MoveToEnd(m_ptr);
        m_typeReader.reset();

        m_arrayShapeReader.reset(new SignatureArrayShapeReaderState(m_ptr, m_limit));
        m_where = ARRAY_SHAPE;
    }
}

bool SignatureTypeReaderState::MoveNextTypeArg()
{
    if (m_where != GENERICINST_INIT && m_where != GENERICINST_TYPE)
    {
        return false;
    }

    if (m_where == GENERICINST_TYPE)
    {
        m_typeReader->MoveToEnd(m_ptr);
        m_typeReader.reset();
        m_currentTypeArg++;
    }

    if (m_currentTypeArg >= m_typeArgCount)
    {
        m_where = END;
        return false;
    }

    m_typeReader.reset(new SignatureTypeReaderState(m_ptr, m_limit));
    m_where = GENERICINST_TYPE;
    return true;
}

void SignatureTypeReaderState::AdvanceToType()
{
    while (MoveNextCustomModifier()) {}

    decltype(m_where) where_next;
    if (m_where == PTR_PRE_TYPE)
    {
        where_next = PTR_TYPE;
    }
    else if (m_where == SZARRAY_PRE_TYPE)
    {
        where_next = SZARRAY_TYPE;
    }
    else
    {
        return;
    }

    if (PeekByte() == ELEMENT_TYPE_VOID)
    {
        if (m_where != PTR_PRE_TYPE)
        {
            throw std::domain_error("SignatureTypeReaderState::AdvanceToType - unexpected VOID element");
        }

        ReadByte();
        m_isVoidPtr = true;
    }

    m_typeReader.reset(new SignatureTypeReaderState(m_ptr, m_limit));
    m_where = where_next;
}

void SignatureTypeReaderState::MoveToEnd(sigPtr& endPtr)
{
    switch (m_where)
    {
    case ARRAY_TYPE:
        AdvanceToArrayShape();
        //fallthru
    case ARRAY_SHAPE:
        m_arrayShapeReader->MoveToEnd(m_ptr);
        m_arrayShapeReader.reset();
        break;
    case FNPTR_METHOD_SIG:
        m_methodSigReader->MoveToEnd(m_ptr);
        m_methodSigReader.reset();
        break;
    case GENERICINST_INIT:
    case GENERICINST_TYPE:
        while (MoveNextTypeArg()) {}
        break;
    case PTR_INIT:
    case PTR_CUSTOM_MOD:
    case PTR_PRE_TYPE:
    case SZARRAY_INIT:
    case SZARRAY_CUSTOM_MOD:
    case SZARRAY_PRE_TYPE:
        AdvanceToType();
        //fallthru
    case PTR_TYPE:
    case SZARRAY_TYPE:
        m_typeReader->MoveToEnd(m_ptr);
        m_typeReader.reset();
        break;
    case END:
        break;
    default:
        throw std::logic_error("SignatureTypeReaderState::MoveToEnd - bad m_where");
    }

    m_where = END;
    endPtr = m_ptr;
}

SignatureArrayShapeReaderState::SignatureArrayShapeReaderState(sigPtr start, sigPtr limit) :
    ReaderBase(start, limit),
    m_loboundsCount(0),
    m_current(0),
    m_currentLoBound(0),
    m_currentSize(0)
{
    m_rank = ReadCompressedUnsigned();
    m_sizesCount = ReadCompressedUnsigned();
    m_where = INIT;
}

bool SignatureArrayShapeReaderState::MoveNextSize()
{
    if (m_where == SIZE)
    {
        m_current++;
    }
    else if (m_where == INIT)
    {
        m_current = 0;
    }
    else
    {
        return false;
    }

    if (m_current >= m_sizesCount)
    {
        m_where = POST_SIZES;
        return false;
    }

    m_currentSize = ReadCompressedUnsigned();
    m_where = SIZE;
    return true;
}

ULONG SignatureArrayShapeReaderState::GetLoBoundsCount()
{
    while (m_where < POST_SIZES)
    {
        MoveNextSize();
    }

    if (m_where == POST_SIZES)
    {
        m_loboundsCount = ReadCompressedUnsigned();
        m_where = PRE_LOBOUNDS;
    }

    return m_loboundsCount;
}

bool SignatureArrayShapeReaderState::MoveNextLoBound()
{
    GetLoBoundsCount();

    if (m_where == LOBOUND)
    {
        m_current++;
    }
    else if (m_where == PRE_LOBOUNDS)
    {
        m_current = 0;
    }
    else
    {
        return false;
    }

    if (m_current >= m_loboundsCount)
    {
        m_where = END;
        return false;
    }

    m_currentLoBound = ReadCompressedSigned();
    m_where = LOBOUND;
    return true;
}

void SignatureArrayShapeReaderState::MoveToEnd(sigPtr& endPtr)
{
    while (MoveNextLoBound()) {}
    endPtr = m_ptr;
}



ULONG ReaderBase::ReadCompressedUnsigned()
{
    COR_SIGNATURE b1 = ReadByte();
    if ((b1 & 0x80) == 0)
    {
        return b1;
    }

    b1 &= 0x7f;

    if ((b1 & 0x40) == 0)
    {
        return (b1 << 8) | ReadByte();
    }

    ULONG value = b1 & 0x3f;
    value = (value << 8) | ReadByte();
    value = (value << 8) | ReadByte();
    value = (value << 8) | ReadByte();
    return value;
}

LONG ReaderBase::ReadCompressedSigned()
{
    // This implementation relies on signed ints using 2's complement
    COR_SIGNATURE b1 = ReadByte();
    if ((b1 & 0x80) == 0)
    {
        uint8_t bits = (b1 >> 1);
        if (b1 & 1)
        {
            bits |= 0xc0;
        }
        return static_cast<int8_t>(bits);
    }

    b1 &= 0x7f;

    if ((b1 & 0x40) == 0)
    {
        uint16_t b12 = (b1 << 8) | ReadByte();
        uint16_t bits = b12 >> 1;
        if (b12 & 1)
        {
            bits |= 0xe000;
        }
        return static_cast<int16_t>(bits);
    }

    uint32_t b1234 = b1 & 0x3f;
    b1234 = (b1234 << 8) | ReadByte();
    b1234 = (b1234 << 8) | ReadByte();
    b1234 = (b1234 << 8) | ReadByte();
    
    uint32_t bits = b1234 >> 1;
    if (b1234 & 1)
    {
        bits |= 0xe0000000;
    }
    return static_cast<int32_t>(bits);
}

mdToken ReaderBase::ReadTypeDefOrRefEncoded()
{
    ULONG typeDefOrRef = ReadCompressedUnsigned();
    ULONG rid = typeDefOrRef >> 2;
    mdToken typeToken = 0;
    switch (typeDefOrRef & 3)
    {
    case 0: // type def
        return TokenFromRid(mdtTypeDef, rid);
    case 1: // type ref
        return TokenFromRid(mdtTypeRef, rid);
    case 2: // type spec
        return TokenFromRid(mdtTypeSpec, rid);
    default: // not defined
        throw std::domain_error("ReadTypeDefOrRefEncoded: bad token encoding");
    }
}



MethodSignatureReader::MethodSignatureReader(const SignatureBlob& sigBlob) :
    MethodSignatureReader(std::make_shared<MethodSignatureReaderState>(sigBlob))
{
}

MethodSignatureReader::MethodSignatureReader(const std::shared_ptr<MethodSignatureReaderState>& state) :
    m_state(state)
{
}

bool MethodSignatureReader::HasThis()
{
    return m_state->GetCallConvByte() & IMAGE_CEE_CS_CALLCONV_HASTHIS;
}

byte MethodSignatureReader::GetCallingConvention()
{
    return m_state->GetCallConvByte() & IMAGE_CEE_CS_CALLCONV_MASK;
}

ULONG MethodSignatureReader::GenericParamCount()
{
    return m_state->m_genericParamCount;
}

ULONG MethodSignatureReader::ParamCount()
{
    return m_state->m_paramCount;
}

bool MethodSignatureReader::MoveNextParam()
{
    return m_state->MoveNextParam();
}

SignatureParamReader MethodSignatureReader::GetParamReader()
{
    return SignatureParamReader(m_state->m_paramReader);
}

void MethodSignatureReader::Check(const SignatureBlob& sigBlob)
{
    MethodSignatureReaderState s(sigBlob);

    sigPtr endPtr;
    s.MoveToEnd(endPtr);

    if (endPtr != sigBlob.end())
    {
        throw std::domain_error("Signature blob contains bytes beyond end of signature");
    }
}

SignatureParamReader::SignatureParamReader(const std::shared_ptr<SignatureParamReaderState>& state) :
    m_state(state)
{
}

bool SignatureParamReader::IsReturn()
{
    return m_state->m_kind == ParamKind::Return;
}

bool SignatureParamReader::IsVarArg()
{
    return m_state->m_kind == ParamKind::VarArg;
}

bool SignatureParamReader::MoveNextCustomModifier()
{
    return m_state->MoveNextCustomModifier();
}

std::pair<mdToken, bool> SignatureParamReader::GetCustomModifier()
{
    return m_state->GetCustomModifier();
}

bool SignatureParamReader::IsTypedByRef()
{
    m_state->AdvanceToType();
    return m_state->m_isTypedByRef;
}

bool SignatureParamReader::IsVoid()
{
    m_state->AdvanceToType();
    return m_state->m_isVoid;
}

bool SignatureParamReader::IsByRef()
{
    m_state->AdvanceToType();
    return m_state->m_isByRef;
}

bool SignatureParamReader::HasType()
{
    m_state->AdvanceToType();
    return m_state->m_typeReader != nullptr;
}

SignatureTypeReader SignatureParamReader::GetTypeReader()
{
    m_state->AdvanceToType();
    return m_state->m_typeReader;
}

SignatureTypeReader::SignatureTypeReader(const std::shared_ptr<SignatureTypeReaderState>& state) :
    m_state(state)
{
}

CorElementType SignatureTypeReader::GetTypeKind()
{
    return m_state->GetTypeKind();
}

CorElementType SignatureTypeReader::GetGenericInstKind()
{
    return m_state->GetGenericInstKind();
}

bool SignatureTypeReader::MoveNextCustomModifier()
{
    return m_state->MoveNextCustomModifier();
}

std::pair<mdToken, bool> SignatureTypeReader::GetCustomModifier()
{
    return m_state->GetCustomModifier();
}

mdToken SignatureTypeReader::GetToken()
{
    return m_state->GetToken();
}

bool SignatureTypeReader::IsVoidPointer()
{
    m_state->AdvanceToType();
    return m_state->m_isVoidPtr;
}

SignatureTypeReader SignatureTypeReader::GetTypeReader()
{
    m_state->AdvanceToType();
    if (m_state->m_typeReader == nullptr)
    {
        if (m_state->m_isVoidPtr)
        {
            throw std::logic_error("SignatureTypeReader::GetTypeReader - void pointer has no type reader - use IsVoidPointer to guard");
        }
        throw std::logic_error("SignatureTypeReader::GetTypeReader - bad call");
    }
    return m_state->m_typeReader;
}

SignatureArrayShapeReader SignatureTypeReader::GetArrayShapeReader()
{
    m_state->AdvanceToArrayShape();
    if (m_state->m_arrayShapeReader == nullptr)
    {
        throw std::logic_error("SignatureTypeReader::GetArrayShapeReader - bad call");
    }
    return m_state->m_arrayShapeReader;
}

MethodSignatureReader SignatureTypeReader::GetMethodSignatureReader()
{
    if (m_state->m_methodSigReader == nullptr)
    {
        throw std::logic_error("SignatureTypeReader::GetMethodSignatureReader - bad call");
    }
    return m_state->m_methodSigReader;
}

ULONG SignatureTypeReader::GetVariableNumber()
{
    return m_state->GetTypeVarNumber();
}

LONG SignatureTypeReader::GetGenArgCount()
{
    return m_state->GetTypeArgCount();
}

bool SignatureTypeReader::MoveNextTypeArg()
{
    return m_state->MoveNextTypeArg();
}

SignatureBlob SignatureTypeReader::GetSigSpan()
{
    return m_state->GetSigSpan();
}

SignatureArrayShapeReader::SignatureArrayShapeReader(const std::shared_ptr<SignatureArrayShapeReaderState>& state) :
    m_state(state)
{
}

ULONG SignatureArrayShapeReader::GetRank()
{
    return m_state->m_rank;
}

ULONG SignatureArrayShapeReader::GetNumSizes()
{
    return m_state->m_sizesCount;
}

bool SignatureArrayShapeReader::MoveNextSize()
{
    return m_state->MoveNextSize();
}

ULONG SignatureArrayShapeReader::GetSize()
{
    return m_state->m_currentSize;
}

ULONG SignatureArrayShapeReader::GetNumLoBounds()
{
    return m_state->GetLoBoundsCount();
}

bool SignatureArrayShapeReader::MoveNextLoBound()
{
    return m_state->MoveNextLoBound();
}

LONG SignatureArrayShapeReader::GetLoBound()
{
    return m_state->m_currentLoBound;
}


// ============================ Writers start here ===========================

class WriterBase
{
public:
    WriterBase(std::vector<COR_SIGNATURE>& buffer) : m_buffer(buffer)
    {
    }

    void Append(COR_SIGNATURE byte)
    {
        m_buffer.push_back(byte);
    }

    void Append(const SignatureBlob& sigBlobSpan)
    {
        m_buffer.insert(m_buffer.end(), sigBlobSpan.begin(), sigBlobSpan.end());
    }

    void AppendCompressedUnsigned(ULONG value);

private:
    std::vector<COR_SIGNATURE>& m_buffer;
};

void WriterBase::AppendCompressedUnsigned(ULONG value)
{
    if (value < 0x80)
    {
        Append(static_cast<COR_SIGNATURE>(value));
        return;
    }

    if (value < 0x4000)
    {
        Append(static_cast<COR_SIGNATURE>((value >> 8) | 0x80));
        Append(static_cast<COR_SIGNATURE>(value & 0xff));
        return;
    }

    Append(static_cast<COR_SIGNATURE>((value >> 24) | 0xc0));
    Append(static_cast<COR_SIGNATURE>((value >> 16) & 0xff));
    Append(static_cast<COR_SIGNATURE>((value >> 8) & 0xff));
    Append(static_cast<COR_SIGNATURE>(value & 0xff));
}

class MethodSpecSignatureWriterState : private WriterBase
{
public:
    MethodSpecSignatureWriterState(std::vector<COR_SIGNATURE>& buffer, ULONG typeArgCount);

    void AddTypeArg(const SignatureBlob& sigBlobSpan)
    {
        if (m_typeArgsAdded >= m_typeArgCount)
        {
            throw std::logic_error("AddTypeArg - too many type args added");
        }

        Append(sigBlobSpan);
        m_typeArgsAdded++;
    }

private:
    ULONG m_typeArgCount;
    ULONG m_typeArgsAdded;
};

MethodSpecSignatureWriterState::MethodSpecSignatureWriterState(std::vector<COR_SIGNATURE>& buffer, ULONG typeArgCount) :
    WriterBase(buffer),
    m_typeArgCount(typeArgCount),
    m_typeArgsAdded(0)
{
    Append(IMAGE_CEE_CS_CALLCONV_GENERICINST);
    AppendCompressedUnsigned(typeArgCount);
}

MethodSpecSignatureWriter::MethodSpecSignatureWriter(std::vector<COR_SIGNATURE>& buffer, ULONG typeArgCount) :
    m_state(new MethodSpecSignatureWriterState(buffer, typeArgCount))
{
}

void MethodSpecSignatureWriter::AddTypeArg(const SignatureBlob& sigBlobSpan)
{
    m_state->AddTypeArg(sigBlobSpan);
}
