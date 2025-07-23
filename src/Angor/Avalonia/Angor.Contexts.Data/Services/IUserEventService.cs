using Angor.Contexts.Data.Entities;

namespace Angor.Contexts.Data.Services;

public interface IUserEventService
{
    public Task<List<NostrUser>> PullAndSaveUserEventsAsync(params string[] pubkeys);
}