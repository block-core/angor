using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using App.UI.Sections.FindProjects;
using App.UI.Shared;
using App.UI.Shared.Controls;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// Guards the row-chunked virtualization of the Find Projects grid: the flat
/// <c>Projects</c> list is re-chunked into <c>ProjectRows</c> by column count, and the
/// view renders rows through a VirtualizingStackPanel so long project lists don't
/// materialize every ProjectCard. Without this, a non-virtualizing ResponsiveGrid
/// inflates every card (images, bindings, layout) at once — a mobile memory/scroll
/// risk as the indexer project count grows.
/// </summary>
public class ProjectListVirtualizationTests
{
    [AvaloniaFact]
    public void ProjectRows_chunk_into_column_sized_rows_preserving_order()
    {
        var vm = global::App.App.Services.GetRequiredService<FindProjectsViewModel>();
        // The VM's ctor defers a process-cache seed via a posted dispatcher job — drain
        // it before touching Projects, or the test data below gets wiped at the next flush.
        Dispatcher.UIThread.RunJobs();
        // Drop whatever the drained process-cache seed populated; arrange a clean slate.
        vm.Projects.Clear();
        try
        {
            for (var i = 0; i < 7; i++)
                vm.Projects.Add(new ProjectItemViewModel { ProjectId = $"proj-{i}" });

            vm.CardColumnCount = 3;
            // 7 items / 3 cols = rows [3,3,1]
            string.Join(",", vm.ProjectRows.Select(r => r.Cards.Count)).Should().Be("3,3,1",
                $"state: Projects={vm.Projects.Count} Cols={vm.CardColumnCount}");
            vm.ProjectRows.SelectMany(r => r.Cards).Select(c => c.ProjectId)
                .Should().Equal(Enumerable.Range(0, 7).Select(i => $"proj-{i}"));
            vm.ProjectRows.Should().OnlyContain(r => r.ColumnCount == 3);

            // Width-driven re-chunk: 2 columns → 4 rows.
            vm.CardColumnCount = 2;
            // 7 items / 2 cols = rows [2,2,2,1]
            string.Join(",", vm.ProjectRows.Select(r => r.Cards.Count)).Should().Be("2,2,2,1",
                $"state: Projects={vm.Projects.Count} Cols={vm.CardColumnCount}");
            vm.ProjectRows.SelectMany(r => r.Cards).Select(c => c.ProjectId)
                .Should().Equal(Enumerable.Range(0, 7).Select(i => $"proj-{i}"));
        }
        finally
        {
            vm.Projects.Clear();
            vm.CardColumnCount = 1;
        }
    }

    [AvaloniaFact]
    public void ProjectList_only_realizes_visible_rows_for_long_lists()
    {
        const int projectCount = 60;

        LayoutModeService.Instance.UpdateWidth(1024);

        var vm = global::App.App.Services.GetRequiredService<FindProjectsViewModel>();
        // Drain the ctor's deferred process-cache seed before arranging test data.
        Dispatcher.UIThread.RunJobs();
        try
        {
            // Drop whatever the drained process-cache seed populated; arrange a clean slate.
            vm.Projects.Clear();
            vm.IsInitialLoad = false;
            vm.IsLoading = false;
            vm.HasMoreItems = false;
            for (var i = 0; i < projectCount; i++)
                vm.Projects.Add(new ProjectItemViewModel
                {
                    ProjectName = $"Project {i}",
                    ShortDescription = "A moderately long project description for layout purposes.",
                    Raised = "1.00000000",
                    Target = "10.00000000",
                    ProjectId = $"angor1qtest{i:000}",
                });

            var view = new FindProjectsView(vm);
            var window = new Window
            {
                Width = 1024,
                Height = 600,
                SizeToContent = SizeToContent.Manual,
                Content = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = view,
                },
            };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                vm.Projects.Should().HaveCount(projectCount,
                    "no deferred seeding should wipe the arranged data (it was drained before arrange)");
                vm.ProjectRows.Select(r => r.Cards.Count).Sum().Should().Be(projectCount);

                var itemsControl = view.FindControl<ItemsControl>("ProjectsItemsControl");
                itemsControl.Should().NotBeNull();
                vm.CardColumnCount.Should().BeGreaterThan(0);

                var totalRows = vm.ProjectRows.Count;
                totalRows.Should().BeGreaterThan(8,
                    "60 projects must chunk into many rows regardless of column count");

                var vsp = itemsControl!.GetVisualDescendants().OfType<VirtualizingStackPanel>().FirstOrDefault();
                vsp.Should().NotBeNull("the projects grid must use a VirtualizingStackPanel so rows recycle");

                var realizedRows = vsp!.GetVisualChildren().Count();
                realizedRows.Should().BeLessThan(totalRows,
                    "only viewport rows (+overscan) should be materialized — realizing all rows means " +
                    $"virtualization regressed (realized={realizedRows}, total={totalRows})");
                realizedRows.Should().BeGreaterThan(0);

                // First realized row must lay out a full column-width set of cards.
                var firstRowCards = vsp.GetVisualChildren().First()
                    .GetVisualDescendants().OfType<ProjectCard>().Count();
                firstRowCards.Should().Be(vm.CardColumnCount);
            }
            finally
            {
                window.Close();
                LayoutModeService.Instance.UpdateWidth(1280);
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            vm.Projects.Clear();
            vm.HasMoreItems = false;
            vm.CardColumnCount = 1;
            Dispatcher.UIThread.RunJobs();
        }
    }
}
