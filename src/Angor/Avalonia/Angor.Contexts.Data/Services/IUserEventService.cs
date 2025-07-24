using Angor.Contexts.Data.Entities;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Data.Services;

public interface IUserEventService
{
    public Task<Result> PullAndSaveUserEventsAsync(params string[] pubkeys);
}