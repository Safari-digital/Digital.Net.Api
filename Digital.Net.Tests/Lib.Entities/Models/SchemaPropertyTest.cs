using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Exceptions;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Templating.Attributes;
using Digital.Net.Tests.Core;

namespace Digital.Net.Tests.Lib.Entities.Models;

public class SchemaPropertyTest : UnitTest
{
    private enum TestEnum
    {
        Alpha,
        Beta,
        Gamma
    }

    private class TestEntity : Entity
    {
        [Column("required_property"), TemplateTarget, Required, ReadOnly]
        public string RequiredProperty { get; set; }

        [TemplateSource]
        public string SourceProperty { get; set; }

        [RegexValidation(@"^[a-z]+$")]
        public string RegexProperty { get; set; }

        [Required]
        [RegexValidation(@"^[a-z]{6,}$")]
        public string ValidatedProperty { get; set; }

        [OneOf("alpha", "beta", "gamma")]
        public string? OneOfProperty { get; set; }

        [ForeignKey("Foreign")]
        public Guid ForeignId { get; set; }

        public TestEnum EnumProperty { get; set; }

        public TestEnum? NullableEnumProperty { get; set; }
    }

    private static SchemaProperty<TestEntity> SchemaFor(string propertyName)
    {
        var info = typeof(TestEntity).GetProperty(propertyName)
                   ?? throw new InvalidOperationException($"Missing test property '{propertyName}'.");
        return new SchemaProperty<TestEntity>(info);
    }

    [Test]
    public async Task SchemaProperty_SetsPropertiesCorrectly()
    {
        var propertyInfo = typeof(TestEntity).GetProperty("RequiredProperty");
        var schemaProperty = new SchemaProperty<TestEntity>(propertyInfo!);
        await Assert.That(schemaProperty.Name).IsEqualTo("RequiredProperty");
        await Assert.That(schemaProperty.Path).IsEqualTo("required_property");
        await Assert.That(schemaProperty.Type).IsEqualTo(propertyInfo!.PropertyType.Name);
        await Assert.That(schemaProperty.IsTemplateTarget).IsTrue();
        await Assert.That(schemaProperty.IsTemplateSource).IsFalse();
        await Assert.That(schemaProperty.RegexValidation).IsNull();
        await Assert.That(schemaProperty.IsRequired).IsTrue();
        await Assert.That(schemaProperty.IsReadOnly).IsTrue();
        await Assert.That(schemaProperty.IsSecret).IsFalse();
        await Assert.That(schemaProperty.IsUnique).IsFalse();
        await Assert.That(schemaProperty.IsIdentity).IsFalse();
        await Assert.That(schemaProperty.IsForeignKey).IsFalse();
        await Assert.That(schemaProperty.MaxLength).IsNull();
        await Assert.That(schemaProperty.EnumValues).IsNull();
    }

    [Test]
    public async Task SchemaProperty_TemplateRoles_AreFalse_ForUnflaggedProperty()
    {
        var schema = SchemaFor("RegexProperty");
        await Assert.That(schema.IsTemplateTarget).IsFalse();
        await Assert.That(schema.IsTemplateSource).IsFalse();
    }

    [Test]
    public async Task SchemaProperty_IsTemplateSource_IsTrue_ForSourceProperty()
    {
        var schema = SchemaFor("SourceProperty");
        await Assert.That(schema.IsTemplateSource).IsTrue();
        await Assert.That(schema.IsTemplateTarget).IsFalse();
    }

    [Test]
    public async Task SchemaProperty_ExposesRegexPatternAsString()
    {
        var propertyInfo = typeof(TestEntity).GetProperty("RegexProperty");
        var schemaProperty = new SchemaProperty<TestEntity>(propertyInfo!);
        await Assert.That(schemaProperty.RegexValidation).IsEqualTo(@"^[a-z]+$");
    }

    [Test]
    public async Task SchemaProperty_DetectsEnumType()
    {
        var propertyInfo = typeof(TestEntity).GetProperty("EnumProperty");
        var schemaProperty = new SchemaProperty<TestEntity>(propertyInfo!);
        await Assert.That(schemaProperty.Type).IsEqualTo("Enum");
        await Assert.That(schemaProperty.EnumValues).IsEquivalentTo(["Alpha", "Beta", "Gamma"]);
    }

