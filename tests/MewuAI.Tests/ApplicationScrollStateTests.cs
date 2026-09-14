// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ApplicationScrollStateTests
{
    [Theory]
    [InlineData(100.00000000000001,100)]
    [InlineData(-0.00000000000001,0)]
    [InlineData(52.25,52.25)]
    public void ProviderFloatingPointRoundingDoesNotBreakRestoration(double position,double expected)
    {
        var state=ApplicationScrollSession.Validate(new(new ScreenRect(-200,40,800,600),position,20,true));
        Assert.Equal(expected,state.Position);
    }

    [Theory]
    [InlineData(double.NaN,20)]
    [InlineData(double.PositiveInfinity,20)]
    [InlineData(101,20)]
    [InlineData(-1,20)]
    [InlineData(20,0)]
    [InlineData(20,101)]
    public void InvalidProviderGeometryIsRejected(double position,double view)
        =>Assert.Throws<InvalidDataException>(()=>ApplicationScrollSession.Validate(new(new ScreenRect(0,0,800,600),position,view,true)));
}
