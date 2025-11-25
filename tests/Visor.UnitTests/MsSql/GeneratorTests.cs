using Visor.Abstractions.Attributes;
using Visor.Abstractions.Enums;

namespace Visor.UnitTests.MsSql;

public class GeneratorTests
{
    [Fact]
    public void Test_DependencyInjection_Works()
    {
        // Arrange: Create dependencies.
        var factory = new FakeConnectionFactory();
            
        // Act: Instantiate the generated class, which now requires the factory.
        // If this line fails to compile, the source generator has not been updated correctly.
        var repo = new MyFirstRepo(factory);

        // Assert: For now, just verify that the repository can be instantiated.
        Assert.NotNull(repo);
    }
        
    // --- 6. Float & Double Tests (IEEE 754) ---

    [Fact]
    public void VisorColumn_Double_ShouldThrow_WhenSettingPrecision()
    {
        // Double (SQL Float) is fixed precision (53 bits). 
        // Setting explicit precision in SqlMetaData is not allowed.
        var attr = new VisorColumnAttribute(1, VisorDbType.Double);

        var ex = Assert.Throws<ArgumentException>(() =>
        {
            attr.Precision = 10;
        });

        Assert.Contains("Property 'Precision' is not applicable", ex.Message);
    }

    [Fact]
    public void VisorColumn_Single_ShouldThrow_WhenSettingScale()
    {
        // Single (SQL Real) is fixed.
        var attr = new VisorColumnAttribute(1, VisorDbType.Single);

        Assert.Throws<ArgumentException>(() =>
        {
            attr.Scale = 2;
        });
    }

    [Fact]
    public void VisorColumn_Double_ShouldThrow_WhenSettingSize()
    {
        // Double is fixed 8 bytes.
        var attr = new VisorColumnAttribute(1, VisorDbType.Double);

        Assert.Throws<ArgumentException>(() =>
        {
            attr.Size = 8;
        });
    }

    [Fact]
    public void VisorColumn_Double_Default_IsValid()
    {
        // Correct usage: just Type without extra params
        var attr = new VisorColumnAttribute(1, VisorDbType.Double);
            
        // Should pass
        Assert.Equal(VisorDbType.Double, attr.Type);
        Assert.Equal(0, attr.Precision);
    }
}