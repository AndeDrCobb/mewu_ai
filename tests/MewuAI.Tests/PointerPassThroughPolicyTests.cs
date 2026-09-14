// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class PointerPassThroughPolicyTests
{
    [Fact]
    public void StaleLayoutsAndControlsCannotForwardClicks()
    {
        var policy=new PointerPassThroughPolicy(true,[new(-800,200,300,100)],1000);
        Assert.False(policy.Allows(-700,240,1100));
        Assert.True(policy.Allows(-900,240,1100));
        Assert.False(policy.Allows(-900,240,1250));
        Assert.False((policy with{Enabled=false}).Allows(-900,240,1100));
    }

    [Theory]
    [InlineData(-1919,-199)]
    [InlineData(-1,0)]
    [InlineData(0,200)]
    [InlineData(2399,1439)]
    public void AbsoluteInputRoundTripsThroughNegativeMixedMonitorDesktop(int x,int y)
    {
        var desktop=new ScreenRect(-1920,-200,4320,1640);
        var point=ScreenCoordinateService.ToAbsoluteMousePoint(x,y,desktop);
        Assert.Equal(x,(int)((long)point.X*desktop.Width/65536)+desktop.X);
        Assert.Equal(y,(int)((long)point.Y*desktop.Height/65536)+desktop.Y);
    }

    [Fact]
    public void OffDesktopInputClampsToTheNearestPixel()
    {
        Assert.Equal((0,65535),ScreenCoordinateService.ToAbsoluteMousePoint(-5000,9000,new(-100,0,200,300)));
        Assert.Throws<ArgumentException>(()=>ScreenCoordinateService.ToAbsoluteMousePoint(0,0,default));
    }
}
