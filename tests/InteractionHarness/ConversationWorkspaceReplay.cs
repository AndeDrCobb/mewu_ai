// SPDX-License-Identifier: MPL-2.0
using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Services;
using mewu_ai_Assistant.Views;
using Application=System.Windows.Application;

internal static class ConversationWorkspaceReplay
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Application app,AppHost host,CaptureOverlayWindow overlay)
    {
        var checks=new List<string>();string? failure=null;
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        try
        {
            // Open the actual window before detaching. The previous replay
            // bypassed Loaded and incorrectly accepted a blank content surface.
            Set(overlay,"_applicationSnapshotActive",true);
            overlay.Show();Pump(app);
            Set(overlay,"_applicationSnapshotActive",false);
            Set(overlay,"_conversationAiAvailable",true);
            var history=(List<AiMessage>)typeof(CaptureOverlayWindow).GetField("_history",Private)!.GetValue(overlay)!;
            history.Add(new("user","帮我解释这道题的解题思路。"));
            history.Add(new("assistant","先整理题目给出的条件，再写出等量关系。每一步都要保留依据，最后把答案代回原题检查。"));
            Set(overlay,"_historyExpanded",true);
            Set(overlay,"_promptDetached",true);
            Invoke(overlay,"RefreshHistoryPreview");Invoke(overlay,"PositionPromptBar");Pump(app);
            var workspace=(Border)overlay.FindName("PromptBar");
            var content=(Grid)overlay.FindName("PromptContent");
            var barHost=(FrameworkElement)overlay.FindName("PromptBarHost");
            var prompt=(System.Windows.Controls.TextBox)overlay.FindName("QuickPrompt");
            var historyScroll=(ScrollViewer)overlay.FindName("HistoryScroll");
            var frozenImage=((System.Windows.Controls.Image)overlay.FindName("DesktopImage")).Source;
            Check(checks,"dragged-chat-stays-in-overlay",workspace.Child==content&&overlay.IsAncestorOf(content)&&barHost.IsVisible);
            Check(checks,"no-independent-conversation-window",!app.Windows.Cast<Window>().Any(window=>window.GetType().Name=="ConversationWorkspaceWindow"));
            Check(checks,"no-speaker-labels",!Descendants((HistoryPreviewPanel)overlay.FindName("HistoryItems")).OfType<TextBlock>().Any(text=>text.Text is "AI" or "你" or "You"));
            prompt.Text="把第二步再讲详细一点";prompt.Focus();Pump(app);
            Save(workspace,"conversation-integrated.png");            var answer=(MarkdownAnswerView)overlay.FindName("AnswerText");
            Set(overlay,"_lastSubmittedPrompt","把第二步再讲详细一点");
            Invoke(overlay,"ResetAnswerForRequest");Pump(app);
            Check(checks,"send-keeps-unified-history",historyScroll.IsAncestorOf(answer)&&((ScrollViewer)overlay.FindName("ResponseScroll")).Content is null);
            Check(checks,"pending-has-no-fake-answer",!Descendants((HistoryPreviewPanel)overlay.FindName("HistoryItems")).OfType<System.Windows.Controls.TextBox>().Any(box=>box.Text.Contains("未收到 AI 回复")||box.Text.Contains("正在生成回答")));
            answer.Markdown="### 解题步骤\n\n1. **整理条件**：列出已知量与要求的量。\n2. **建立关系**：根据题意写出算式。\n3. **检查结果**：代入原条件，核对单位与范围。\n\n你可以继续圈选原截图中的某一步，我会针对那一步展开解释。";
            Invoke(overlay,"ShowAnswer");Pump(app);Save(workspace,"conversation-answer.png");
            Check(checks,"current-answer-uses-bubble-surface",((FrameworkElement)overlay.FindName("AnswerHeader")).Visibility==Visibility.Collapsed&&((Border)overlay.FindName("AnswerScroll")).BorderThickness.Left==1);
            Check(checks,"answer-and-history-fit",answer.ActualHeight>30&&historyScroll.ActualHeight>60&&prompt.IsVisible);
            Check(checks,"live-answer-shares-only-message-scroll",historyScroll.IsAncestorOf(answer)&&answer.VerticalScrollBarVisibility==ScrollBarVisibility.Disabled&&double.IsPositiveInfinity(((Border)overlay.FindName("AnswerScroll")).MaxHeight));
            history.Add(new("user","把第二步再讲详细一点"));history.Add(new("assistant",answer.Markdown));
            Set(overlay,"_lastSubmittedTurnRecorded",true);Invoke(overlay,"RefreshHistoryPreview");Pump(app);
            Check(checks,"completed-answer-not-duplicated",!Descendants((HistoryPreviewPanel)overlay.FindName("HistoryItems")).OfType<System.Windows.Controls.TextBox>().Any(box=>box.Text.Contains("解题步骤")));
            Save(workspace,"conversation-unified-completed.png");
            Set(overlay,"_lastSubmittedPrompt","下一题怎么做？");Invoke(overlay,"ResetAnswerForRequest");Pump(app);
            Check(checks,"next-turn-keeps-previous-answer",Descendants((HistoryPreviewPanel)overlay.FindName("HistoryItems")).OfType<System.Windows.Controls.TextBox>().Any(box=>box.Text.Contains("解题步骤")));
            Invoke(overlay,"RefreshHistoryPreview");Pump(app);
            Check(checks,"cancel-without-answer-has-no-fake-bubble",!Descendants((HistoryPreviewPanel)overlay.FindName("HistoryItems")).OfType<System.Windows.Controls.TextBox>().Any(box=>box.Text.Contains("未收到 AI 回复")));
            ((System.Windows.Controls.Button)overlay.FindName("MinimizeConversationButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Pump(app);
            var widget=app.Windows.OfType<ConversationFloatingWidget>().Single();
            Check(checks,"minimize-hides-canvas",!overlay.IsVisible&&widget.IsVisible);
            var next=new CaptureOverlayWindow(host);
            Set(next,"_applicationSnapshotActive",true);next.Show();Pump(app);
            Check(checks,"new-capture-acquires-slot",host.TryReacquireCaptureForOverlay(next));
            overlay.RestoreFromConversationWidget();Pump(app);
            Check(checks,"active-new-capture-keeps-old-session-minimized",!overlay.IsVisible&&widget.IsVisible);
            Set(next,"_conversationAiAvailable",true);Set(next,"_historyExpanded",true);
            Invoke(next,"RefreshHistoryPreview");
            ((System.Windows.Controls.Button)next.FindName("MinimizeConversationButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Pump(app);
            Check(checks,"multiple-independent-widgets",app.Windows.OfType<ConversationFloatingWidget>().Count()==2&&!next.IsVisible);
            overlay.RestoreFromConversationWidget();Pump(app);
            Check(checks,"restore-only-selected-session",!next.IsVisible&&app.Windows.OfType<ConversationFloatingWidget>().Count()==1);
            next.Close();Pump(app);
            Check(checks,"restore-keeps-draft-history",overlay.IsVisible&&prompt.Text=="把第二步再讲详细一点"&&history.Count>=2);
            Check(checks,"restore-keeps-original-frozen-frame",ReferenceEquals(frozenImage,((System.Windows.Controls.Image)overlay.FindName("DesktopImage")).Source));
            Check(checks,"restore-keeps-one-message-scroll",historyScroll.IsAncestorOf(answer));
            Set(overlay,"_historyExpanded",false);
            typeof(CaptureOverlayWindow).GetMethod("ToggleHistory",Private)!.Invoke(overlay,[null,new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)]);Pump(app);
            Check(checks,"drag-then-expand-stays-in-overlay",workspace.Child==content&&overlay.IsAncestorOf(content));            var picker=(Popup)overlay.FindName("ReferencePicker");picker.IsOpen=true;Pump(app);
            SendEscape(prompt);Pump(app);
            Check(checks,"escape-closes-picker-first",!picker.IsOpen&&workspace.IsVisible&&overlay.IsVisible);
            prompt.Focus();SendEscape(prompt);Pump(app);
            Check(checks,"escape-from-input-closes-session",!workspace.IsVisible&&!overlay.IsVisible);
        }
        catch(Exception ex){failure=ex is TargetInvocationException tie?tie.InnerException?.ToString()??tie.ToString():ex.ToString();}
        finally
        {
            foreach(var widget in app.Windows.OfType<ConversationFloatingWidget>().ToArray())widget.CloseForOwnerExit();
            overlay.Close();
            File.WriteAllText(Path.GetFullPath(".codex-build/conversation-workspace-result.json"),JsonSerializer.Serialize(new{checks,failure}),new System.Text.UTF8Encoding(false));
            Environment.ExitCode=failure is null?0:1;app.Shutdown(Environment.ExitCode);
        }
    }
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private)!.SetValue(target,value);
    private static void Invoke(object target,string name)=>target.GetType().GetMethod(name,Private)!.Invoke(target,null);
    private static void Pump(Application app){for(var i=0;i<3;i++)app.Dispatcher.Invoke(DispatcherPriority.Render,new Action(()=>{}));}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    private static void SendEscape(UIElement target){var source=PresentationSource.FromVisual(target)!;target.RaiseEvent(new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});}
    private static bool IsTransparent(FrameworkElement element){var bitmap=Render(element);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);return Enumerable.Range(0,pixels.Length/4).All(i=>pixels[i*4+3]==0);}
    private static RenderTargetBitmap Render(FrameworkElement element){element.UpdateLayout();var w=Math.Max(1,(int)Math.Ceiling(element.ActualWidth));var h=Math.Max(1,(int)Math.Ceiling(element.ActualHeight));var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,element.ActualWidth,element.ActualHeight),Stretch=Stretch.Fill},null,new Rect(0,0,w,h));var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);return bitmap;}
    private static void Save(FrameworkElement element,string name){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Render(element)));using var stream=File.Create(Path.GetFullPath(".codex-build/"+name));encoder.Save(stream);}
    private static void Check(List<string> checks,string name,bool passed){if(!passed)throw new InvalidOperationException(name);checks.Add(name);}
}
