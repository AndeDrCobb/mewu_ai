// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using mewu_ai_Assistant.Models;
using mewu_ai_Assistant.Views;
using Application=System.Windows.Application;
using Button=System.Windows.Controls.Button;

internal static class HistoryMenuReplay
{
    internal static void Run(Application app,CaptureOverlayWindow overlay,bool english)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        Program.MarkReplayWindow(overlay,"上拉菜单布局验收 · 合成内容 · 自动关闭");
        overlay.Loaded+=(_,_)=>app.Dispatcher.BeginInvoke(new Action(async()=>
        {
            var checks=new List<string>();string? failure=null;
            var folder=Path.GetFullPath(".codex-build/history-menu");Directory.CreateDirectory(folder);
            var prefix=english?"en":"zh";
            try
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var button=(Button)overlay.FindName("NewConversationButton");
                var toggle=(Button)overlay.FindName("HistoryToggle");
                var panel=(Border)overlay.FindName("HistoryPanel");
                var bar=(Border)overlay.FindName("PromptBar");
                var scroll=(ScrollViewer)overlay.FindName("HistoryScroll");
                Invoke("SetPromptBarHidden",false,false);
                await Layout();Check("collapsed-hides-new-chat",!button.IsVisible);Save("collapsed");
                var history=(List<AiMessage>)typeof(CaptureOverlayWindow).GetField("_history",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(overlay)!;
                history.Add(new("user",english?"Explain the second question.":"帮我解释一下第 2 题。"));
                history.Add(new("assistant",english?"Start with the known conditions, then check each step.":"先找出题目给出的条件，再逐步检查解题过程。"));
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Layout();
                Check("expanded-shows-new-chat",button.IsVisible&&panel.IsAncestorOf(button));
                Check("header-outside-scrolling-list",!scroll.IsAncestorOf(button));
                var bubbleRows=Descendants((HistoryPreviewPanel)overlay.FindName("HistoryItems")).OfType<Border>().Where(border=>border.Child is StackPanel&&border.CornerRadius.TopLeft>=14).ToArray();
                Check("expanded-history-uses-left-right-bubbles",bubbleRows.Any(border=>border.HorizontalAlignment==System.Windows.HorizontalAlignment.Left)&&bubbleRows.Any(border=>border.HorizontalAlignment==System.Windows.HorizontalAlignment.Right));
                CheckBounds();Save("expanded");
                scroll.ScrollToEnd();await Layout();Check("action-stays-visible-after-scroll",button.IsVisible);CheckBounds();
                // Inspect the actual production panel at a narrow width too.
                typeof(CaptureOverlayWindow).GetField("_positioningPromptBar",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(overlay,true);
                bar.Width=320;bar.UpdateLayout();CheckBounds();Save("narrow");
                typeof(CaptureOverlayWindow).GetField("_positioningPromptBar",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(overlay,false);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Layout();
                Check("new-chat-clears-context-and-collapses",history.Count==1&&history[0].Role=="system"&&!button.IsVisible);
                Save("new-chat");
                void CheckBounds()
                {
                    var bounds=new Rect(button.TranslatePoint(new System.Windows.Point(),panel),button.RenderSize);
                    Check("action-fits-"+bar.ActualWidth,bounds.Left>=0&&bounds.Right<=panel.ActualWidth+.1&&bounds.Bottom<=panel.ActualHeight+.1);
                    var scrollBounds=new Rect(scroll.TranslatePoint(new System.Windows.Point(),panel),scroll.RenderSize);
                    Check("list-fits-"+bar.ActualWidth,scrollBounds.Bottom<=panel.ActualHeight+.1&&scrollBounds.Top>=bounds.Bottom);
                }
                async Task Layout(){await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);overlay.UpdateLayout();}
                void Save(string name)
                {
                    var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(bar){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(bar.RenderSize));
                    var image=new RenderTargetBitmap((int)Math.Ceiling(bar.ActualWidth),(int)Math.Ceiling(bar.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(visual);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(folder,prefix+"-"+name+".png"));encoder.Save(file);
                }
            }
            catch(Exception ex){failure=ex.ToString();Environment.ExitCode=1;}
            finally{File.WriteAllText(Path.Combine(folder,prefix+"-result.json"),JsonSerializer.Serialize(new{checks,failure}));overlay.Close();app.Shutdown(Environment.ExitCode);}
            void Check(string name,bool success){if(!success)throw new InvalidOperationException(name);checks.Add(name);}
            IEnumerable<DependencyObject> Descendants(DependencyObject root){for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
            object? Invoke(string name,params object[] values)=>typeof(CaptureOverlayWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,values);
        }));
    }
}
