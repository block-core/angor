using System.Reactive.Disposables;
using System.Reactive.Linq;
using AngorApp.UI.Shared;
using FluentAssertions;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Tests.Shared
{
    public class ReactiveValidationExtensionsTests
    {
        [Fact]
        public async Task WhenValid_should_return_true_when_observed_properties_are_valid()
        {
            using TestViewModel vm = new();

            // Initial state: both invalid
            IObservable<bool> isValidFlow = vm.WhenValid(x => x.Name);
            bool isValid = await isValidFlow.FirstAsync();
            isValid.Should().BeFalse();

            // Make Name valid, Description still invalid
            vm.Name = "Valid Name";

            isValid = await isValidFlow.FirstAsync();
            isValid.Should().BeTrue();
        }

        [Fact]
        public async Task WhenValid_should_return_false_when_observed_property_is_invalid()
        {
            using TestViewModel vm = new();
            vm.Name = "Valid Name";

            IObservable<bool> isValidFlow = vm.WhenValid(x => x.Name);
            bool isValid = await isValidFlow.FirstAsync();
            isValid.Should().BeTrue();

            vm.Name = ""; // Make invalid

            isValid = await isValidFlow.FirstAsync();
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task WhenValid_should_ignore_unobserved_properties()
        {
            using TestViewModel vm = new();
            vm.Name = "Valid Name";
            vm.Description = ""; // Invalid

            // Only observe Name
            bool isNameValid = await vm.WhenValid(x => x.Name).FirstAsync();
            isNameValid.Should().BeTrue();

            // Observe both
            bool isBothValid = await vm.WhenValid(x => x.Name, x => x.Description).FirstAsync();
            isBothValid.Should().BeFalse();
        }

        [Fact]
        public async Task WhenValid_should_start_with_current_state()
        {
            using TestViewModel vm = new();
            vm.Name = "Valid Name";

            bool result = await vm.WhenValid(x => x.Name).Take(1);
            result.Should().BeTrue();
        }

        [Fact]
        public void Should_raise_ErrorsChanged_when_property_changes()
        {
            using TestViewModel vm = new();
            bool errorsChanged = false;
            vm.ErrorsChanged += (s, e) => errorsChanged = true;

            vm.Name = "New Name";

            errorsChanged.Should().BeTrue("ErrorsChanged should fire when property changes affect validation");

            IEnumerable<object> errors = vm.GetErrors("Name").Cast<object>();
            errors.Should().BeEmpty("Validation errors should be cleared when property is valid");
        }

        public class TestViewModel : ReactiveValidationObject, IDisposable
        {
            private readonly CompositeDisposable disposables = new();

            private string description = "";
            private string name = "";

            public TestViewModel()
            {
                disposables.Add(this.ValidationRule(x => x.Name, x => !string.IsNullOrEmpty(x), "Name is required"));
                disposables.Add(
                    this.ValidationRule(x => x.Description, x => !string.IsNullOrEmpty(x), "Description is required"));
            }

            public string Name
            {
                get => name;
                set => this.RaiseAndSetIfChanged(ref name, value);
            }

            public string Description
            {
                get => description;
                set => this.RaiseAndSetIfChanged(ref description, value);
            }

            public new void Dispose()
            {
                disposables.Dispose();
            }
        }
    }
}