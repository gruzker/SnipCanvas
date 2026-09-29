using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SnipCanvas;

internal abstract record ImageEdit
{
    public abstract BitmapSource Apply(BitmapSource image);
}
internal sealed record TextEdit(Point Position, string Value, Color Color, double Size) : ImageEdit
{
    public override BitmapSource Apply(BitmapSource image) => Edits.Text(image, Position, Value, Color, Size);
}
internal sealed record ArrowEdit(Point Start, Point End, Color? Color = null, double Thickness = 4) : ImageEdit
{
    public override BitmapSource Apply(BitmapSource image) => Edits.Arrow(image, Start, End, Color, Thickness);
}
internal sealed record BoxEdit(Rect Area, Color Color, double Thickness) : ImageEdit
{
    public override BitmapSource Apply(BitmapSource image) => Edits.Box(image, Area, Color, Thickness);
}
internal sealed record BlurEdit(Int32Rect Area) : ImageEdit
{
    public override BitmapSource Apply(BitmapSource image) => Edits.Blur(image, Area);
}
internal sealed record CropEdit(Int32Rect Area) : ImageEdit
{
    public override BitmapSource Apply(BitmapSource image) { var result = new CroppedBitmap(image, Area); result.Freeze(); return result; }
}
internal sealed record EditorState(BitmapSource Source, ImageEdit[] Edits);

