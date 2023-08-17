using Kitchen;
using KitchenData;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;

namespace KitchenSmartGrabberPullBugFix
{
    public class ReplaceMisgrabbedItem : DaySystem, IModSystem
    {
        EntityQuery Grabbers;

        protected override void Initialise()
        {
            base.Initialise();
            Grabbers = GetEntityQuery(typeof(CConveyPushItems), typeof(CItemHolder));
        }

        protected override void OnUpdate()
        {
            using NativeArray<Entity> entities = Grabbers.ToEntityArray(Allocator.Temp);
            using NativeArray<CConveyPushItems> pushItems = Grabbers.ToComponentDataArray<CConveyPushItems>(Allocator.Temp);
            using NativeArray<CItemHolder> holders = Grabbers.ToComponentDataArray<CItemHolder>(Allocator.Temp);

            EntityContext ctx = new EntityContext(EntityManager);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                CConveyPushItems push = pushItems[i];
                CItemHolder holder = holders[i];

                if (push.State != CConveyPushItems.ConveyState.Grab || !push.GrabSpecificType || push.SpecificType == 0 || holder.HeldItem == default || !Require(holder.HeldItem, out CItem item))
                    continue;

                bool requireFix = item.ID != push.SpecificType || item.Items.Count != push.SpecificComponents.Count;

                if (!requireFix)
                {
                    for (int j = 0; j < item.Items.Count; j++)
                    {
                        int componentID = item.Items[j];
                        bool foundComponent = false;
                        for (int k = 0; k < push.SpecificComponents.Count; k++)
                        {
                            if (componentID == push.SpecificComponents[k])
                            {
                                foundComponent = true;
                                break;
                            }
                        }
                        if (!foundComponent)
                        {
                            requireFix = true;
                            break;
                        }
                    }
                }
                if (!requireFix)
                    continue;

                string origItemName = "Unknown";
                if (GameData.Main.TryGet(item.ID, out Item origItem))
                    origItemName = origItem.name;
                Main.LogError($"Misgrabbed item detected! ({origItemName})");
                if (!GameData.Main.TryGet(push.SpecificType, out Item itemGDO, warn_if_fail: true))
                    continue;
                EntityManager.DestroyEntity(holder.HeldItem);

                bool isGroup = itemGDO is ItemGroup;
                Entity newItem;
                if (isGroup)
                {
                    newItem = ctx.CreateItemGroup(push.SpecificType, push.SpecificComponents);
                }
                else
                {
                    newItem = ctx.CreateItem(push.SpecificType);
                }
                ctx.UpdateHolder(newItem, entity);
                Main.LogWarning($"Replaced misgrabbed item with {itemGDO.name}.");
            }
        }
    }
}
