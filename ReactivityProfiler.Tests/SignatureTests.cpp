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
