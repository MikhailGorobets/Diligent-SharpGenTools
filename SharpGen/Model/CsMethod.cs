﻿// Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharpGen.CppModel;
using SharpGen.Transform;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpGen.Model;

public sealed class CsMethod : CsCallable
{
    protected override int MaxSizeReturnParameter => 4;

    public CsMethod(Ioc ioc, CppMethod cppMethod, string name) : base(ioc, cppMethod, name)
    {
        if (cppMethod == null)
            return;

        var tag = cppMethod.Rule;

        AllowProperty = tag.Property ?? AllowProperty;
        IsPersistent = tag.Persist ?? IsPersistent;
        Hidden = tag.Hidden ?? Hidden;
        CustomVtbl = tag.CustomVtbl ?? CustomVtbl;
        IsKeepImplementPublic = tag.IsKeepImplementPublic ?? IsKeepImplementPublic;

        // Apply any offset to the method's vtable
        var offset = tag.LayoutOffsetTranslate;

        Offset = cppMethod.Offset + offset;
        WindowsOffset = cppMethod.WindowsOffset + offset;
    }

    public bool Hidden { get; set; }
    private bool IsKeepImplementPublic { get; }
    public bool? AllowProperty { get; }
    public bool CustomVtbl { get; }
    public bool IsPersistent { get; }
    public int Offset { get; }
    public int WindowsOffset { get; }

    public ExpressionSyntax VTableOffsetExpression(PlatformDetectionType platform)
    {
        uint windowsOffset = (uint) WindowsOffset, offset = (uint) Offset;
        var windowsOffsetExpression =
            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(windowsOffset));
        var nonWindowsOffsetExpression =
            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(offset));

        if ((platform & PlatformDetectionType.Any) == PlatformDetectionType.Any && offset != windowsOffset)
            return ConditionalExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    Ioc.GlobalNamespace.GetTypeNameSyntax(WellKnownName.PlatformDetection),
                    IdentifierName("Is" + nameof(PlatformDetectionType.Windows))
                ),
                windowsOffsetExpression, nonWindowsOffsetExpression
            );

        // Use the Windows offset for the default offset in the vtable when the Windows platform is requested for compat reasons.
        return (platform & PlatformDetectionType.Windows) != 0
                   ? windowsOffsetExpression
                   : nonWindowsOffsetExpression;
    }

    public bool IsFunctionPointerInVtbl
    {
        get
        {
            static bool IsBlittable(CsMarshalCallableBase x)
            {
                var marshalType = x.MarshalType;

                if (marshalType is { IsBlittable: true })
                    return true;

                return !x.IsArray && !x.HasPointer && !x.IsString && marshalType is CsFundamentalType
                {
                    PrimitiveTypeIdentity: { Type: PrimitiveTypeCode.Char, PointerCount: 0 }
                };
            }

            return Ioc.GeneratorConfig.UseFunctionPointersInVtbl
                && Parameters.All(IsBlittable)
                && IsBlittable(ReturnValue);
        }
    }

    public bool IsPublicVisibilityForced(CsInterface parentInterface)
    {
        if (parentInterface == null)
            return false;

        return IsKeepImplementPublic || parentInterface.IsCallback && !Hidden;
    }

    public bool IsPublicVisibilityForced(params CsInterface[] parentInterfaces) =>
        parentInterfaces.Any(IsPublicVisibilityForced);

    public void SuffixName(string suffix)
    {
        Name += suffix;
    }
}