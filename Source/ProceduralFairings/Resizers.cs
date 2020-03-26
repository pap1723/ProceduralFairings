//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using UnityEngine;

namespace Keramzit
{
    public class KzFairingBaseResizer : PartModule
    {
        //        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Size", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        //        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 5, incrementLarge = 1.25f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        //        public float size = 1.25f;
        //public virtual void resizePart(float scale, bool pushAttachments) { }
        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            StartCoroutine(DestroyMe());
        }
        private System.Collections.IEnumerator DestroyMe()
        {
            yield return new WaitForSeconds(1);
            Destroy(this);
        }
    }

    public class KzThrustPlateResizer : PartModule
    {
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Size", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 5, incrementLarge = 1.25f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        public float size = 1.25f;
        public override void OnStartFinished(StartState state)
        {
            GameEvents.onVariantApplied.Add(OnPartVariantApplied);
            base.OnStartFinished(state);
            resizePart(size, false);
        }
        private void OnPartVariantApplied(Part p, PartVariant variant)
        {
            if (p == part) StartCoroutine(OnPartVariantAppliedCR());
        }

        private System.Collections.IEnumerator OnPartVariantAppliedCR()
        {
            yield return new WaitForFixedUpdate();
            resizePart(size, false);
        }

        public void OnDestroy()
        {
            GameEvents.onVariantApplied.Remove(OnPartVariantApplied);
        }

        public virtual void resizePart(float scale, bool pushAttachments)
        {
            if (part.FindAttachNode("bottom") is AttachNode node &&
                part.FindAttachNodes("bottom") is AttachNode[] nodes)
            {
                foreach (AttachNode n in nodes)
                {
                    Vector3 newPos = new Vector3(n.position.x, node.position.y, n.position.z);
                    PFUtils.UpdateNode(part, n, newPos, node.size, pushAttachments);
                }
            }

            if (part.GetComponent<KzNodeNumberTweaker>() is KzNodeNumberTweaker nnt)
            {
                nnt.SetRadius(Math.Min(nnt.radius, size / 2), pushAttachments);
                (nnt.Fields[nameof(nnt.radius)].uiControlEditor as UI_FloatEdit).maxValue = size / 2;
            }
        }
    }
}
