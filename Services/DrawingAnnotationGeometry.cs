// SPDX-FileCopyrightText: 2026 Abner Stephen and contributors
// SPDX-License-Identifier: MPL-2.0
using System.Windows;

namespace mewu_ai_Assistant.Services;

internal static class DrawingAnnotationGeometry
{
    internal static Point ConstrainEllipseEndToCircle(Point start,Point end,Size canvas)
        =>ConstrainShapeEndToSquare(start,end,canvas);

    internal static Point ConstrainShapeEndToSquare(Point start,Point end,Size canvas)
    {
        if(!ValidCanvas(canvas)||!InsideCanvas(start,canvas)||!FinitePoint(end))return start;
        var directionX=end.X<start.X?-1d:1d;var directionY=end.Y<start.Y?-1d:1d;
        var desired=Math.Max(Math.Abs(end.X-start.X),Math.Abs(end.Y-start.Y));
        return ConstrainAlongDirection(start,directionX,directionY,desired,canvas);
    }

    internal static Point ConstrainLineEnd(Point start,Point end,Size canvas)
    {
        if(!ValidCanvas(canvas)||!InsideCanvas(start,canvas)||!FinitePoint(end))return start;
        var dx=end.X-start.X;var dy=end.Y-start.Y;
        if(!IsFinite(dx)||!IsFinite(dy))return start;
        var horizontal=Math.Abs(dx);var vertical=Math.Abs(dy);
        // Nearest multiple of 45 degrees, with the pointer projected onto that ray.
        // Cardinal rays win the exact 22.5-degree ties, avoiding unstable axis changes.
        var diagonalThreshold=Math.Sqrt(2)-1;
        if(vertical<=horizontal*diagonalThreshold)
            return ConstrainAlongDirection(start,Math.Sign(dx),0,horizontal,canvas);
        if(horizontal<=vertical*diagonalThreshold)
            return ConstrainAlongDirection(start,0,Math.Sign(dy),vertical,canvas);
        return ConstrainAlongDirection(start,Math.Sign(dx),Math.Sign(dy),horizontal/2+vertical/2,canvas);
    }

    private static Point ConstrainAlongDirection(Point start,double directionX,double directionY,double distance,Size canvas)
    {
        // Clip a single distance along the chosen ray; independent X/Y clamps would
        // bend a diagonal line or turn a square into a rectangle at the canvas edge.
        if(directionX!=0)distance=Math.Min(distance,directionX>0?canvas.Width-start.X:start.X);
        if(directionY!=0)distance=Math.Min(distance,directionY>0?canvas.Height-start.Y:start.Y);
        return new Point(start.X+directionX*distance,start.Y+directionY*distance);
    }

    internal static Vector ConstrainTranslation(Rect bounds,Vector requested,Size canvas)
    {
        if(bounds.IsEmpty||!IsFinite(requested.X)||!IsFinite(requested.Y)||!IsFinite(canvas.Width)||!IsFinite(canvas.Height))return new Vector();
        var minimumX=-bounds.Left;var maximumX=Math.Max(minimumX,canvas.Width-bounds.Right);
        var minimumY=-bounds.Top;var maximumY=Math.Max(minimumY,canvas.Height-bounds.Bottom);
        return new Vector(Math.Clamp(requested.X,minimumX,maximumX),Math.Clamp(requested.Y,minimumY,maximumY));
    }

    private static bool IsFinite(double value)=>double.IsFinite(value);
    private static bool FinitePoint(Point point)=>IsFinite(point.X)&&IsFinite(point.Y);
    private static bool ValidCanvas(Size canvas)=>!canvas.IsEmpty&&IsFinite(canvas.Width)&&IsFinite(canvas.Height);
    private static bool InsideCanvas(Point point,Size canvas)=>FinitePoint(point)&&point.X>=0&&point.Y>=0&&point.X<=canvas.Width&&point.Y<=canvas.Height;

    internal static Rect ResizeSquareCorner(Rect original,int corner,Point pointer,Size canvas)
    {
        if(original.IsEmpty||corner is <0 or >3||!FinitePoint(pointer)||!ValidCanvas(canvas)||
            !InsideCanvas(original.TopLeft,canvas)||!InsideCanvas(original.BottomRight,canvas))return original;
        var left=corner is 0 or 3;var top=corner is 0 or 1;
        var anchor=new Point(left?original.Right:original.Left,top?original.Bottom:original.Top);
        var directionX=left?-1d:1d;var directionY=top?-1d:1d;
        var desired=Math.Max((pointer.X-anchor.X)*directionX,(pointer.Y-anchor.Y)*directionY);
        var limit=Math.Min(left?anchor.X:canvas.Width-anchor.X,top?anchor.Y:canvas.Height-anchor.Y);
        var side=Math.Clamp(desired,Math.Min(2,limit),limit);
        return new Rect(anchor,new Point(anchor.X+directionX*side,anchor.Y+directionY*side));
    }

    internal static Rect ResizeCorner(Rect original,int corner,Point pointer,Size canvas)
    {
        if(original.IsEmpty||corner is <0 or >3||!double.IsFinite(pointer.X)||!double.IsFinite(pointer.Y))return original;
        var left=corner is 0 or 3;var top=corner is 0 or 1;
        var anchor=new Point(left?original.Right:original.Left,top?original.Bottom:original.Top);
        var x=Math.Clamp(pointer.X,0,Math.Max(0,canvas.Width));var y=Math.Clamp(pointer.Y,0,Math.Max(0,canvas.Height));
        x=left?Math.Min(x,Math.Max(0,anchor.X-2)):Math.Max(x,Math.Min(canvas.Width,anchor.X+2));
        y=top?Math.Min(y,Math.Max(0,anchor.Y-2)):Math.Max(y,Math.Min(canvas.Height,anchor.Y+2));
        return new Rect(anchor,new Point(x,y));
    }
}
