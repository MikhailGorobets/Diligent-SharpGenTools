using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Struct;

public partial struct StructWithCallback
{
    public delegate void ModifyDelegate(ref SimpleStruct desc);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void ModifyCallbackImpl(IntPtr desc, IntPtr pUserData) { }
}

public static partial class Native
{
    public delegate void ModifyDelegate(ref SimpleStruct desc);
}

public class SimpleStructTests
{
    [Fact]
    public void SimpleStructMarshalledCorrectly()
    {
        var simple = Functions.GetSimpleStruct();
        Assert.Equal(10, simple.I);
        Assert.Equal(3, simple.J);
    }

    [Fact]
    public void DependencyPlatformStructCheck()
    {
        Assert.Equal(Environment.Is64BitProcess ? 4 : 8, Unsafe.SizeOf<StructPlatformDependency>());
    }
}