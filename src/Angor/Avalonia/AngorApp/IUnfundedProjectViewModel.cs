using Zafiro.UI.Commands;

namespace AngorApp;

public interface IUnfundedProjectViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; }
}

public class UnfundedProjectViewModelDesign : IUnfundedProjectViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; } = new List<IUnfundedProjectTransaction>()
    {
        new UnfundedProjectTransactionDesign()
        {
            Approved = DateTime.Now,
            Released = DateTime.Now,
            Arrived = DateTime.Now,
        },
        new UnfundedProjectTransactionDesign()
        {
            Approved = DateTime.Now,
            Arrived = DateTime.Now,
        },
        new UnfundedProjectTransactionDesign()
        {
            Approved = DateTime.Now,
            Arrived = DateTime.Now,
        }
    };
}

public interface IUnfundedProjectTransaction
{
    public DateTime Arrived { get;  }
    public DateTime Approved { get;  }
    public DateTime? Released { get; }
    IEnhancedCommand Release { get; }
}

public class UnfundedProjectTransactionDesign : IUnfundedProjectTransaction
{
    public DateTime Arrived { get; set; }
    public DateTime Approved { get; set; }
    public DateTime? Released { get; set; }
    public IEnhancedCommand Release { get; } = ReactiveCommand.Create(() => { }).Enhance();
}