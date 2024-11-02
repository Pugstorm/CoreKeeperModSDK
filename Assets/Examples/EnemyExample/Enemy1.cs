using Unity.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class Enemy1 : EntityMonoBehaviour
{
    public GameObject graphics;
    
    protected override void DeathEffect()
    {
        graphics.SetActive(false);
        base.DeathEffect();
    }

    // Any IGraphicalObject component will get called by UpdateGraphicalObjectSystem
    public override void GraphicalUpdate(Entity entity, EntityManager entityManager, TimeData timeData)
    {
	    base.GraphicalUpdate(entity, entityManager, timeData);

		// Need to handle EntityMonoBehaviour update manually, hopefully more automatic in future
	    UpdatePosition(true, EntityUtility.GetComponentData<LocalToWorld>(entity, world));
	    UpdateDestroyedState(EntityUtility.GetComponentData<HealthCD>(entity, world).health <= 0);

	    if (conditionEffectsHandler != null)
	    {
		    conditionEffectsHandler.UpdateShowing(false);
	    }

	    // TODO: Missing some references
	    //if (HasConditions() && conditionEffectsHandler != null)
	    //{
		//    conditionEffectsHandler.UpdateShowing(false);
		//    var summarizedConditionsBufferLookup = world.Unmanaged.GetExistingSystemState<PugQuerySystem>().GetBufferLookup<SummarizedConditionsBuffer>(true);
		//    var summarizedConditionsEffectsBufferLookup = world.Unmanaged.GetExistingSystemState<PugQuerySystem>().GetBufferLookup<SummarizedConditionsEffectsBuffer>(true);
		//    conditionEffectsHandler.UpdateConditionsVisuals(this, false, default, summarizedConditionsBufferLookup, summarizedConditionsEffectsBufferLookup);
	    //}
    }
}