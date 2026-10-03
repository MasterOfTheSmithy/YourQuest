#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System;
using System.Linq;

public sealed partial class YQDeveloperCommandRegistry
{
    private string _containerId;

    private void RegisterContainerCommands()
    {
        Register("container list", 0, 0, false, "container list : persisted container IDs (open a source once to register it)", a =>
            Ok(string.Join("\n", WorldManager?.State?.containers.Values.Select(c => c.entityId + " / " + c.displayName + " / " + c.contents.Count + " slots") ?? Array.Empty<string>())));
        Register("container select", 1, 1, false, "container select <entityId> : explicitly select one persisted source", a =>
        {
            if (YQContainerInventory.Find(WorldManager?.State, a[0]) == null) return Fail("Unknown container ID; use container list.");
            _containerId = a[0]; return Ok("Selected container " + _containerId);
        });
        Register("container inspect", 0, 0, false, "container inspect : selected context/profile/seed/version/access/restock/current contents", a =>
        {
            var selected = YQContainerInventory.Find(WorldManager?.State, _containerId);
            return selected == null ? Fail("Select a container first.") : Ok(YQContainerInventory.Serialize(selected));
        });
        Register("container regenerate", 0, 1, true, "container regenerate [--replace] : WARNING replaces selected contents; existing test snapshot required", a =>
        {
            // note: Regeneration never touches an implicitly selected nearby entity or publishes to a normal profile automatically.
            WorldState world = WorldManager?.State;
            YQContainerRecord selected = YQContainerInventory.Find(world, _containerId);
            if (selected == null) return Fail("Select one persisted test container explicitly first.");
            if (a.Count == 0) return Fail("WARNING: regenerating " + _containerId + " replaces its items, deposits and gold. Capture test snapshot first, then use container regenerate --replace. No state changed.");
            if (a[0] != "--replace" || !YQDeveloperTestSession.Active || !YQDeveloperTestSession.CheckOwners(out _))
                return Fail("Replacement requires --replace and an existing valid test snapshot. No state changed.");
            if (selected.context.sourceType == YQContainerType.Hostile) return Fail("Hostile equipment/death state cannot be regenerated; select a disposable physical storage container.");
            YQContainerRecord next = YQContainerInventory.Copy(selected);
            next.contents.Clear(); next.currency = 0; next.generated = false; next.equippedItemBySlot.Clear();
            next.hasPlayerDeposits = false; next.legacyConsumed = false; next.revision++;
            if (!YQContainerLoot.Generate(next, GeneratedRpgContentService.Instance, world, State, YQContainerLoot.Catalog, out string message)) return Fail(message);
            if (!YQContainerInventory.PublishRecord(world, State, next, YQWorldContainer.PublishLive, out message)) return Fail(message);
            return Ok("WARNING: SESSION contents replaced for " + _containerId + ". Use test restore to recover the supported snapshot. Normal saves remain blocked.");
        });
        Register("container restock", 1, 1, true, "container restock <eventId> : SESSION explicit empty-source policy; deposits prevent restock", a =>
        {
            if (_containerId == null) return Fail("Select a container first.");
            Session();
            return From(YQContainerLoot.TryRestock(WorldManager?.State, State, _containerId, a[0],
                GeneratedRpgContentService.Instance, YQWorldContainer.PublishLive, out string message), message);
        });
    }
}
#endif
