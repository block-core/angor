using CSharpFunctionalExtensions;

namespace AngorApp.Core;

public static class SafeMaybeExtensions
{
    public static SafeMaybe<T> AsSafeMaybe<T>(this Maybe<T> maybe) =>
        new SafeMaybe<T>(maybe);

    public static SafeMaybe<T> AsSafeMaybe<T>(this T? obj) where T : class =>
        obj.AsMaybe().AsSafeMaybe();
}