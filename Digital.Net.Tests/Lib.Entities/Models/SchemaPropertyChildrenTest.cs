using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Tests.Core;

namespace Digital.Net.Tests.Lib.Entities.Models;

public class SchemaPropertyChildrenTest : UnitTest
{
    private class ChildEntity : Entity
    {
        [Required]
        public string Name { get; set; } = string.Empty;
    }

    private class ParentEntity : Entity
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [ChildSchema]
        public virtual List<ChildEntity> InlineChildren { get; set; } = [];

        public virtual List<ChildEntity> ServerManagedChildren { get; set; } = [];
    }

    [PivotResolution("/ownedItems", Ownership.Cascade)]
    private class OwnedPivot : Pivot<ParentEntity, ChildEntity>;

    [PivotResolution("/linkedItems", Ownership.Dissociate)]
    private class LinkedPivot : Pivot<ParentEntity, ChildEntity>;

    [Test]
    public async Task Get_AppendsCollectionEntry_ForCascadePivot()
    {
        var entry = SchemaProperty<ParentEntity>.Get().FirstOrDefault(s => s.Name == "OwnedItems");
        await Assert.That(entry).IsNotNull();
        await Assert.That(entry!.Path).IsEqualTo("ownedItems");
        await Assert.That(entry.Type).IsEqualTo("Collection");
        await Assert.That(entry.IsRequired).IsFalse();
        await Assert.That(entry.IsReadOnly).IsFalse();
        await Assert.That(entry.IsIdentity).IsFalse();
        await Assert.That(entry.Children).IsNotNull();
        await Assert.That(entry.Children!.Any(c => c.Name == "Name" && c.IsRequired)).IsTrue();
    }

    [Test]
    public async Task Get_IgnoresDissociatePivot()
    {
        var schema = SchemaProperty<ParentEntity>.Get();
        await Assert.That(schema.Any(s => s.Name == "LinkedItems")).IsFalse();
    }

    [Test]
    public async Task Get_EmbedsChildren_ForChildSchemaNavigation()
    {
        var entry = SchemaProperty<ParentEntity>.Get().First(s => s.Name == "InlineChildren");
        await Assert.That(entry.Type).IsEqualTo("Collection");
        await Assert.That(entry.Children).IsNotNull();
        await Assert.That(entry.Children!.Any(c => c.Name == "Name" && c.IsRequired)).IsTrue();
    }

    [Test]
    public async Task Get_LeavesChildrenNull_ForUnflaggedNavigation()
    {
        var entry = SchemaProperty<ParentEntity>.Get().First(s => s.Name == "ServerManagedChildren");
        await Assert.That(entry.Type).IsEqualTo("Collection");
        await Assert.That(entry.Children).IsNull();
    }

    [Test]
    public async Task Get_EmbeddedChildren_StayOneLevelDeep()
    {
        var entry = SchemaProperty<ParentEntity>.Get().First(s => s.Name == "OwnedItems");
        await Assert.That(entry.Children!.All(c => c.Children is null)).IsTrue();
    }

    [Test]
    public async Task Validate_IsUnaffected_ByCollectionEntries()
    {
        _ = SchemaProperty<ParentEntity>.Get();
        var entity = new ParentEntity { Title = "valid", InlineChildren = [new ChildEntity { Name = "child" }] };
        await Assert.That(() => SchemaProperty<ParentEntity>.Validate(entity)).ThrowsNothing();
    }

    [Test]
    public async Task Serialization_EmitsChildren_ThroughTheBaseContract()
    {
        var json = JsonSerializer.Serialize(SchemaProperty<ParentEntity>.Get(), JsonSerializerOptions.Web);
        using var document = JsonDocument.Parse(json);
        var entry = document.RootElement
            .EnumerateArray()
            .First(e => e.GetProperty("name").GetString() == "OwnedItems");
        await Assert.That(entry.GetProperty("type").GetString()).IsEqualTo("Collection");
        var children = entry.GetProperty("children").EnumerateArray().ToList();
        await Assert.That(children.Any(c => c.GetProperty("name").GetString() == "Name")).IsTrue();
    }
}
