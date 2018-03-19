#define STRUCTLIB_API __declspec(dllexport)
#define STRUCTLIB_FUNC(RET) extern "C" __declspec(dllexport) RET __stdcall

struct SimpleStruct
{
	int i;
	int j;
};

struct StructWithArray
{
	int i[3];
	double j;
};

union TestUnion
{
	int integer;
	float decimal;
};

struct BitField
{
	int firstBit : 1;
	int lastBits : 31;
};

struct AsciiTest
{
	char SmallString[10];
	char* LargeString;
};

struct Utf16Test
{
	wchar_t SmallString[10];
	wchar_t* LargeString;
};

static_assert(sizeof(wchar_t) == 2, "Wide character isn't wide.");

STRUCTLIB_FUNC(SimpleStruct) GetSimpleStruct();

STRUCTLIB_FUNC(StructWithArray) PassThroughArray(StructWithArray param);

STRUCTLIB_FUNC(TestUnion) PassThroughUnion(TestUnion param);

STRUCTLIB_FUNC(BitField) PassThroughBitfield(BitField param);

STRUCTLIB_FUNC(AsciiTest) PassThroughAscii(AsciiTest param);

STRUCTLIB_FUNC(Utf16Test) PassThroughUtf(Utf16Test param);
