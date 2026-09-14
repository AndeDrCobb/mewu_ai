// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Diagnostics;
using System.Windows.Automation;
using mewu_ai_Assistant.Models;

namespace mewu_ai_Assistant.Services;

internal sealed record ApplicationScrollState(ScreenRect Bounds,double Position,double ViewSize,bool Scrollable,string? Failure=null);
internal sealed record ApplicationScrollCommand(string Action,double Position=0);

// Keep unreliable UIA providers outside the WPF process. The worker retains
// the exact scroll provider and original position, and restores on EOF too.
internal sealed class ApplicationScrollSession : IAsyncDisposable
{
    internal const string Argument="--application-scroll-worker";
    private readonly Process _process;
    private readonly Task _errors;
    private bool _restored;
    private bool _faulted;
    internal ApplicationScrollState Initial { get; private set; }=null!;
    private ApplicationScrollSession(Process process)
    {_process=process;_errors=process.StandardError.BaseStream.CopyToAsync(Stream.Null);}

    internal static async Task<ApplicationScrollSession> StartAsync(ApplicationSnapshotTarget target,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var process=new Process{StartInfo=new(Path.ChangeExtension(typeof(App).Assembly.Location,".exe")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        process.StartInfo.ArgumentList.Add(Argument);process.Start();var session=new ApplicationScrollSession(process);
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(12));
            await ApplicationSnapshotProcess.WriteMessage(process.StandardInput.BaseStream,target,4096,timeout.Token);
            session.Initial=Validate(await ApplicationSnapshotProcess.ReadMessage<ApplicationScrollState>(process.StandardOutput.BaseStream,4096,timeout.Token));
            return session;
        }
        catch{await session.DisposeAsync();throw;}
    }

    internal async Task<ApplicationScrollState> MoveAsync(double position,CancellationToken token)
        =>await SendAsync(new("move",position),token);

    private async Task<ApplicationScrollState> SendAsync(ApplicationScrollCommand command,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(_faulted)throw new IOException("Scroll session is unavailable.");
        // Once a packet starts, finish its response before observing user
        // cancellation, so restoration cannot consume a stale move response.
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        ApplicationScrollState state;
        try
        {
            await ApplicationSnapshotProcess.WriteMessage(_process.StandardInput.BaseStream,command,4096,timeout.Token);
            state=await ApplicationSnapshotProcess.ReadMessage<ApplicationScrollState>(_process.StandardOutput.BaseStream,4096,timeout.Token);
        }
        catch{_faulted=true;throw;}
        // A complete but invalid geometry packet does not desynchronize the
        // pipe. Keep restoration available even when that frame is rejected.
        return Validate(state);
    }

    internal static ApplicationScrollState Validate(ApplicationScrollState state)
    {
        if(state.Failure is not null)throw new IOException("Scroll worker failed: "+state.Failure);
        if(state.Bounds.IsEmpty)throw new InvalidDataException("scroll-bounds");
        if(!double.IsFinite(state.Position)||state.Position<-.000001||state.Position>100.000001)throw new InvalidDataException("scroll-position");
        if(!double.IsFinite(state.ViewSize)||state.ViewSize<=0||state.ViewSize>100.000001)throw new InvalidDataException("scroll-view-size");
        // WPF's provider can report 100.00000000000001 at the bottom.
        return state with{Position=Math.Clamp(state.Position,0,100),ViewSize=Math.Min(100,state.ViewSize)};
    }

    internal async Task RestoreAsync()
    {
        if(_restored)return;
        var restored=await SendAsync(new("restore"),CancellationToken.None);
        if(Initial.Scrollable&&Math.Abs(restored.Position-Initial.Position)>.2)throw new IOException($"The application did not restore its scroll position ({restored.Position:F3}/{Initial.Position:F3}).");
        _restored=true;
    }