    [Test]
    public async Task SchemaProperty_DetectsNullableEnumType()
    {
        var propertyInfo = typeof(TestEntity).GetProperty("NullableEnumProperty");
        var schemaProperty = new SchemaProperty<TestEntity>(propertyInfo!);
        await Assert.That(schemaProperty.Type).IsEqualTo("Enum");
        await Assert.That(schemaProperty.EnumValues).IsEquivalentTo(["Alpha", "Beta", "Gamma"]);
    }

    [Test]
    public async Task Validate_DoesNotThrow_WhenValueIsNull()
    {
        var schema = SchemaFor("ValidatedProperty");
        await Assert.That(() => schema.ValidatePath(null, "ValidatedProperty")).ThrowsNothing();
    }

    [Test]
    public async Task Validate_DoesNotThrow_WhenRequiredStringHasValidValue()
    {
        var schema = SchemaFor("ValidatedProperty");
        await Assert.That(() => schema.ValidatePath("validname", "ValidatedProperty")).ThrowsNothing();
    }

    [Test]
    public async Task Validate_Throws_WhenRequiredStringIsEmpty()
    {
        var schema = SchemaFor("ValidatedProperty");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            schema.ValidatePath("", "ValidatedProperty");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Validate_Throws_WhenRegexFails()
    {
        var schema = SchemaFor("ValidatedProperty");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            schema.ValidatePath("ab", "ValidatedProperty");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Validate_DoesNotThrow_WhenOneOfValueIsKnown()
    {
        var schema = SchemaFor("OneOfProperty");
        await Assert.That(() => schema.ValidatePath("alpha", "OneOfProperty")).ThrowsNothing();
    }

    [Test]
    public async Task Validate_Throws_WhenOneOfValueIsUnknown()
    {
        var schema = SchemaFor("OneOfProperty");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            schema.ValidatePath("delta", "OneOfProperty");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Validate_DoesNotThrow_WhenIdentityHasEmptyGuid()
    {
        var schema = SchemaFor("Id");
        await Assert.That(() => schema.ValidatePath(Guid.Empty, "Id")).ThrowsNothing();
    }

    [Test]
    public async Task Validate_DoesNotThrow_WhenForeignKeyIsEmptyGuid()
    {
        var schema = SchemaFor("ForeignId");
        await Assert.That(() => schema.ValidatePath(Guid.Empty, "ForeignId")).ThrowsNothing();
    }

    [Test]
    public async Task Validate_DoesNotThrow_WhenCreatedAtIsMinValue()
    {
        var schema = SchemaFor("CreatedAt");
        await Assert.That(() => schema.ValidatePath(DateTime.MinValue, "CreatedAt")).ThrowsNothing();
    }

    [Test]
    public async Task Validate_DoesNotThrow_WhenUpdatedAtIsMinValue()
    {
        var schema = SchemaFor("UpdatedAt");
        await Assert.That(() => schema.ValidatePath(DateTime.MinValue, "UpdatedAt")).ThrowsNothing();
    }

    [Test]
    public async Task Validate_Throws_WhenIdentityHasNonDefaultValue()
    {
        var schema = SchemaFor("Id");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            schema.ValidatePath(Guid.NewGuid(), "Id");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidateMutation_DoesNotThrow_WhenRegularPropertyHasValidValue()
    {
        var schema = SchemaFor("ValidatedProperty");
        await Assert.That(() => schema.ValidatePathMutation("validname", "ValidatedProperty")).ThrowsNothing();
    }

    [Test]
    public async Task ValidateMutation_Throws_WhenSchemaIsReadOnly()
    {
        var schema = SchemaFor("RequiredProperty");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            schema.ValidatePathMutation("anything", "RequiredProperty");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidateMutation_Throws_WhenSchemaIsIdentity()
    {
        var schema = SchemaFor("Id");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            schema.ValidatePathMutation(Guid.Empty, "Id");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidateMutation_PropagatesValidateError_WhenValueIsInvalid()
    {
        var schema = SchemaFor("ValidatedProperty");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            schema.ValidatePathMutation(string.Empty, "ValidatedProperty");
            await Task.CompletedTask;
        });
    }
}
