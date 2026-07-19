using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// Layout-regression assertions for headless UI tests.
///
/// Walks a realized visual tree after a layout pass and reports:
///  1. children whose bounds escape their parent's bounds (clipped/overflowing content),
///  2. overlapping siblings inside panels that should never overlap
///     (StackPanel / DockPanel / non-shared-cell Grid children),
///
/// These are the failure modes behind "UI elements breaking randomly on mobile":
/// a button overlaying a title, badges spilling over card edges, etc.
/// Bounds checks are pixel-free and platform-stable (unlike screenshot diffing).
/// </summary>
public static class LayoutAsserts
{
    /// <summary>Sub-pixel/rounding tolerance in px.</summary>
    private const double Tolerance = 1.5;

    /// <summary>
    /// Attached-style opt-out: subtrees whose root control has Tag="LayoutTest.Ignore"
    /// (or a name ending in "_LayoutIgnore") are skipped — for intentional overlaps
    /// such as modal backdrops, floating badges, ZIndex overlays.
    /// </summary>
    private static bool IsIgnored(Control c) =>
        (c.Tag as string) == "LayoutTest.Ignore" ||
        (c.Name?.EndsWith("_LayoutIgnore") ?? false) ||
        // Popups are windowless overlays: their in-tree bounds are meaningless
        // (they render in a separate top-level), so auditing them is pure noise.
        c is Popup;

    public static List<string> FindViolations(Visual root)
    {
        var issues = new List<string>();
        Walk(root, root, issues);
        return issues;
    }

    private static void Walk(Visual root, Visual parent, List<string> issues)
    {
        var children = parent.GetVisualChildren()
            .OfType<Control>()
            .Where(c => c.IsVisible && c.Bounds.Width > 0.5 && c.Bounds.Height > 0.5 && !IsIgnored(c))
            .ToList();

        // ── 1. Children escaping the parent's bounds ──
        // Panels are the elements that lay out children; presenters/decorators often
        // intentionally draw outside (shadows, negative margins for avatars), so we
        // only flag overflow within panel containers.
        if (parent is Panel parentPanel && parentPanel.ClipToBounds != true)
        {
            var parentRect = ToRootRect(parentPanel, root);
            foreach (var child in children)
            {
                // ScrollContentPresenter clips and pans its content by design (e.g. a
                // swipeable tab strip wider than its viewport) — overflow is the feature.
                // Its own descendants are still walked and audited normally.
                if (child is ScrollContentPresenter)
                    continue;

                // Negative margins signal intentional overlap (e.g. overlapping avatar)
                if (child.Margin.Left < 0 || child.Margin.Top < 0 ||
                    child.Margin.Right < 0 || child.Margin.Bottom < 0)
                    continue;

                var childRect = ToRootRect(child, root);

                // ItemsControl wraps each item in an invisible ContentPresenter that
                // panels like UniformGrid may arrange with 1-2px rounding overflow.
                // The user-visible element is the presenter's content (often inset by
                // margins) — audit that instead of the chrome-less wrapper.
                if (child is ContentPresenter presenter &&
                    presenter.GetVisualChildren().OfType<Control>().FirstOrDefault(c => c.IsVisible) is { } inner)
                {
                    childRect = ToRootRect(inner, root);
                }

                if (childRect.Right > parentRect.Right + Tolerance ||
                    childRect.Bottom > parentRect.Bottom + Tolerance ||
                    childRect.X < parentRect.X - Tolerance ||
                    childRect.Y < parentRect.Y - Tolerance)
                {
                    issues.Add(
                        $"OVERFLOW: {Describe(child)} escapes {Describe(parentPanel)} — child={Round(childRect)}, parent={Round(parentRect)}");
                }
            }
        }

        // ── 2. Overlapping siblings in linear panels / grids ──
        if (parent is StackPanel or DockPanel or WrapPanel or Grid)
        {
            for (int i = 0; i < children.Count; i++)
            {
                for (int j = i + 1; j < children.Count; j++)
                {
                    var a = children[i];
                    var b = children[j];

                    // Grid children sharing a cell (or spanning into each other) overlap by design.
                    if (parent is Grid && CellsIntersect(a, b))
                        continue;

                    // Explicit ZIndex means intentional layering.
                    if (a.ZIndex != 0 || b.ZIndex != 0)
                        continue;

                    var ra = ToRootRect(a, root);
                    var rb = ToRootRect(b, root);
                    var overlap = ra.Intersect(rb);
                    if (overlap.Width > Tolerance && overlap.Height > Tolerance)
                    {
                        issues.Add(
                            $"OVERLAP: {Describe(a)} ∩ {Describe(b)} = {Round(overlap)} inside {Describe((Control)parent)}");
                    }
                }
            }
        }

        foreach (var child in children)
            Walk(root, child, issues);
    }

    /// <summary>True when two Grid children occupy intersecting cell ranges.</summary>
    private static bool CellsIntersect(Control a, Control b)
    {
        int aCol = Grid.GetColumn(a), aColEnd = aCol + Grid.GetColumnSpan(a) - 1;
        int bCol = Grid.GetColumn(b), bColEnd = bCol + Grid.GetColumnSpan(b) - 1;
        int aRow = Grid.GetRow(a), aRowEnd = aRow + Grid.GetRowSpan(a) - 1;
        int bRow = Grid.GetRow(b), bRowEnd = bRow + Grid.GetRowSpan(b) - 1;
        return aCol <= bColEnd && bCol <= aColEnd && aRow <= bRowEnd && bRow <= aRowEnd;
    }

    /// <summary>Translate a control's bounds into root coordinates (handles render transforms).</summary>
    private static Rect ToRootRect(Visual v, Visual root)
    {
        var origin = v.TranslatePoint(default, root) ?? default;
        return new Rect(origin, v.Bounds.Size);
    }

    private static string Describe(Control c)
    {
        var name = !string.IsNullOrEmpty(c.Name) ? $"#{c.Name}" : "";
        var classes = c.Classes.Count > 0 ? $".{string.Join(".", c.Classes.Where(cl => !cl.StartsWith(":")))}" : "";
        var text = c switch
        {
            TextBlock tb => $" \"{Truncate(tb.Text)}\"",
            Button { Content: string s } => $" \"{Truncate(s)}\"",
            _ => ""
        };
        return $"{c.GetType().Name}{name}{classes}{text}";
    }

    private static string Truncate(string? s) =>
        s is null ? "" : s.Length > 30 ? s[..30] + "…" : s;

    private static string Round(Rect r) =>
        $"({r.X:F0},{r.Y:F0} {r.Width:F0}x{r.Height:F0})";
}
