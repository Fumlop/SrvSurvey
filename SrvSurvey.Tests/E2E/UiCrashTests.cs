using SrvSurvey.forms;
using SrvSurvey.game;
using SrvSurvey.plotters;
using SrvSurvey.units;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace SrvSurvey.Tests.E2E;

/// <summary>
/// Real WinForms controls on the app's UI thread, for crashes that no journal event reaches. See README "Failure modes".
/// </summary>
public sealed class UiCrashTests
{
    /// <summary> #1075: sorting the boxel search list by distance while some systems have no distance yet (Tag == null) </summary>
    [Fact]
    public void Issue1075_SortBoxelSearchByDistanceWithUnknownDistances()
    {
        using var host = new ReplayHost("issue1075-boxel-sort", "unused.log");
        var (error, order) = host.OnUi(() =>
        {
            using var list = new ListView { View = View.Details };
            list.Columns.Add("system"); list.Columns.Add("distance");
            list.CreateControl();
            foreach (var (name, dist) in new[] { ("far", (double?)42.5), ("unknown", null), ("near", 3.25) })
            {
                var item = new ListViewItem(name);
                item.SubItems.Add(new ListViewItem.ListViewSubItem(item, dist?.ToString() ?? "") { Tag = dist });
                list.Items.Add(item);
            }
            try
            {
                // what FormBoxelSearch does when the distance column header is clicked
                list.ListViewItemSorter = new FormBoxelSearch.ColumnSorter { column = 1 };
                list.Sort();
                return ((Exception?)null, string.Join(",", list.Items.Cast<ListViewItem>().Select(i => i.Text)));
            }
            catch (Exception ex) { return (ex, ""); }
        });
        var artifact = host.WriteArtifact($"error={error?.GetType().Name}:{error?.Message} order={order}");

        Assert.True(error == null, $"sorting threw {error}, see {artifact}");
        Assert.Equal("unknown,near,far", order);
    }

    /// <summary> #948: journey viewer selecting a system that has no (readable) system file for this commander </summary>
    [Fact]
    public void Issue948_JourneyViewerSystemWithoutDataFile()
    {
        using var host = new ReplayHost("issue948-journey-system", "unused.log");
        var (error, shownName) = host.OnUi(() =>
        {
            Game.settings.lastFid = "F0000948";
            Game.settings.lastCommander = "E2E TESTER";
            try
            {
                using var view = new ViewJourneySystem(null!);
                view.setSystem(new CommanderJourney.SystemStats
                {
                    starRef = new StarRef(new double[] { 1, 2, 3 }, "Never Visited AB-C d1-2", 123456789),
                    count = new(),
                });
                return ((Exception?)null, view.systemData?.name);
            }
            catch (Exception ex) { return (ex, null); }
        });
        var artifact = host.WriteArtifact($"error={error?.GetType().Name}:{error?.Message} shownName={shownName}");

        Assert.True(error == null, $"setSystem threw {error}, see {artifact}");
        Assert.Equal("Never Visited AB-C d1-2", shownName);
    }

    /// <summary> #1070 #1064: FSS screen grab while the game window reports a 0 size (being created/closed) </summary>
    [Fact]
    public void Issue1070_FssScreenGrabOfZeroSizedWindow()
    {
        using var host = new ReplayHost("issue1070-fss-zero-grab", "unused.log");
        var screenGrab = typeof(PlotFSS).GetMethod("screenGrab", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var plotter = RuntimeHelpers.GetUninitializedObject(typeof(PlotFSS)); // screenGrab uses no instance state
        var results = new List<string>();
        foreach (var rect in new[] { new Rectangle(100, 100, 0, 540), new Rectangle(100, 100, 960, 0), new Rectangle(0, 0, 64, 32) })
        {
            try
            {
                using var bmp = (Bitmap?)screenGrab.Invoke(plotter, [rect]);
                results.Add($"{rect.Size}=>{(bmp == null ? "null" : bmp.Size.ToString())}");
            }
            catch (TargetInvocationException ex) { results.Add($"{rect.Size}=>{ex.InnerException!.GetType().Name}:{ex.InnerException.Message}"); }
        }
        var artifact = host.WriteArtifact(string.Join(" | ", results));

        Assert.Equal("{Width=0, Height=540}=>null", results[0]);
        Assert.Equal("{Width=960, Height=0}=>null", results[1]);
        Assert.Equal("{Width=64, Height=32}=>{Width=64, Height=32}", results[2]);
    }
}
