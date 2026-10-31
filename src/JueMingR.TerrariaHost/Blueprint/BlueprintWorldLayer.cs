using System;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed class BlueprintWorldLayer
    {
        private readonly HostBlueprint host;
        private readonly World.WorldTileObservation geometry;
        internal BlueprintWorldLayer(HostBlueprint host, World.WorldTileObservation geometry) { this.host = host; this.geometry = geometry; }
        internal void Draw()
        {
            if (!host.HasSession || !Rendering.WorldPresentation.CanDraw || Main.spriteBatch == null) return;
            Platform.WorldTargets.WorldTargetView view;
            if (!geometry.TryView(0, out view)) return;
            int right = view.X + view.Width, bottom = view.Y + view.Height;
            if (host.ProjectionVisible)
            {
                host.UpdateProjectionWindow(view);
                foreach (var fixedProjection in host.ProjectionScene)
                    if (!ReferenceEquals(fixedProjection, host.ProjectionMovingSource) && !DrawProjection(fixedProjection)) return;
                var projection = host.ProjectionCandidate;
                if (host.ProjectionCandidateVisible && projection?.Positioned == true && !DrawProjection(projection)) return;
                foreach (var fixedProjection in host.ProjectionScene)
                    if (!ReferenceEquals(fixedProjection,host.ProjectionMovingSource))
                        Outline(fixedProjection.OriginX,fixedProjection.OriginY,fixedProjection.Summary.Width,fixedProjection.Summary.Height,view,
                            ReferenceEquals(fixedProjection,host.ProjectionEditTarget) ? Color.FromNonPremultiplied(255,210,75,220) : Color.FromNonPremultiplied(90,215,240,180));
                if (host.ProjectionEditing && host.ProjectionCandidateVisible && projection?.Summary != null)
                {
                    int x = projection.OriginX + (projection.Summary.Width - 1) / 2, y = projection.OriginY + (projection.Summary.Height - 1) / 2;
                    Cross(x, y);
                    Outline(projection.OriginX, projection.OriginY, projection.Summary.Width, projection.Summary.Height, view, Color.FromNonPremultiplied(255,210,75,220));
                }
                var trim = host.ProjectionEditTarget;
                if (host.TrimPointerVisible && trim != null)
                {
                    DrawTrimGeometry(host.TrimManualGeometry,trim.OriginX,trim.OriginY,view,
                        Color.FromNonPremultiplied(255,55,65,65),Color.FromNonPremultiplied(255,65,75,220));
                    DrawTrimGeometry(host.TrimObjectGeometry,trim.OriginX,trim.OriginY,view,
                        Color.FromNonPremultiplied(255,55,65,90),Color.FromNonPremultiplied(255,75,85,230));
                    var bounds = host.TrimDisplayBounds;
                    if (host.TrimDragging && bounds.Width > 0 && bounds.Height > 0)
                    {
                        FillClipped(trim.OriginX+bounds.Left,trim.OriginY+bounds.Top,bounds.Width,bounds.Height,view,Color.FromNonPremultiplied(255,55,65,65));
                        Outline(trim.OriginX+bounds.Left,trim.OriginY+bounds.Top,bounds.Width,bounds.Height,view,Color.FromNonPremultiplied(255,65,75,220));
                    }
                    if (!host.TrimDragging && host.CanRetainEdit && host.Pointer.HasPoint)
                        FillClipped(host.Pointer.CellX,host.Pointer.CellY,1,1,view,Color.FromNonPremultiplied(255,55,65,150));
                }
            }
            if (!host.State.Active) return;
            // Enumerate only visible rows/spans. A solid world-sized selection
            // takes one fill per visible row, never one object/draw per tile.
            foreach (var span in host.State.Selection.EnumerateVisible(view.X, view.Y, right, bottom))
                Fill(span.Left, span.Y, span.RightExclusive, span.Y + 1, Color.FromNonPremultiplied(35, 125, 255, 70));
            var preview = host.State.Preview;
            if (host.State.Dragging && !preview.IsEmpty)
            {
                int l = Math.Max(view.X, preview.Left), t = Math.Max(view.Y, preview.Top), r = Math.Min(right, preview.RightExclusive), b = Math.Min(bottom, preview.BottomExclusive);
                if (l < r && t < b) Fill(l, t, r, b, Color.FromNonPremultiplied(65, 155, 255, 95));
            }
            // A legal empty outer Update revokes new action permission, while
            // Draw may continue. Present the retained point without replaying
            // input; actual focus/UI/session rejection still hides stale points.
            if (host.CanRetainEdit && host.Pointer.HasPoint)
            {
                int x = host.Pointer.CellX, y = host.Pointer.CellY;
                if (x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY) {
                    Fill(x, y, x + 1, y + 1, Color.FromNonPremultiplied(80, 180, 255, 150));
                }
            }
        }
        private bool DrawProjection(BlueprintProjectionPlacement projection)
        {
            projection.Renderer.Draw(projection.OriginX, projection.OriginY,projection.Summary.Width,projection.MirroredHorizontally);
            string error; if (projection.Renderer.TryTakeError(out error)) { host.ProjectionDrawFailed(projection, error); return false; } return true;
        }
        private static void DrawTrimGeometry(BlueprintTrimGeometry selected,int originX,int originY,Platform.WorldTargets.WorldTargetView view,Color fill,Color edgeInk)
        {
            if (selected == null || !selected.HasContent) return;
            int left=view.X-originX,top=view.Y-originY,right=left+view.Width,bottom=top+view.Height;
            foreach (BlueprintSpan span in selected.EnumerateVisible(left,top,right,bottom))
                Fill(originX+span.Left,originY+span.Y,originX+span.RightExclusive,originY+span.Y+1,fill);
            // Geometry was transformed to this fixed instance pose at admission.
            // Query its numeric visible edges; Draw never walks old selections,
            // object IDs, or complete source footprints to discover visibility.
            foreach (BlueprintTrimEdge edge in selected.EnumerateVisibleEdges(left,top,right,bottom))
            {
                if (edge.Side == BlueprintTrimEdgeSide.Top || edge.Side == BlueprintTrimEdgeSide.Bottom)
                {
                    float y=(originY+edge.Fixed)*16f;
                    if (edge.Side == BlueprintTrimEdgeSide.Bottom) y--;
                    FillPixels((originX+edge.Start)*16f,y,(originX+edge.EndExclusive)*16f,y+1,edgeInk);
                }
                else
                {
                    float x=(originX+edge.Fixed)*16f;
                    if (edge.Side == BlueprintTrimEdgeSide.Right) x--;
                    FillPixels(x,(originY+edge.Start)*16f,x+1,(originY+edge.EndExclusive)*16f,edgeInk);
                }
            }
        }
        private static void Cross(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return;
            var center = Main.ReverseGravitySupport(new Vector2(x * 16f + 8, y * 16f + 8) - Main.screenPosition);
            var pixel = TextureAssets.MagicPixel.Value; var ink = Color.FromNonPremultiplied(120, 220, 255, 210);
            Main.spriteBatch.Draw(pixel, new Rectangle((int)center.X - 6, (int)center.Y - 1, 13, 2), ink);
            Main.spriteBatch.Draw(pixel, new Rectangle((int)center.X - 1, (int)center.Y - 6, 2, 13), ink);
        }
        private static void Outline(int x, int y, int width, int height, Platform.WorldTargets.WorldTargetView view, Color color)
        {
            // Clip the four real edges before pixel conversion. Very large
            // offscreen bounds never become overflowing destination rectangles.
            int right = x + width, bottom = y + height, vr = view.X + view.Width, vb = view.Y + view.Height;
            int l = Math.Max(x, view.X), r = Math.Min(right, vr), t = Math.Max(y, view.Y), b = Math.Min(bottom, vb);
            if (l < r) { if (y >= view.Y && y < vb) FillPixels(l * 16f, y * 16f, r * 16f, y * 16f + 1, color); if (bottom > view.Y && bottom <= vb) FillPixels(l * 16f, bottom * 16f - 1, r * 16f, bottom * 16f, color); }
            if (t < b) { if (x >= view.X && x < vr) FillPixels(x * 16f, t * 16f, x * 16f + 1, b * 16f, color); if (right > view.X && right <= vr) FillPixels(right * 16f - 1, t * 16f, right * 16f, b * 16f, color); }
        }
        private static void Fill(int left, int top, int right, int bottom, Color color)
        { FillPixels(left * 16f, top * 16f, right * 16f, bottom * 16f, color); }
        private static void FillClipped(int left,int top,int width,int height,Platform.WorldTargets.WorldTargetView view,Color color)
        {
            int l=Math.Max(left,view.X),t=Math.Max(top,view.Y),r=Math.Min(left+width,view.X+view.Width),b=Math.Min(top+height,view.Y+view.Height);
            if(l<r && t<b)Fill(l,t,r,b,color);
        }
        private static void FillPixels(float left, float top, float right, float bottom, Color color)
        {
            Vector2 a = Main.ReverseGravitySupport(new Vector2(left, top) - Main.screenPosition);
            Vector2 b = Main.ReverseGravitySupport(new Vector2(right, bottom) - Main.screenPosition);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)Math.Min(a.X, b.X), (int)Math.Min(a.Y, b.Y),
                (int)Math.Abs(b.X - a.X), (int)Math.Abs(b.Y - a.Y)), color);
        }
    }
}
