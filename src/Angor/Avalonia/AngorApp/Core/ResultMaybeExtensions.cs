namespace AngorApp.Core;

public static class ResultMaybeExtensions
{
    // Async: Task<Result<Maybe<Result>>> -> Task<Maybe<Result>>
    public static async Task<Maybe<Result>> ToMaybeResult(this Task<Result<Maybe<Result>>> source)
    {
        var r = await source.ConfigureAwait(false);

        if (r.IsFailure)
            return Maybe<Result>.From(Result.Failure(r.Error));

        return r.Value.Match(
            rr => Maybe<Result>.From(rr),
            ()  => Maybe<Result>.None);
    }
}