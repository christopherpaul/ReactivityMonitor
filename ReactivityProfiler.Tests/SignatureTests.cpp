#include "pch.h"
#include "Signature.h"

TEST(MethodSignatureReader, CheckPassesOnGoodBlobs) {
    std::vector<std::vector<COR_SIGNATURE>> sigs =
    { 
        { 0x00, 0x01, 0x01, 0x0e },
        { 0x10, 0x01, 0x01, 0x15, 0x12, 0x35, 0x01, 0x1e, 0x00, 0x15, 0x12, 0x51, 0x01, 0x1e, 0x00 }
    };

    for (auto sig : sigs)
    {
        EXPECT_NO_THROW({ MethodSignatureReader::Check(simplespan<const COR_SIGNATURE>(sig.data(), sig.size())); })
            << sig;
    }
}

TEST(MethodSignatureReader, SubstitutesTypeArgsForToObservable) {
    std::vector<COR_SIGNATURE> methodRefSig =
    {
        0x10, 0x01, 0x01, 0x15, 0x12, 0x35, 0x01, 0x1e,
        0x00, 0x15, 0x12, 0x55, 0x01, 0x1e, 0x00
    };

    std::vector<COR_SIGNATURE> methodSpecSig =
    {
        0x0a, 0x01, 0x0e
    };

    MethodSignatureReader methodRefSigReader({ methodRefSig.data(), methodRefSig.size() });
    EXPECT_EQ(methodRefSigReader.GenericParamCount(), 1);
    EXPECT_EQ(methodRefSigReader.MoveNextParam(), true);
    
    auto returnReader = methodRefSigReader.GetParamReader();
    auto returnTypeReader = returnReader.GetTypeReader();
    EXPECT_EQ(returnTypeReader.GetTypeKind(), ELEMENT_TYPE_GENERICINST);
    
    EXPECT_EQ(returnTypeReader.MoveNextTypeArg(), true);
    auto typeArgReader = returnTypeReader.GetTypeReader();

    auto methodTypeArgSpans = MethodSpecSignatureReader::GetTypeArgSpans({ methodSpecSig.data(), methodSpecSig.size() });
    auto substituted = typeArgReader.SubstituteTypeArgs(std::vector<SignatureBlob>(), methodTypeArgSpans);

    EXPECT_EQ(substituted.size(), 1);
    EXPECT_EQ(substituted[0], ELEMENT_TYPE_STRING);
}

TEST(MethodSignatureReader, SubstitutesTypeArgsForZip) {
    // Zip`3
    std::vector<COR_SIGNATURE> methodRefSig =
    {
        0x10, 0x03, 0x03, 0x15, 0x12, 0x35, 0x01, 0x1e, 0x02, 0x15, 0x12, 0x35, 0x01, 0x1e, 0x00, 0x15,
        0x12, 0x35, 0x01, 0x1e, 0x01, 0x15, 0x12, 0x41, 0x03, 0x1e, 0x00, 0x1e, 0x01, 0x1e, 0x02
    };

    // Zip<string, long, string>
    std::vector<COR_SIGNATURE> methodSpecSig =
    {
        0x0a, 0x03, 0x0e, 0x0a, 0x0e
    };

    MethodSignatureReader methodRefSigReader({ methodRefSig.data(), methodRefSig.size() });
    EXPECT_EQ(methodRefSigReader.GenericParamCount(), 3);
    EXPECT_EQ(methodRefSigReader.MoveNextParam(), true);

    auto returnReader = methodRefSigReader.GetParamReader();
    auto returnTypeReader = returnReader.GetTypeReader();
    EXPECT_EQ(returnTypeReader.GetTypeKind(), ELEMENT_TYPE_GENERICINST);

    EXPECT_EQ(returnTypeReader.MoveNextTypeArg(), true);
    auto typeArgReader = returnTypeReader.GetTypeReader();

    auto methodTypeArgSpans = MethodSpecSignatureReader::GetTypeArgSpans({ methodSpecSig.data(), methodSpecSig.size() });
    auto substituted = typeArgReader.SubstituteTypeArgs(std::vector<SignatureBlob>(), methodTypeArgSpans);

    EXPECT_EQ(substituted.size(), 1);
    EXPECT_EQ(substituted[0], ELEMENT_TYPE_STRING);
}
