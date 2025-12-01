using Visor.Core;

namespace Visor.UnitTests.Core;

public class VisorConvertTests
{
    [Fact]
    public void Unbox_ShouldReturnDefault_WhenValueIsNull()
    {
        // Act
        var result = VisorConvert.Unbox<int>(null!);
        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Unbox_ShouldReturnDefault_WhenValueIsDBNull()
    {
        // Act
        var result = VisorConvert.Unbox<int>(DBNull.Value);
        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Unbox_ShouldReturnSameValue_WhenTypesMatch()
    {
        // Arrange
        int expected = 42;
        // Act
        var result = VisorConvert.Unbox<int>(expected);
        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Unbox_ShouldConvert_WhenTypesDifferButCompatible()
    {
        // Arrange: long to int
        long expected = 42;
        // Act
        var result = VisorConvert.Unbox<int>(expected);
        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void Unbox_ShouldHandleNullable_WhenValueIsNotNull()
    {
        // Arrange
        object val = 42;
        // Act
        var result = VisorConvert.Unbox<int?>(val);
        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void Unbox_ShouldHandleChar_WhenStringOfLengthOnePassed()
    {
        // SQL drivers sometimes return string for CHAR(1)
        object val = "A";
        var result = VisorConvert.Unbox<char>(val);
        Assert.Equal('A', result);
    }

    [Fact]
    public void Unbox_ShouldThrow_WhenStringPassedForCharIsTooLong()
    {
        object val = "ABC";
        Assert.Throws<ArgumentException>(() => VisorConvert.Unbox<char>(val));
    }

    [Fact]
    public void Unbox_ShouldThrow_WhenInvalidCast()
    {
        object val = "NotANumber";
        Assert.Throws<InvalidCastException>(() => VisorConvert.Unbox<int>(val));
    }
}