internal sealed partial class MainWindow
{
    private BitmapSource? sourceImage;
    private readonly List<ImageEdit> imageEdits = new();
    private int selectedText = -1;
    private readonly Rectangle textOutline = new() { Stroke = new SolidColorBrush(Color.FromRgb(124, 108, 255)), StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false };
    private readonly StackPanel textTools = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
    private readonly TextBlock textSizeLabel = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 50, TextAlignment = TextAlignment.Center, FontSize = 12.5 };
    private EditorState? textDragState;
    private Point? textDragStart;
    private TextEdit? draggedText;
    private EditorState? deletedState;

    private EditorState Snapshot() => new(sourceImage!, imageEdits.ToArray());
    private void Restore(EditorState state)
    {
        sourceImage = state.Source; imageEdits.Clear(); imageEdits.AddRange(state.Edits); selectedText = -1;
        RenderDocument();
    }
    private void RenderDocument()
    {
        var image = sourceImage!;
        foreach (var edit in imageEdits) image = edit.Apply(image);
        DisplayImage(image); UpdateTextSelection();
    }
    private void CommitEdit(ImageEdit edit)
    {
        undo.Push(Snapshot()); redo.Clear(); imageEdits.Add(edit);
        selectedText = edit is TextEdit ? imageEdits.Count - 1 : -1;
        RenderDocument(); dirty = true;
    }
    private void BuildTextTools()
    {
        textTools.Children.Add(Ui.Divider());
        var smaller = Ui.Styled("IconButton", new TextBlock { Text = "A−", FontSize = 14, FontWeight = FontWeights.SemiBold }, () => ResizeText(-2), "Make the text smaller", "Smaller text");
        var larger = Ui.Styled("IconButton", new TextBlock { Text = "A+", FontSize = 14, FontWeight = FontWeights.SemiBold }, () => ResizeText(2), "Make the text bigger", "Bigger text");
        textSizeLabel.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        textTools.Children.Add(smaller); textTools.Children.Add(textSizeLabel); textTools.Children.Add(larger);
        textTools.Children.Add(Ui.Divider());
        textTools.Children.Add(Ui.Styled("GhostButton", Ui.IconLabel("edit", "Edit", out _, 16), EditSelectedText, "Change the words or color  (double-click)", "Edit text"));
        textTools.Children.Add(Ui.Styled("GhostButton", Ui.IconLabel("trash", "Delete", out _, 16), DeleteSelectedText, "Delete this text  (Delete key)", "Delete text"));
    }
    private Vector CropOffset(int index)
    {
        var offset = new Vector();
        foreach (var crop in imageEdits.Skip(index + 1).OfType<CropEdit>()) { offset.X += crop.Area.X; offset.Y += crop.Area.Y; }
        return offset;
    }
    private Rect TextBounds(int index)
    {
        var note = (TextEdit)imageEdits[index];
        var layout = Edits.FormatText(note.Value, note.Color, note.Size);
        var rect = new Rect(note.Position, new Size(Math.Max(1, layout.WidthIncludingTrailingWhitespace), layout.Height));
        foreach (var crop in imageEdits.Skip(index + 1).OfType<CropEdit>())
        {
            rect.Intersect(new Rect(crop.Area.X, crop.Area.Y, crop.Area.Width, crop.Area.Height));
            if (rect.IsEmpty) return rect;
            rect.Offset(-crop.Area.X, -crop.Area.Y);
        }
        rect.Intersect(new Rect(0, 0, canvas.Width, canvas.Height)); return rect;
    }
    private void UpdateTextSelection()
    {
        if (textDragStart == null) Mouse.UpdateCursor();
        canvas.Children.Remove(textOutline);
        bool selected = bitmap != null && selectedText >= 0 && selectedText < imageEdits.Count && imageEdits[selectedText] is TextEdit;
        UpdateOptions();
        if (!selected) return;
        var note = (TextEdit)imageEdits[selectedText]; textSizeLabel.Text = note.Size + " px";
        var bounds = TextBounds(selectedText);
        if (bounds.IsEmpty) return;
        textOutline.StrokeThickness = 2 / Math.Max(Zoom, 0.05);
        textOutline.Width = bounds.Width; textOutline.Height = bounds.Height;
        Canvas.SetLeft(textOutline, bounds.X); Canvas.SetTop(textOutline, bounds.Y); canvas.Children.Add(textOutline);
    }
    private bool BeginTextSelection(Point point, int clicks)
    {
        for (int i = imageEdits.Count - 1; i >= 0; i--)
        {
            if (imageEdits[i] is not TextEdit note || !TextBounds(i).Contains(point)) continue;
            selectedText = i; UpdateTextSelection();
            if (clicks == 2) { EditSelectedText(); return true; }
            textDragState = Snapshot(); textDragStart = point; draggedText = note; canvas.CaptureMouse(); return true;
        }
        selectedText = -1; UpdateTextSelection(); return false;
    }
    private Cursor CanvasCursor(Point point)
    {
        if (tool != "Text") return Cursors.Cross;
        if (textDragStart != null) return Cursors.SizeAll;
        if (bitmap != null)
            for (int i = imageEdits.Count - 1; i >= 0; i--)
                if (imageEdits[i] is TextEdit && TextBounds(i).Contains(point)) return Cursors.SizeAll;
        return Cursors.IBeam;
    }
    private void MoveSelectedText(Point point)
    {
        if (textDragStart is not Point origin || draggedText == null) return;
        var offset = CropOffset(selectedText);
        var position = draggedText.Position - offset + (point - origin);
        position.X = Math.Clamp(position.X, 0, Math.Max(0, canvas.Width - 8));
        position.Y = Math.Clamp(position.Y, 0, Math.Max(0, canvas.Height - 8));
        imageEdits[selectedText] = draggedText with { Position = position + offset };
        RenderDocument();
    }
    private void EndTextMove()
    {
        if (textDragState != null && draggedText != null && imageEdits[selectedText] != draggedText)
        { undo.Push(textDragState); redo.Clear(); dirty = true; RefreshChrome(); }
        textDragState = null; textDragStart = null; draggedText = null;
        if (canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
    }
    private void CancelTextMove()
    {
        var previous = textDragState;
        textDragState = null; textDragStart = null; draggedText = null;
        if (previous != null) Restore(previous);
    }
    private void ResizeText(double change)
    {
        if (selectedText < 0 || imageEdits[selectedText] is not TextEdit note) return;
        double size = Math.Clamp(note.Size + change, 8, 144);
        if (size == note.Size) return;
        undo.Push(Snapshot()); redo.Clear(); imageEdits[selectedText] = note with { Size = size }; RenderDocument(); dirty = true;
    }
    private void DeleteSelectedText()
    {
        if (selectedText < 0) return;
        undo.Push(Snapshot()); redo.Clear(); imageEdits.RemoveAt(selectedText); selectedText = -1; RenderDocument(); dirty = true;
    }
    private void EditSelectedText()
    {
        if (selectedText < 0 || imageEdits[selectedText] is not TextEdit note) return;
        int index = selectedText;
        var dialog = new TextDialog(this, canvas.PointToScreen(note.Position - CropOffset(index)), note.Color, (value, color) => {
            imageEdits[index] = note with { Value = value, Color = color }; RenderDocument();
        }, note.Value);
        bool applied = false;
        try
        {
            if (dialog.ShowDialog() == true)
            {
                var edited = note with { Value = dialog.Value, Color = dialog.TextColor };
                imageEdits[index] = note;
                if (edited != note) { undo.Push(Snapshot()); redo.Clear(); imageEdits[index] = edited; annotationColor = edited.Color; dirty = true; }
                applied = true;
            }
            else imageEdits[index] = note;
        }
        finally { if (!applied) imageEdits[index] = note; RenderDocument(); }
    }
}