    public async ValueTask DisposeAsync()
    {
        // Closing stdin also requests restoration when cancellation interrupts
        // the response pipe. Never send a second command into a partial packet.
        try{_process.StandardInput.Close();}catch(InvalidOperationException){}
        try{await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));}
        catch(TimeoutException){if(!_process.HasExited)_process.Kill(true);await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));}
        await _errors;_process.Dispose();
    }

    internal static async Task<int> RunWorkerAsync()
    {
        ScrollPattern? scroll=null;double originalVertical=0,originalHorizontal=ScrollPattern.NoScroll;
        var stage="prepare";var positionRestored=true;
        try
        {
            using var lifetime=new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var input=Console.OpenStandardInput();using var output=Console.OpenStandardOutput();
            var target=await ApplicationSnapshotProcess.ReadMessage<ApplicationSnapshotTarget>(input,4096,lifetime.Token);
            stage="discover";AutomationElement? element=null;
            for(var attempt=0;attempt<5;attempt++)
            {
                try{element=await Task.Run(()=>FindScrollable(target),lifetime.Token);if(element.TryGetCurrentPattern(ScrollPattern.Pattern,out _))break;}
                catch(ElementNotAvailableException){element=null;}
                if(attempt<4)await Task.Delay(250,lifetime.Token);
            }
            if(element is null)throw new ElementNotAvailableException();
            if(element.TryGetCurrentPattern(ScrollPattern.Pattern,out var raw)&&raw is ScrollPattern candidate&&candidate.Current.VerticallyScrollable)
            {scroll=candidate;originalVertical=Math.Clamp(scroll.Current.VerticalScrollPercent,0,100);originalHorizontal=scroll.Current.HorizontalScrollPercent;if(originalHorizontal!=ScrollPattern.NoScroll)originalHorizontal=Math.Clamp(originalHorizontal,0,100);}
            ApplicationScrollState State()
            {
                if(!target.IsCurrent())throw new InvalidOperationException("Source window changed.");
                var bounds=element.Current.BoundingRectangle;
                var x=(int)Math.Ceiling(bounds.Left);var y=(int)Math.Ceiling(bounds.Top);
                var rect=new ScreenRect(x,y,(int)Math.Floor(bounds.Right)-x,(int)Math.Floor(bounds.Bottom)-y);
                var current=scroll?.Current;
                return current is {VerticallyScrollable:true} info
                    ?new(rect,info.VerticalScrollPercent,info.VerticalViewSize,true)
                    :new(rect,0,100,false);
            }
            stage="initial-state";await ApplicationSnapshotProcess.WriteMessage(output,State(),4096,lifetime.Token);
            while(true)
            {
                ApplicationScrollCommand command;
                try{command=await ApplicationSnapshotProcess.ReadMessage<ApplicationScrollCommand>(input,4096,lifetime.Token);}
                catch(EndOfStreamException){break;}
                if(!target.IsCurrent())throw new InvalidOperationException("Source window changed.");
                if(command.Action=="restore")
                {
                    stage="restore";
                    if(scroll is not null)await Task.Run(()=>scroll.SetScrollPercent(originalHorizontal,originalVertical),lifetime.Token);
                }
                else if(command.Action=="move"&&double.IsFinite(command.Position)&&command.Position>=0&&command.Position<=100)
                {
                    stage="move";
                    positionRestored=false;
                    if(scroll is not null)await Task.Run(()=>scroll.SetScrollPercent(ScrollPattern.NoScroll,command.Position),lifetime.Token);
                }
                else throw new InvalidDataException("Invalid scroll command.");
                var expected=command.Action=="restore"?originalVertical:command.Position;
                ApplicationScrollState current;
                var settle=Stopwatch.StartNew();
                do{await Task.Delay(80,lifetime.Token);current=State();}
                while(current.Scrollable&&Math.Abs(current.Position-expected)>.05&&settle.Elapsed<TimeSpan.FromSeconds(2));
                if(command.Action=="restore")positionRestored=Math.Abs(current.Position-originalVertical)<=.05;
                await ApplicationSnapshotProcess.WriteMessage(output,current,4096,lifetime.Token);
            }
            return 0;
        }
        catch(Exception ex)
        {
            try{await ApplicationSnapshotProcess.WriteMessage(Console.OpenStandardOutput(),new ApplicationScrollState(default,0,0,false,stage+":"+ex.GetType().Name),4096,CancellationToken.None);}catch(Exception){}
            return 1;
        }
        finally{try{if(!positionRestored)scroll?.SetScrollPercent(originalHorizontal,originalVertical);}catch(Exception){}}
    }

    private static AutomationElement FindScrollable(ApplicationSnapshotTarget target)
    {
        if(!target.IsCurrent())throw new InvalidOperationException("Source window changed.");
        var root=AutomationElement.FromHandle(new IntPtr(target.Handle));
        var best=root;var largest=0d;var timer=Stopwatch.StartNew();var visited=0;
        var queue=new Queue<(AutomationElement Element,int Depth)>();foreach(var contentRoot in ApplicationSnapshotService.GetContentRoots(target,root))queue.Enqueue((contentRoot,0));
        while(queue.TryDequeue(out var node))
        {
            if(++visited>10000||timer.Elapsed>TimeSpan.FromSeconds(8)||node.Depth>48)throw new InvalidDataException("Scroll target too complex.");
            var info=node.Element.Current;if(info.IsPassword)continue;
            // Chromium initially exposes an empty placeholder document while
            // enabling accessibility. Request a bounded range to warm only
            // this provider, then rediscover it on the next bounded attempt.
            if(info.ControlType==ControlType.Document&&node.Element.TryGetCurrentPattern(TextPattern.Pattern,out var textProvider)&&textProvider is TextPattern text)
                _=text.DocumentRange.GetText(1);
            if(!info.IsOffscreen&&!info.BoundingRectangle.IsEmpty&&node.Element.TryGetCurrentPattern(ScrollPattern.Pattern,out var pattern)&&pattern is ScrollPattern scroll&&scroll.Current.VerticallyScrollable)
            {
                var area=info.BoundingRectangle.Width*info.BoundingRectangle.Height;
                if(area>largest){largest=area;best=node.Element;}
                continue; // Prefer the document viewport over nested scroll areas.
            }
            var child=TreeWalker.RawViewWalker.GetFirstChild(node.Element);
            while(child is not null)
            {
                if(queue.Count+visited>10000||timer.Elapsed>TimeSpan.FromSeconds(8))throw new InvalidDataException("Scroll target too complex.");
                queue.Enqueue((child,node.Depth+1));child=TreeWalker.RawViewWalker.GetNextSibling(child);
            }
        }
        return best;
    }
}
