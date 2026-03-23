using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Extensions;

namespace AngorApp.UI.Shared;

public static class ReactiveValidationExtensions
{
    public static IObservable<bool> WhenValid<TViewModel>(
        this TViewModel viewModel,
        params Expression<Func<TViewModel, object?>>[] properties)
        where TViewModel : INotifyDataErrorInfo
    {
        var propertyNames = properties
            .Select(expr => expr.Body switch
            {
                MemberExpression member => member.Member.Name,
                UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
                _ => throw new ArgumentException("Expression must be a property access", nameof(properties))
            })
            .ToHashSet();

        return Observable.Defer(() =>
        {
            return Observable
                .FromEventPattern<DataErrorsChangedEventArgs>(
                    h => viewModel.ErrorsChanged += h,
                    h => viewModel.ErrorsChanged -= h)
                .Where(args => args.EventArgs.PropertyName == null || propertyNames.Contains(args.EventArgs.PropertyName))
                .Select(_ => propertyNames.All(p => !viewModel.GetErrors(p).Cast<object>().Any()))
                .StartWith(propertyNames.All(p => !viewModel.GetErrors(p).Cast<object>().Any()))
                .DistinctUntilChanged();
        });
    }

    private static string GetPropertyName<T>(Expression<Func<T, object?>> expression)
    {
        return expression.Body switch
        {
            MemberExpression member => member.Member.Name,
            UnaryExpression { Operand: MemberExpression member } => member.Member.Name,
            _ => throw new ArgumentException("Expression must be a property access")
        };
    }
}
