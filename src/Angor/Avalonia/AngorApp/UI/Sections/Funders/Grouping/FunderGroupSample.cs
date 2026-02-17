namespace AngorApp.UI.Sections.Funders.Grouping;

using AngorApp.UI.Sections.Funders.Items;
public class FunderGroupSample : IFunderGroup
{
    public string Name { get; set; } = "Default";

    public IReadOnlyCollection<IFunderItem> Funders { get; set; } =
    [
        new FunderItemSample() { Amount = new AmountUI(10000)},
        new FunderItemSample(),
        new FunderItemSample(),
    ];
}