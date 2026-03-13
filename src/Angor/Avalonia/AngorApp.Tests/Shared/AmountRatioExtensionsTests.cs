using AngorApp.Model.Amounts;
using AngorApp.Model.Contracts.Amounts;
using AngorApp.UI.Shared;
using FluentAssertions;

namespace AngorApp.Tests.Shared
{
    public class AmountRatioExtensionsTests
    {
        [Fact]
        public void RatioOrZero_should_return_zero_when_denominator_is_null()
        {
            IAmountUI? raised = new AmountUI(50_000_000);
            IAmountUI? target = null;

            raised.RatioOrZero(target).Should().Be(0m);
        }

        [Fact]
        public void RatioOrZero_should_return_zero_when_denominator_is_zero()
        {
            IAmountUI? raised = new AmountUI(50_000_000);
            IAmountUI? target = new AmountUI(0);

            raised.RatioOrZero(target).Should().Be(0m);
        }

        [Fact]
        public void RatioOrZero_should_return_expected_ratio_when_denominator_is_valid()
        {
            IAmountUI? raised = new AmountUI(25_000_000);
            IAmountUI? target = new AmountUI(100_000_000);

            raised.RatioOrZero(target).Should().Be(0.25m);
        }

        [Fact]
        public void RatioOrZero_should_return_zero_when_numerator_is_null()
        {
            IAmountUI? raised = null;
            IAmountUI? target = new AmountUI(100_000_000);

            raised.RatioOrZero(target).Should().Be(0m);
        }
        
        [Fact]
        public void RatioOrZeroAsDouble_should_return_zero_when_denominator_is_zero()
        {
            IAmountUI? raised = new AmountUI(50_000_000);
            IAmountUI? target = new AmountUI(0);

            raised.RatioOrZeroAsDouble(target).Should().Be(0d);
        }
    }
}
