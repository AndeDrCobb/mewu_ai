// SPDX-License-Identifier: MPL-2.0
using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        try
        {
            var history=(List<AiMessage>)typeof(CaptureOverlayWindow).GetField("_history",Private)!.GetValue(overlay)!;
            history.Add(new("user","这是一条会话窗口测试问题"));history.Add(new("assistant","这是冻结画面与聊天记录的测试回答"));
            typeof(CaptureOverlayWindow).GetField("_historyExpanded",Private)!.SetValue(overlay,true);
            ((FrameworkElement)overlay.FindName("PromptBarHost")).Visibility=Visibility.Visible;
            typeof(CaptureOverlayWindow).GetMethod("RefreshHistoryPreview",Private)!.Invoke(overlay,null);
            typeof(CaptureOverlayWindow).GetMethod("DetachConversationWindow",Private)!.Invoke(overlay,null);
            app.Dispatcher.Invoke(()=>{foreach(var _ in new[]{1,2,3})app.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render,new Action(()=>{}));});
            var workspace=app.Windows.OfType<ConversationWorkspaceWindow>().SingleOrDefault()??throw new InvalidOperationException("Detached conversation window was not created");
            var hostElement=(FrameworkElement)overlay.FindName("PromptBarHost");
            Check(checks,"detached-window-created",!overlay.IsVisible&&workspace.HasPromptBar(hostElement));
            Check(checks,"detached-window-resizable",workspace.ResizeMode==ResizeMode.CanResize&&workspace.MinWidth>=600&&workspace.MinHeight>=400);
            workspace.MinimizeToWidget();app.Dispatcher.Invoke(()=>{});
            var widget=app.Windows.OfType<ConversationFloatingWidget>().SingleOrDefault()??throw new InvalidOperationException("Floating widget was not created");
            Check(checks,"minimized-to-floating-widget",!workspace.IsVisible&&widget.IsVisible);
            workspace.RestoreFromWidget();app.Dispatcher.Invoke(()=>{});
            Check(checks,"floating-widget-restores",workspace.IsVisible&&!widget.IsVisible);
            overlay.RedockConversationWindow();app.Dispatcher.Invoke(()=>{});
            Check(checks,"redock-restores-overlay",overlay.IsVisible&&hostElement.Parent==overlay.FindName("Root"));
            typeof(CaptureOverlayWindow).GetField("_promptDetached",Private)!.SetValue(overlay,true);
            typeof(CaptureOverlayWindow).GetField("_historyExpanded",Private)!.SetValue(overlay,false);
            typeof(CaptureOverlayWindow).GetMethod("ToggleHistory",Private)!.Invoke(overlay,[null,new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)]);
            app.Dispatcher.Invoke(()=>{});
            var reordered=app.Windows.OfType<ConversationWorkspaceWindow>().SingleOrDefault()??throw new InvalidOperationException("History expansion did not upgrade a detached composer");
            Check(checks,"expand-after-drag-upgrades-window",reordered.HasPromptBar(hostElement));
            overlay.RedockConversationWindow();app.Dispatcher.Invoke(()=>{});
        }
        catch(Exception ex){failure=ex is TargetInvocationException tie?tie.InnerException?.Message??tie.Message:ex.Message;}
        finally
        {
            foreach(var window in app.Windows.OfType<ConversationWorkspaceWindow>().ToArray())window.CloseForOwnerExit();
            foreach(var widget in app.Windows.OfType<ConversationFloatingWidget>().ToArray())widget.CloseForOwnerExit();
            overlay.Close();
            File.WriteAllText(Path.GetFullPath(".codex-build/conversation-workspace-result.json"),JsonSerializer.Serialize(new{checks,failure}),new System.Text.UTF8Encoding(false));
            app.Shutdown(failure is null?0:1);
        }
    }

    private static void Check(List<string> checks,string name,bool passed){if(!passed)throw new InvalidOperationException(name);checks.Add(name);}
}
