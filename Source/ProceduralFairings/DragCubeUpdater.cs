using System.Collections.Generic;
using UnityEngine;

namespace ProceduralFairings
{
    public class DragCubeUpdater
    {
        private bool dragUpdating = false;
        private readonly Part part;

        public DragCubeUpdater(Part part)
        {
            this.part = part;
        }

        private IEnumerator<YieldInstruction> UpdateDragCubesCR(float delay = 0)
        {
            if (dragUpdating) yield break;
            dragUpdating = true;
            if (delay == 0)
                yield return new WaitForFixedUpdate();
            else
                yield return new WaitForSeconds(delay);
            while (HighLogic.LoadedSceneIsFlight && (!FlightGlobals.ready || part.packed || !part.vessel.loaded))
                yield return new WaitForFixedUpdate();
            while (HighLogic.LoadedSceneIsEditor && (part.localRoot != EditorLogic.RootPart || part.gameObject.layer == LayerMask.NameToLayer("TransparentFX")))
                yield return new WaitForFixedUpdate();
            Keramzit.PFUtils.updateDragCube(part);
            dragUpdating = false;
        }

        public void Update(float delay = 0)
        {
            if (!dragUpdating)
                part.StartCoroutine(UpdateDragCubesCR(delay));
        }
    }
}
