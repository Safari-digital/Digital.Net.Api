using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Lib.Abstractions;
using Digital.Net.Lib.Entities.Attributes;
using Digital.Net.Lib.Entities.Exceptions;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Random;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;

namespace Digital.Net.Tests.Core.Http.Services.Crud;

public class SchemaValidationTest : UnitTest
{
    private class SecretSample : IEntity
    {
        [Secret]
        public string? Token { get; set; }
    }

    [Test]
    public async Task ValidatePathMutation_Throws_WhenFieldIsSecret()
    {
        var property = SchemaProperty<SecretSample>.Get().First(p => p.Name == nameof(SecretSample.Token));
        await Assert.That(() => property.ValidatePathMutation("value", "/Token"))
            .Throws<EntityValidationException>();
    }

    private static CrudTestEntity GetValidEntity() => new()
    {
        Name = Randomizer.GenerateRandomString(Randomizer.AnyLetterOrNumber, 8),
        UniqueField = Randomizer.GenerateRandomString(Randomizer.AnyLetterOrNumber, 16),
    };

    private static CrudTestChild GetValidChild() => new()
    {
        Label = Randomizer.GenerateRandomString(Randomizer.AnyLetterOrNumber, 8),
    };

    [Test]
    public async Task ValidateEntity_DoesNotThrow_WhenEntityIsValid()
    {
        var entity = GetValidEntity();
        await Assert.That(() => SchemaProperty<CrudTestEntity>.Validate(entity)).ThrowsNothing();
    }

    [Test]
    public async Task ValidateEntity_DoesNotThrow_WhenIdentityIsDefaultGuid()
    {
        var entity = GetValidEntity();
        await Assert.That(entity.Id).IsEqualTo(Guid.Empty);
        await Assert.That(() => SchemaProperty<CrudTestEntity>.Validate(entity)).ThrowsNothing();
    }

    [Test]
    public async Task ValidateEntity_Throws_WhenRequiredStringIsEmpty()
    {
        var entity = GetValidEntity();
        entity.UniqueField = "";
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaProperty<CrudTestEntity>.Validate(entity);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidateEntity_Throws_WhenRegexFails()
    {
        var entity = GetValidEntity();
        entity.Name = "ab";
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaProperty<CrudTestEntity>.Validate(entity);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidateEntity_Throws_WhenOneOfValueIsUnknown()
    {
        var entity = GetValidEntity();
        entity.OneOfField = "delta";
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaProperty<CrudTestEntity>.Validate(entity);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidatePatch_DoesNotThrow_WhenValueIsNull()
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Replace(e => e.OneOfField, null);
        await Assert.That(() => SchemaPatchValidator.Validate(patch)).ThrowsNothing();
    }

    [Test]
    [Arguments("copy")]
    [Arguments("move")]
    [Arguments("test")]
    public async Task ValidatePatch_Throws_WhenOperationIsOutsideAllowList(string operation)
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Operations.Add(new Operation<CrudTestEntity>
        {
            op = operation, from = "/Name", path = "/ReadOnlyField",
        });
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaPatchValidator.Validate(patch);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidatePatch_DoesNotThrow_WhenPathIsDeeperThanTwoLevels()
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Operations.Add(new Operation<CrudTestEntity>
        {
            op = "replace", path = "/Children/0/Label", value = "ab",
        });
        await Assert.That(() => SchemaPatchValidator.Validate(patch)).ThrowsNothing();
    }

    [Test]
    public async Task ValidatePatch_DoesNotThrow_WhenPropertyIsNotInSchema()
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Operations.Add(new Operation<CrudTestEntity>
        {
            op = "replace", path = "/UnknownField", value = "x",
        });
        await Assert.That(() => SchemaPatchValidator.Validate(patch)).ThrowsNothing();
    }

    [Test]
    public async Task ValidatePatch_DoesNotThrow_WhenScalarReplaceIsValid()
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Replace(e => e.Name, Randomizer.GenerateRandomString(Randomizer.AnyLetterOrNumber, 8));
        await Assert.That(() => SchemaPatchValidator.Validate(patch)).ThrowsNothing();
    }

    [Test]
    public async Task ValidatePatch_Throws_WhenScalarReplaceTargetsReadOnlyField()
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Replace(e => e.ReadOnlyField, "blocked");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaPatchValidator.Validate(patch);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidatePatch_Throws_WhenScalarReplaceFailsOneOf()
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Replace(e => e.OneOfField, "delta");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaPatchValidator.Validate(patch);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidatePatch_DoesNotThrow_WhenAddingValidChildToCollection()
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Add(e => e.Children, GetValidChild());
        await Assert.That(() => SchemaPatchValidator.Validate(patch)).ThrowsNothing();
    }

    [Test]
    public async Task ValidatePatch_Throws_WhenAddingChildWithInvalidProperty()
    {
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Add(e => e.Children, new CrudTestChild { Label = "" });
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaPatchValidator.Validate(patch);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidatePatch_Throws_WhenAddingChildToReadOnlyNavigation()
    {
        // Single-nav read-only path falls through to ValidatePathMutation,
        // which rejects the mutation because LockedChild is [ReadOnly].
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Add(e => e.LockedChild, GetValidChild());
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaPatchValidator.Validate(patch);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidatePatch_Throws_WhenDepthTwoTargetsReadOnlyParent()
    {
        // Hits the depth-2 branch where the parent SchemaProperty is read-only.
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Operations.Add(new Operation<CrudTestEntity>
        {
            op = "add", path = "/LockedChild/-", value = GetValidChild(),
        });
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaPatchValidator.Validate(patch);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ValidatePatch_ContinuesAfterSkippedOp_WhenLaterOpIsInvalid()
    {
        // Regression guard: a value-less but allowed op (here remove) must `continue`, not `return`.
        // A later invalid op must still throw.
        var patch = new JsonPatchDocument<CrudTestEntity>();
        patch.Operations.Add(new Operation<CrudTestEntity>
        {
            op = "remove", path = "/ReadOnlyField",
        });
        patch.Replace(e => e.OneOfField, "delta");
        await Assert.ThrowsAsync<EntityValidationException>(async () =>
        {
            SchemaPatchValidator.Validate(patch);
            await Task.CompletedTask;
        });
    }
}
