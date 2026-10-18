using Xunit;

namespace WorkshopCore.Tests;

public class CalculatorTests
{
    [Fact]
    public void Add_ReturnsTheSum()
    {
        int result = Calculator.Add(2, 3);

        Assert.Equal(5, result);
    }

    [Fact]
    public void Add_HandlesNegativeNumbers()
    {
        int result = Calculator.Add(-4, 1);

        Assert.Equal(-3, result);
    }

    [Fact]
    public void Add_ReturnsTheSameNumberWhenAddingZero()
    {
        int result = Calculator.Add(7, 0);

        Assert.Equal(7, result);
    }

    [Fact]
    public void Subtract_ReturnsTheDifference()
    {
        int result = Calculator.Subtract(10, 4);

        Assert.Equal(6, result);
    }
}
