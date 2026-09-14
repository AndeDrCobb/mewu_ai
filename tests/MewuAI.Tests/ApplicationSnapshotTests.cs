// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using mewu_ai_Assistant.Services;
using Xunit;

namespace MewuAI.Tests;

public sealed class ApplicationSnapshotTests
{
    [Fact]
    public void TextChunkingKeepsRepeatedParagraphsBlankLinesAndUnicode()
    {
        var text=string.Concat(Enumerable.Repeat("重复 paragraph 😀\n\n",2000))+new string('长',10000)+"😀末尾";
        var chunks=ApplicationSnapshotRenderer.TextChunks(text).ToArray();
        Assert.Equal(text,string.Concat(chunks));
        Assert.All(chunks,chunk=>{Assert.InRange(chunk.Length,1,8192);Assert.False(char.IsHighSurrogate(chunk[^1]));});
    }

    [Fact]
    public async Task IsolatedReaderGetsOffscreenTextWithoutScrollingAndRejectsClosedWindow()
    {
        var ready=new TaskCompletionSource<(Window Window,TextBox Text,ApplicationSnapshotTarget Target)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                var text=new TextBox{Text="SNAPSHOT_FIRST\n"+string.Join('\n',Enumerable.Range(1,200).Select(n=>$"Snapshot row {n:000}"))+"\nSNAPSHOT_LAST",IsReadOnly=true,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=160};
                var panel=new StackPanel();panel.Children.Add(text);panel.Children.Add(new PasswordBox{Password="SECRET_SHOULD_NOT_APPEAR"});
                var window=new Window{Title="Mewu synthetic snapshot test",Width=440,Height=260,Content=panel,ShowActivated=false,ShowInTaskbar=false};
                window.Show();window.UpdateLayout();
                window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>ready.TrySetResult((window,text,ApplicationSnapshotTarget.FromWindow(new WindowInteropHelper(window).Handle)!))));
                Dispatcher.Run();
            }
            catch(Exception ex){ready.TrySetException(ex);}
        }){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        var fixture=await ready.Task.WaitAsync(TimeSpan.FromSeconds(15),TestContext.Current.CancellationToken);
        try
        {
            var before=await fixture.Window.Dispatcher.InvokeAsync(()=>fixture.Text.VerticalOffset);
            var document=await ApplicationSnapshotProcess.ReadAsync(fixture.Target,TestContext.Current.CancellationToken);
            Assert.Contains("SNAPSHOT_FIRST",document.Text);Assert.Contains("Snapshot row 100",document.Text);Assert.Contains("SNAPSHOT_LAST",document.Text);
            Assert.DoesNotContain("SECRET_SHOULD_NOT_APPEAR",document.Text);
            Assert.Equal(before,await fixture.Window.Dispatcher.InvokeAsync(()=>fixture.Text.VerticalOffset));
            var image=await Task.Run(()=>ApplicationSnapshotRenderer.Render(document,1000,true,TestContext.Current.CancellationToken),TestContext.Current.CancellationToken);
            Assert.True(image.IsFrozen);Assert.True(image.PixelHeight>3000);
            using var cancelled=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ApplicationSnapshotProcess.ReadAsync(fixture.Target,cancelled.Token));
            await fixture.Window.Dispatcher.InvokeAsync(fixture.Window.Close);
            Assert.False(fixture.Target.IsCurrent());
            await Assert.ThrowsAsync<InvalidOperationException>(()=>ApplicationSnapshotProcess.ReadAsync(fixture.Target,TestContext.Current.CancellationToken));
        }
        finally
        {
            await fixture.Window.Dispatcher.InvokeAsync(()=>{fixture.Window.Close();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);});
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        }
    }
}
