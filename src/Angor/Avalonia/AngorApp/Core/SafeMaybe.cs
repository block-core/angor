using CSharpFunctionalExtensions;

namespace AngorApp.Core;

public class SafeMaybe<T>(Maybe<T> maybe)
{
    public Maybe<T> Maybe { get; } = maybe;
    public T? Value => Maybe.GetValueOrDefault();
    public bool HasValue => Maybe.HasValue;
    public bool HasNoValue => !Maybe.HasValue;
}