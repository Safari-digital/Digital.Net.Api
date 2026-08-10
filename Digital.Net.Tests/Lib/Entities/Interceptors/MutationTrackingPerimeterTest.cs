using Digital.Net.Cms.Models;
using Digital.Net.Cms.Models.Forms;
using Digital.Net.Cms.Models.Medias;
using Digital.Net.Cms.Models.Pages;
using Digital.Net.Core.Entities.Models.ApiKeys;
using Digital.Net.Core.Entities.Models.Auth;
using Digital.Net.Core.Entities.Models.Avatars;
using Digital.Net.Core.Entities.Models.ConfigValues;
using Digital.Net.Core.Entities.Models.Documents;
using Digital.Net.Core.Entities.Models.Sessions;
using Digital.Net.Core.Entities.Models.Users;
using Digital.Net.Lib.Entities.Models;
using Digital.Net.Lib.Entities.Mutations;

namespace Digital.Net.Tests.Lib.Entities.Interceptors;

/// <summary>
///     Documents & guards the mutation-tracking perimeter (US-MUT-06). Pure reflection — no DB.
/// </summary>
public class MutationTrackingPerimeterTest
{
    [Test]
    [Arguments(typeof(User))]
    [Arguments(typeof(ConfigValue))]
    [Arguments(typeof(ApiKey))]
    [Arguments(typeof(Document))]
    [Arguments(typeof(Page))]
    [Arguments(typeof(Media))]
    [Arguments(typeof(Form))]
    [Arguments(typeof(FormField))]
    [Arguments(typeof(FormSubmission))]
    // Sheets carry editorial content, which is precisely what an audit trail is for.
    [Arguments(typeof(Sheet))]
    public async Task Tracked_entities_are_Entity_and_not_excluded(Type type)
    {
        await Assert.That(typeof(Entity).IsAssignableFrom(type)).IsTrue();
        await Assert.That(typeof(IUntrackedEntity).IsAssignableFrom(type)).IsFalse();
    }

    [Test]
    [Arguments(typeof(Session))]
    [Arguments(typeof(AuthEvent))]
    [Arguments(typeof(EntityMutation))]
    [Arguments(typeof(Avatar))]
    [Arguments(typeof(MediaVariant))]
    [Arguments(typeof(OpenGraphEntry))]
    public async Task Excluded_entities_implement_IUntrackedEntity(Type type) =>
        await Assert.That(typeof(IUntrackedEntity).IsAssignableFrom(type)).IsTrue();

    [Test]
    [Arguments(typeof(PageSheet))]
    [Arguments(typeof(PageOpenGraph))]
    public async Task Pivots_are_IEntity_but_not_Entity(Type type)
    {
        await Assert.That(typeof(IEntity).IsAssignableFrom(type)).IsTrue();
        await Assert.That(typeof(Entity).IsAssignableFrom(type)).IsFalse();
    }

    [Test]
    [Arguments(typeof(User))]
    [Arguments(typeof(ApiKey))]
    public async Task Restricted_entities_are_tracked_and_implement_IRestrictedAuditEntity(Type type)
    {
        await Assert.That(typeof(IRestrictedAuditEntity).IsAssignableFrom(type)).IsTrue();
        await Assert.That(typeof(Entity).IsAssignableFrom(type)).IsTrue();
        await Assert.That(typeof(IUntrackedEntity).IsAssignableFrom(type)).IsFalse();
    }
}