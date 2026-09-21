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
using Color=System.Windows.Media.Color;
using MouseEventArgs=System.Windows.Input.MouseEventArgs;

internal static class ConversationWorkspaceReplay
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Application app,AppHost host,CaptureOverlayWindow overlay)
    {
        var checks=new List<string>();string? failure=null;
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        try
        {
            Program.MarkReplayWindow(overlay,"最小化会话状态验收 · 合成内容 · 自动关闭");
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
            using var pending=new CancellationTokenSource();
            Set(overlay,"_request",pending);Invoke(overlay,"BeginConversationProgress",pending);
            Invoke(overlay,"ShowReasoning","正在检查已知条件。",pending);Pump(app);
            ((System.Windows.Controls.Button)overlay.FindName("MinimizeConversationButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Pump(app);
            var widget=app.Windows.OfType<ConversationFloatingWidget>().Single();
            Check(checks,"minimize-hides-canvas",!overlay.IsVisible&&widget.IsVisible);
            var preview=Descendants(widget).OfType<TextBlock>().Single(text=>text.Name=="ConversationProgressPreview");
            var spinner=Descendants(widget).OfType<Canvas>().Single(element=>element.Name=="ConversationThinkingSpinner");
            var dot=Descendants(widget).OfType<System.Windows.Shapes.Ellipse>().Single(element=>element.Name=="ConversationStatusDot");
            Check(checks,"pending-widget-shows-dotted-spinner-and-reasoning",spinner.IsVisible&&spinner.Children.Count==8&&!dot.IsVisible&&preview.Text.Contains("已知条件")&&spinner.RenderTransform.HasAnimatedProperties==SystemParameters.ClientAreaAnimation);
            if(SystemParameters.ClientAreaAnimation)
            {
                var rotation=(RotateTransform)spinner.RenderTransform;var previousAngle=rotation.Angle;
                var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(240)};
                timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
                Check(checks,"thinking-dots-actually-rotate",Math.Abs(rotation.Angle-previousAngle)>1);
            }
            var marker=spinner.TranslatePoint(new System.Windows.Point(spinner.ActualWidth/2,spinner.ActualHeight/2),widget);
            Check(checks,"spinner-fits-upper-left-corner",marker.X>=0&&marker.Y>=0&&marker.X<20&&marker.Y<20);
            Invoke(overlay,"ShowReasoning",new string('旧',600)+"现在核对末尾单位🧠。",pending);Pump(app);
            Check(checks,"minimized-reasoning-preview-follows-latest-bounded-tail",preview.Text.EndsWith("单位🧠。",StringComparison.Ordinal)&&!preview.Text.Contains('旧')&&preview.Text.Length<110&&!overlay.IsVisible);
            Save(widget,"conversation-widget-thinking.png");
            widget.Hide();Pump(app);Check(checks,"hidden-widget-stops-spinner",!spinner.RenderTransform.HasAnimatedProperties);
            widget.Show();Pump(app);Check(checks,"reshown-pending-widget-resumes-spinner",spinner.RenderTransform.HasAnimatedProperties==SystemParameters.ClientAreaAnimation);
            Invoke(overlay,"FinishReasoning","最终核验：单位一致，推导成立。");
            Invoke(overlay,"RefreshAnswer","最终回答正文。");Pump(app);
            Check(checks,"reasoning-preview-takes-priority-over-answer",preview.Text.EndsWith("推导成立。",StringComparison.Ordinal)&&preview.ToolTip.ToString()!.Contains("最终核验：单位一致，推导成立。")&&spinner.IsVisible&&!dot.IsVisible);
            Invoke(overlay,"FinishConversationProgress",pending,true);Set(overlay,"_request",null!);Pump(app);
            Check(checks,"completed-widget-turns-green-without-restoring",!spinner.IsVisible&&!spinner.RenderTransform.HasAnimatedProperties&&dot.IsVisible&&((SolidColorBrush)dot.Fill).Color==Color.FromRgb(34,170,106)&&!overlay.IsVisible);
            Save(widget,"conversation-widget-completed.png");
            Invoke(overlay,"ShowReasoning","旧请求迟到内容",pending);Pump(app);
            Check(checks,"completed-widget-rejects-late-reasoning",preview.Text.EndsWith("推导成立。",StringComparison.Ordinal)&&!preview.ToolTip.ToString()!.Contains("迟到"));
            foreach(var cancel in new[]{false,true})
            {
                using var stopped=new CancellationTokenSource();Set(overlay,"_request",stopped);Invoke(overlay,"BeginConversationProgress",stopped);
                Invoke(overlay,"FinishConversationProgress",pending,true);
                Check(checks,"old-completion-cannot-finish-new-widget-request",spinner.IsVisible&&!dot.IsVisible);
                Invoke(overlay,"RefreshAnswer","不返回思考的模型也显示最新正文。");Pump(app);
                Check(checks,"answer-preview-used-without-reasoning",preview.Text.EndsWith("最新正文。",StringComparison.Ordinal)&&preview.ToolTip.ToString()!.Contains("不返回思考的模型也显示最新正文。"));
                if(cancel)stopped.Cancel();
                Invoke(overlay,"FinishConversationProgress",stopped,cancel);Set(overlay,"_request",null!);Pump(app);
                Check(checks,cancel?"canceled-widget-is-not-green":"failed-widget-is-not-green",!spinner.RenderTransform.HasAnimatedProperties&&dot.IsVisible&&((SolidColorBrush)dot.Fill).Color!=Color.FromRgb(34,170,106));
            }
            var next=new CaptureOverlayWindow(host);
            Set(next,"_applicationSnapshotActive",true);next.Show();Pump(app);
            Check(checks,"new-capture-acquires-slot",host.TryReacquireCaptureForOverlay(next));
            overlay.RestoreFromConversationWidget();Pump(app);
            Check(checks,"active-new-capture-keeps-old-session-minimized",!overlay.IsVisible&&widget.IsVisible);
            Set(next,"_conversationAiAvailable",true);Set(next,"_historyExpanded",true);
            Invoke(next,"RefreshHistoryPreview");
            ((System.Windows.Controls.Button)next.FindName("MinimizeConversationButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Pump(app);
            Check(checks,"multiple-independent-widgets",app.Windows.OfType<ConversationFloatingWidget>().Count()==2&&!next.IsVisible);
            var otherWidget=app.Windows.OfType<ConversationFloatingWidget>().Single(item=>!ReferenceEquals(item,widget));
            var otherPreview=Descendants(otherWidget).OfType<TextBlock>().Single(text=>text.Name=="ConversationProgressPreview");
            Check(checks,"other-widget-does-not-inherit-request-status",otherPreview.Text==LocalizationService.T("点击恢复对话","Click to restore"));
            overlay.RestoreFromConversationWidget();Pump(app);
            Check(checks,"restore-only-selected-session",!next.IsVisible&&app.Windows.OfType<ConversationFloatingWidget>().Count()==1);
            Check(checks,"restored-widget-releases-animation",!spinner.RenderTransform.HasAnimatedProperties);
            Set(next,"_applicationSnapshotActive",false);next.Close();Pump(app);
            Check(checks,"restore-keeps-draft-history",overlay.IsVisible&&prompt.Text=="把第二步再讲详细一点"&&history.Count>=2);
            Check(checks,"restore-keeps-original-frozen-frame",ReferenceEquals(frozenImage,((System.Windows.Controls.Image)overlay.FindName("DesktopImage")).Source));
            Check(checks,"restore-keeps-one-message-scroll",historyScroll.IsAncestorOf(answer));
            Set(overlay,"_historyExpanded",false);
            typeof(CaptureOverlayWindow).GetMethod("ToggleHistory",Private)!.Invoke(overlay,[null,new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)]);Pump(app);
            Check(checks,"drag-then-expand-stays-in-overlay",workspace.Child==content&&overlay.IsAncestorOf(content));            var picker=(Popup)overlay.FindName("ReferencePicker");picker.IsOpen=true;Pump(app);
            SendEscape(prompt);Pump(app);
            Check(checks,"escape-closes-picker-first",!picker.IsOpen&&workspace.IsVisible&&overlay.IsVisible);
            prompt.Focus();SendEscape(prompt);Pump(app);
            widget=app.Windows.OfType<ConversationFloatingWidget>().Single();
            Check(checks,"escape-from-restored-input-minimizes",!overlay.IsVisible&&widget.IsVisible&&app.Windows.Cast<Window>().Contains(overlay));
            overlay.RestoreFromConversationWidget();Pump(app);
            Check(checks,"second-restore-keeps-frame-and-draft",overlay.IsVisible&&prompt.Text=="把第二步再讲详细一点"&&ReferenceEquals(frozenImage,((System.Windows.Controls.Image)overlay.FindName("DesktopImage")).Source));
            SendEscape(prompt);Pump(app);
            widget=app.Windows.OfType<ConversationFloatingWidget>().Single();
            var close=Descendants(widget).OfType<System.Windows.Controls.Button>().Single(button=>button.Name=="CloseConversationButton");
            widget.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseLeaveEvent});
            Check(checks,"widget-close-hidden-until-hover",close.Visibility==Visibility.Hidden);
            widget.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});Pump(app);
            Check(checks,"widget-hover-shows-close",close.IsVisible);
            using var closingRequest=new CancellationTokenSource();Set(overlay,"_request",closingRequest);Invoke(overlay,"BeginConversationProgress",closingRequest);Pump(app);
            var closingSpinner=Descendants(widget).OfType<Canvas>().Single(element=>element.Name=="ConversationThinkingSpinner");
            Check(checks,"closing-scenario-has-active-spinner",closingSpinner.IsVisible&&closingSpinner.RenderTransform.HasAnimatedProperties==SystemParameters.ClientAreaAnimation);
            Save(widget,"conversation-widget-hover.png");
            close.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Pump(app);
            Check(checks,"widget-close-ends-session",!app.Windows.Cast<Window>().Contains(overlay)&&!app.Windows.OfType<ConversationFloatingWidget>().Any());
            Check(checks,"closing-widget-stops-animation-and-cancels-request",!closingSpinner.RenderTransform.HasAnimatedProperties&&closingRequest.IsCancellationRequested);
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
    private static void Invoke(object target,string name,params object[] arguments)=>target.GetType().GetMethod(name,Private)!.Invoke(target,arguments);
    private static void Pump(Application app){for(var i=0;i<3;i++)app.Dispatcher.Invoke(DispatcherPriority.Render,new Action(()=>{}));}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    private static void SendEscape(UIElement target){var source=PresentationSource.FromVisual(target)!;target.RaiseEvent(new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});}
    private static bool IsTransparent(FrameworkElement element){var bitmap=Render(element);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);return Enumerable.Range(0,pixels.Length/4).All(i=>pixels[i*4+3]==0);}
    private static RenderTargetBitmap Render(FrameworkElement element){element.UpdateLayout();var w=Math.Max(1,(int)Math.Ceiling(element.ActualWidth));var h=Math.Max(1,(int)Math.Ceiling(element.ActualHeight));var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(element){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,element.ActualWidth,element.ActualHeight),Stretch=Stretch.Fill},null,new Rect(0,0,w,h));var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);return bitmap;}
    private static void Save(FrameworkElement element,string name){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Render(element)));using var stream=File.Create(Path.GetFullPath(".codex-build/"+name));encoder.Save(stream);}
    private static void Check(List<string> checks,string name,bool passed){if(!passed)throw new InvalidOperationException(name);checks.Add(name);}
}
