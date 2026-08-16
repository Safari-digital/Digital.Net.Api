using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Templating.Attributes;
using Microsoft.EntityFrameworkCore;
using Digital.Net.Tests.Core;

namespace Digital.Net.Tests.Lib.Entities.Attributes;

public class AttributeAnalyzerTest : UnitTest
{
    private enum ETestFlag
    {
        TestValue
    }

    [Index(nameof(UniqueProperty), IsUnique = true)]
    private class TestEntity : Entity
    {
        [Required]
        public string RequiredProperty { get; set; }

        public string UniqueProperty { get; set; }

        [MaxLength(50), TemplateTarget]
        public string MaxLengthProperty { get; set; }

        [TemplateSource]
        public string SourceProperty { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdentityProperty { get; set; }

        [ForeignKey("ForeignKeyProperty")]
        public int ForeignKeyProperty { get; set; }

        [Column("form_property"), ReadOnly]
        public DateTime FormProperty { get; set; }

        [Secret, RegexValidation("^[a-zA-Z0-9.'@_-]{6,24}$")]
        public string SecretProperty { get; set; }
    }

    [Test]
    public async Task IsRequired_ReturnsTrue_ForRequiredProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsRequired("RequiredProperty")).IsTrue();

    [Test]
    public async Task IsRequired_ReturnsFalse_ForNonRequiredProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsRequired("UniqueProperty")).IsFalse();

    [Test]
    public async Task IsUnique_ReturnsTrue_ForUniqueProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsUnique("UniqueProperty")).IsTrue();

    [Test]
    public async Task IsUnique_ReturnsFalse_ForNonUniqueProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsUnique("RequiredProperty")).IsFalse();

    [Test]
    public async Task MaxLength_ReturnsCorrectLength_ForMaxLengthProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.MaxLength("MaxLengthProperty")).IsEqualTo(50);

    [Test]
    public async Task MaxLength_ReturnsZero_ForNonMaxLengthProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.MaxLength("RequiredProperty")).IsEqualTo(0);

    [Test]
    public async Task IsIdentity_ReturnsTrue_ForIdentityProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsIdentity("IdentityProperty")).IsTrue();

    [Test]
    public async Task IsIdentity_ReturnsFalse_ForNonIdentityProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsIdentity("RequiredProperty")).IsFalse();

    [Test]
    public async Task IsForeignKey_ReturnsTrue_ForForeignKeyProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsForeignKey("ForeignKeyProperty")).IsTrue();

    [Test]
    public async Task IsForeignKey_ReturnsFalse_ForNonForeignKeyProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsForeignKey("RequiredProperty")).IsFalse();

    [Test]
    public async Task IsSecret_ReturnsTrue_ForSecretProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsSecret("SecretProperty")).IsTrue();

    [Test]
    public async Task GetPath_ReturnsCorrectPath_ForColumnProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.GetPath("FormProperty")).IsEqualTo("form_property");

    [Test]
    public async Task GetPath_ReturnsPropertyName_ForNonColumnProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.GetPath("RequiredProperty")).IsEqualTo("RequiredProperty");

    [Test]
    public async Task IsTemplateTarget_ReturnsTrue_ForTargetProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateTarget("MaxLengthProperty")).IsTrue();

    [Test]
    public async Task IsTemplateTarget_ReturnsFalse_ForSourceProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateTarget("SourceProperty")).IsFalse();

    [Test]
    public async Task IsTemplateTarget_ReturnsFalse_ForUnflaggedProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateTarget("RequiredProperty")).IsFalse();

    [Test]
    public async Task IsTemplateSource_ReturnsTrue_ForSourceProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateSource("SourceProperty")).IsTrue();

    [Test]
    public async Task IsTemplateSource_ReturnsFalse_ForTargetProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateSource("MaxLengthProperty")).IsFalse();

    [Test]
    public async Task IsReadOnly_ReturnsFalse_ForNonReadOnlyProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsReadOnly("RequiredProperty")).IsFalse();

    [Test]
    public async Task IsReadOnly_ReturnsFalse_ForReadOnlyProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsReadOnly("FormProperty")).IsTrue();

    [Test]
    public async Task GetRegex_ReturnsNull_ForNonRegexProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.GetRegex("RequiredProperty")).IsNull();

    [Test]
    public async Task GetRegex_ReturnsNotNull_ForRegexProperty() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.GetRegex("SecretProperty")).IsNotNull();


    [Test]
    public async Task IsRequired_ReturnsTrue_ForRequiredProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsRequired(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsTrue();

    [Test]
    public async Task IsRequired_ReturnsFalse_ForNonRequiredProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsRequired(typeof(TestEntity).GetProperty("UniqueProperty")!)).IsFalse();

    [Test]
    public async Task IsUnique_ReturnsTrue_ForUniqueProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsUnique(typeof(TestEntity).GetProperty("UniqueProperty")!)).IsTrue();

    [Test]
    public async Task IsUnique_ReturnsFalse_ForNonUniqueProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsUnique(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsFalse();

    [Test]
    public async Task MaxLength_ReturnsCorrectLength_ForMaxLengthProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.MaxLength(typeof(TestEntity).GetProperty("MaxLengthProperty")!)).IsEqualTo(50);

    [Test]
    public async Task MaxLength_ReturnsNull_ForNonMaxLengthProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.MaxLength(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsNull();

    [Test]
    public async Task IsIdentity_ReturnsTrue_ForIdentityProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsIdentity(typeof(TestEntity).GetProperty("IdentityProperty")!)).IsTrue();

    [Test]
    public async Task IsIdentity_ReturnsFalse_ForNonIdentityProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsIdentity(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsFalse();

    [Test]
    public async Task IsForeignKey_ReturnsTrue_ForForeignKeyProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsForeignKey(typeof(TestEntity).GetProperty("ForeignKeyProperty")!)).IsTrue();

    [Test]
    public async Task IsForeignKey_ReturnsFalse_ForNonForeignKeyProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsForeignKey(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsFalse();

    [Test]
    public async Task IsSecret_ReturnsTrue_ForSecretProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsSecret(typeof(TestEntity).GetProperty("SecretProperty")!)).IsTrue();

    [Test]
    public async Task IsSecret_ReturnsFalse_ForNonSecretProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsSecret(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsFalse();

    [Test]
    public async Task IsReadOnly_ReturnsTrue_ForReadOnlyProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsReadOnly(typeof(TestEntity).GetProperty("FormProperty")!)).IsTrue();

    [Test]
    public async Task IsReadOnly_ReturnsFalse_ForNonReadOnlyProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsReadOnly(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsFalse();

    [Test]
    public async Task GetPath_ReturnsColumnName_ForColumnProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.GetPath(typeof(TestEntity).GetProperty("FormProperty")!)).IsEqualTo("form_property");

    [Test]
    public async Task GetPath_ReturnsPropertyName_ForNonColumnProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.GetPath(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsEqualTo("RequiredProperty");

    [Test]
    public async Task IsTemplateTarget_ReturnsTrue_ForTargetProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateTarget(typeof(TestEntity).GetProperty("MaxLengthProperty")!)).IsTrue();

    [Test]
    public async Task IsTemplateTarget_ReturnsFalse_ForUnflaggedProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateTarget(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsFalse();

    [Test]
    public async Task IsTemplateSource_ReturnsTrue_ForSourceProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateSource(typeof(TestEntity).GetProperty("SourceProperty")!)).IsTrue();

    [Test]
    public async Task IsTemplateSource_ReturnsFalse_ForTargetProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.IsTemplateSource(typeof(TestEntity).GetProperty("MaxLengthProperty")!)).IsFalse();

    [Test]
    public async Task GetRegex_ReturnsNotNull_ForRegexProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.GetRegex(typeof(TestEntity).GetProperty("SecretProperty")!)).IsNotNull();

    [Test]
    public async Task GetRegex_ReturnsNull_ForNonRegexProperty_ViaPropertyInfo() =>
        await Assert.That(AttributeAnalyzer<TestEntity>.GetRegex(typeof(TestEntity).GetProperty("RequiredProperty")!)).IsNull();
}