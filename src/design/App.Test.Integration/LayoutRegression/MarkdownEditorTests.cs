using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.EditProfile;
using App.UI.Shared;
using App.UI.Shared.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// Behavior + responsiveness tests for the Project Description markdown editor
/// (EditProfileView, Project tab): caret-aware toolbar insertion, line-prefix
/// toggling, Write/Preview switching, the quick-reference guide, and the
/// compact-layout rearrangement of the toolbar.
/// </summary>
public class MarkdownEditorTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Toolbar insertion behavior
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaFact]
    public void Bold_button_wraps_the_selection()
    {
        var (view, box, window) = CreateEditor("make this bold please");
        try
        {
            box.SelectionStart = 5;
            box.SelectionEnd = 9; // "this"
            Click(view, "MdBoldBtn");

            box.Text.Should().Be("make **this** bold please");
            SelectedText(box).Should().Be("this", "the payload must stay selected for immediate re-editing");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Bold_button_inserts_selected_placeholder_when_nothing_selected()
    {
        var (view, box, window) = CreateEditor("");
        try
        {
            Click(view, "MdBoldBtn");

            box.Text.Should().Be("**bold text**");
            SelectedText(box).Should().Be("bold text", "placeholder must be selected so typing replaces it");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Link_button_wraps_selection_as_link_title()
    {
        var (view, box, window) = CreateEditor("visit angor now");
        try
        {
            box.SelectionStart = 6;
            box.SelectionEnd = 11; // "angor"
            Click(view, "MdLinkBtn");

            box.Text.Should().Be("visit [angor](https://) now");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void List_button_prefixes_every_selected_line_and_toggles_off()
    {
        var (view, box, window) = CreateEditor("alpha\nbeta\ngamma");
        try
        {
            box.SelectionStart = 0;
            box.SelectionEnd = box.Text!.Length;
            Click(view, "MdListBtn");
            box.Text.Should().Be("- alpha\n- beta\n- gamma");

            // Second press on the same block removes the prefixes.
            box.SelectionStart = 0;
            box.SelectionEnd = box.Text!.Length;
            Click(view, "MdListBtn");
            box.Text.Should().Be("alpha\nbeta\ngamma", "line-prefix actions must toggle off");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Heading_button_prefixes_the_caret_line_only()
    {
        var (view, box, window) = CreateEditor("first line\nsecond line");
        try
        {
            box.SelectionStart = box.SelectionEnd = 14; // inside "second line"
            Click(view, "MdHeadingBtn");

            box.Text.Should().Be("first line\n## second line");
        }
        finally { window.Close(); }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Write / Preview toggle
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaFact]
    public void Preview_renders_markdown_and_write_restores_editor()
    {
        var (view, box, window) = CreateEditor("# Title\nSome **bold** text");
        try
        {
            Click(view, "MdPreviewBtn");

            box.IsVisible.Should().BeFalse("preview mode must hide the editor");
            var scroll = view.FindControl<ScrollViewer>("MdPreviewScroll")!;
            scroll.IsVisible.Should().BeTrue();
            var block = view.FindControl<MarkdownTextBlock>("MdPreviewBlock")!;
            block.Markdown.Should().Be("# Title\nSome **bold** text");
            view.FindControl<Button>("MdPreviewBtn")!.Classes.Should().Contain("ModeActive");
            view.FindControl<Button>("MdWriteBtn")!.Classes.Should().NotContain("ModeActive");

            Click(view, "MdWriteBtn");
            box.IsVisible.Should().BeTrue("write mode must restore the editor");
            scroll.IsVisible.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Preview_shows_empty_hint_when_content_is_blank()
    {
        var (view, _, window) = CreateEditor("   ");
        try
        {
            Click(view, "MdPreviewBtn");

            view.FindControl<TextBlock>("MdPreviewEmptyText")!.IsVisible.Should().BeTrue();
            view.FindControl<MarkdownTextBlock>("MdPreviewBlock")!.IsVisible.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Toolbar_buttons_do_nothing_in_preview_mode()
    {
        var (view, box, window) = CreateEditor("content");
        try
        {
            Click(view, "MdPreviewBtn");
            Click(view, "MdBoldBtn");

            box.Text.Should().Be("content", "formatting must not mutate text while previewing");
        }
        finally { window.Close(); }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Quick-reference guide
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaFact]
    public void Guide_toggle_expands_and_collapses()
    {
        var (view, _, window) = CreateEditor("");
        try
        {
            var guide = view.FindControl<Border>("MdGuidePanel")!;
            guide.IsVisible.Should().BeFalse("guide starts collapsed");

            Click(view, "MdGuideToggleBtn");
            guide.IsVisible.Should().BeTrue();

            Click(view, "MdGuideToggleBtn");
            guide.IsVisible.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Responsive toolbar layout (both directions)
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaFact]
    public void Toolbar_rearranges_for_mobile_and_back()
    {
        var (view, _, window) = CreateEditor("", width: 1280);
        try
        {
            var modeSwitch = view.FindControl<Border>("MdModeSwitch")!;
            var toolButtons = view.FindControl<UniformGrid>("MdToolButtons")!;

            DockPanel.GetDock(modeSwitch).Should().Be(Dock.Right, "desktop docks the toggle right");
            toolButtons.HorizontalAlignment.Should().Be(Avalonia.Layout.HorizontalAlignment.Left);

            SetWidth(window, 390);
            DockPanel.GetDock(modeSwitch).Should().Be(Dock.Top, "mobile stacks the toggle full-width on top");
            modeSwitch.HorizontalAlignment.Should().Be(Avalonia.Layout.HorizontalAlignment.Stretch);
            toolButtons.HorizontalAlignment.Should().Be(Avalonia.Layout.HorizontalAlignment.Stretch,
                "mobile spreads the formatting buttons symmetrically");

            SetWidth(window, 1280);
            DockPanel.GetDock(modeSwitch).Should().Be(Dock.Right, "desktop layout must come back");
            toolButtons.HorizontalAlignment.Should().Be(Avalonia.Layout.HorizontalAlignment.Left);
        }
        finally
        {
            window.Close();
            LayoutModeService.Instance.UpdateWidth(1280);
            Dispatcher.UIThread.RunJobs();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Harness
    // ═══════════════════════════════════════════════════════════════════

    private static (EditProfileView view, TextBox box, Window window) CreateEditor(
        string content, double width = 1280)
    {
        LayoutModeService.Instance.UpdateWidth(width);

        var factory = global::App.App.Services
            .GetRequiredService<Func<MyProjectItemViewModel, EditProfileViewModel>>();
        var vm = factory(new MyProjectItemViewModel
        {
            Name = "Markdown Editor Test",
            ProjectType = "fund",
            ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
        });
        vm.SetActiveTab("project");
        vm.ProjectContent = content;

        var view = new EditProfileView { DataContext = vm };
        var window = new Window
        {
            Width = width, Height = 900,
            Content = new ScrollViewer { Content = view },
        };
        // Dispose the VM with the window so its throttled image-preview subscriptions
        // can't post to the dispatcher after the headless session is torn down.
        window.Closed += (_, _) => vm.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var box = view.FindControl<TextBox>("ProjectContentBox")!;
        return (view, box, window);
    }

    private static void Click(EditProfileView view, string buttonName)
    {
        var btn = view.FindControl<Button>(buttonName)!;
        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string SelectedText(TextBox box)
        => (box.Text ?? string.Empty).Substring(
            Math.Min(box.SelectionStart, box.SelectionEnd),
            Math.Abs(box.SelectionEnd - box.SelectionStart));

    private static void SetWidth(Window window, double width)
    {
        window.Width = width;
        LayoutModeService.Instance.UpdateWidth(width);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
