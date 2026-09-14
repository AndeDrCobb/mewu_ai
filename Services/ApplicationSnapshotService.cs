// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace mewu_ai_Assistant.Services;

internal sealed record ApplicationSnapshotTarget(long Handle,int ProcessId,long StartedAt)
{
    internal static ApplicationSnapshotTarget? FromWindow(IntPtr handle)
    {
        try
        {
            var root=GetAncestor(handle,2);if(root==IntPtr.Zero||!IsWindowVisible(root))return null;
            GetWindowThreadProcessId(root,out var pid);
            using var process=Process.GetProcessById(checked((int)pid));
            return new(root.ToInt64(),process.Id,process.StartTime.ToUniversalTime().Ticks);
        }
        catch(ArgumentException){return null;}
        catch(InvalidOperationException){return null;}
        catch(System.ComponentModel.Win32Exception){return null;}
    }

    internal bool IsCurrent()=>Equals(FromWindow(new IntPtr(Handle)));
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window,uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
}

internal sealed record ApplicationSnapshotDocument(string Title,string Text);

internal static class ApplicationSnapshotService
{
    internal const int MaximumCharacters=1_000_000;

    // Only called in the disposable worker. Providers may hang inside a COM
    // call; the parent kills that process rather than leaving blocked threads.
    internal static ApplicationSnapshotDocument Read(ApplicationSnapshotTarget target)
    {
        if(!target.IsCurrent())throw new InvalidOperationException("The selected window is no longer available.");
        var root=AutomationElement.FromHandle(new IntPtr(target.Handle));
        var title=root.Current.Name;if(title.Length>512)title=title[..512];
        var roots=GetContentRoots(target,root);
        // A Chromium document can be rooted at the native render host rather
        // than the WPF/WinUI frame. The first accessibility query may enable
        // its lazy provider; retry only that same target, within one deadline.
        var timer=Stopwatch.StartNew();
        for(var attempt=0;attempt<3;attempt++)
        {
            var content=ReadRoots(roots,attempt==0?TreeWalker.ControlViewWalker:TreeWalker.RawViewWalker,timer);
            if(!target.IsCurrent())throw new InvalidOperationException("The selected window changed during capture.");
            if(!string.IsNullOrWhiteSpace(content))return new(title,content);
            if(attempt<2)Thread.Sleep(150);
        }
        throw new InvalidDataException("This application does not expose readable text.");
    }

    internal static IReadOnlyList<AutomationElement> GetContentRoots(ApplicationSnapshotTarget target,AutomationElement root)
    {
        var roots=new List<AutomationElement>();var renderHosts=new List<IntPtr>();var count=0;
        EnumChildWindows(new IntPtr(target.Handle),(handle,_)=>
        {
            if(++count>256)return false;
            var name=new StringBuilder(256);GetClassName(handle,name,name.Capacity);
            if(IsWindowVisible(handle)&&name.ToString()=="Chrome_RenderWidgetHostHWND")renderHosts.Add(handle);
            return true;
        },IntPtr.Zero);
        if(count>256)throw new InvalidDataException("The application has too many native child windows.");
        foreach(var handle in renderHosts)roots.Add(AutomationElement.FromHandle(handle));
        if(roots.Count==0)roots.Add(root);
        return roots;
    }

    private static string ReadRoots(IReadOnlyList<AutomationElement> roots,TreeWalker walker,Stopwatch timer)
    {
        var parts=new List<string>();var characters=0;
        var stack=new Stack<(AutomationElement Element,int Depth)>();for(var i=roots.Count-1;i>=0;i--)stack.Push((roots[i],0));
        var visited=0;
        while(stack.TryPop(out var current))
        {
            if(++visited>10_000||timer.Elapsed>TimeSpan.FromSeconds(10)||current.Depth>48)
                throw new InvalidDataException("The application did not provide its content within the snapshot budget.");
            var element=current.Element;var info=element.Current;
            if(info.IsPassword)continue;
            if(element.TryGetCurrentPattern(TextPattern.Pattern,out var raw)&&raw is TextPattern text)
            {
                var value=text.DocumentRange.GetText(MaximumCharacters-characters+1);
                if(!string.IsNullOrWhiteSpace(value))
                {
                    characters=checked(characters+value.Length+2);
                    if(characters>MaximumCharacters)throw new InvalidDataException("The application text is too large for one snapshot.");
                    parts.Add(value);continue; // Do not duplicate the document's child text.
                }
            }
            // Include labels outside document ranges without revisiting text
            // already returned by a parent TextPattern provider.
            if(info.ControlType==ControlType.Text&&!string.IsNullOrWhiteSpace(info.Name))
            {
                characters=checked(characters+info.Name.Length+2);
                if(characters>MaximumCharacters)throw new InvalidDataException("The application text is too large for one snapshot.");
                parts.Add(info.Name);
            }
            var children=new List<AutomationElement>();var child=walker.GetFirstChild(element);
            while(child is not null)
            {
                if(children.Count+visited>10_000||timer.Elapsed>TimeSpan.FromSeconds(10))throw new InvalidDataException("The application content is too complex for one snapshot.");
                children.Add(child);child=walker.GetNextSibling(child);
            }
            for(var i=children.Count-1;i>=0;i--)stack.Push((children[i],current.Depth+1));
        }
        return string.Join("\n\n",parts);
    }

    private delegate bool ChildWindowCallback(IntPtr handle,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent,ChildWindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr handle,StringBuilder name,int maximum);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
}
