//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using System.Collections;
using UnityEngine;

namespace Keramzit
{
    public class ProceduralFairingAdapter : PartModule
    {
        // Leave these fields here, so a legacy loader can reference them.
//        [KSPField(isPersistant = true)] public float baseSize;
        [Persistent] public float baseSize;
        [Persistent] public float topSize;
        [Persistent] public float height;
        [Persistent] public float extraHeight;

        [KSPField] public string topNodeName = "top1";

        [KSPField (isPersistant = true)]
        public bool topNodeDecouplesWhenFairingsGone;

        [KSPEvent(name = "decNoFairings", active = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        public void UIToggleTopNodeDecouple()
        {
            topNodeDecouplesWhenFairingsGone = !topNodeDecouplesWhenFairingsGone;
            UpdateUIdecNoFairingsText(topNodeDecouplesWhenFairingsGone);
        }


        public override void OnStart (StartState state)
        {
            base.OnStart (state);
            UpdateUIdecNoFairingsText (topNodeDecouplesWhenFairingsGone);
        }

        void UpdateUIdecNoFairingsText(bool flag)
        {
            Events[nameof(UIToggleTopNodeDecouple)].guiName = $"Decouple when Fairing gone: {(flag ? "Yes" : "No")}";
        }

        public IEnumerator HandleAutomaticDecoupling()
        {
            //  We used to remove the engine fairing (if there is any) from topmost node, but lately that's been causing NREs.  
            //  Since KSP gives us this option nativley, let's just use KSP to do that if we want.
            if (!HighLogic.LoadedSceneIsFlight) yield break;
            yield return new WaitForFixedUpdate();

            if (TopNodePartPresent && topNodeDecouplesWhenFairingsGone && !FairingPresent)
            {
                if (part.FindModuleImplementing<ModuleDecouple>() is ModuleDecouple item)
                {
                    RemoveTopPartJoints();
                    item.Decouple();
                }
                else
                {
                    Debug.LogError($"[PF]: Cannot decouple from top part! {this}");
                }
            }
            Events[nameof(UIToggleTopNodeDecouple)].guiActive = TopNodePartPresent;
        }

        public Part getBottomPart() => (part.FindAttachNode("bottom") is AttachNode node) ? node.attachedPart : null;
        public Part getTopPart() => (part.FindAttachNode(topNodeName) is AttachNode node) ? node.attachedPart : null;
        public bool TopNodePartPresent => getTopPart() is Part;
        public bool FairingPresent
        {
            get
            {
                if (part.FindAttachNodes("connect") is AttachNode[] nodes)
                {
                    foreach (AttachNode n in nodes)
                    {
                        if (n.attachedPart is Part p && p.FindModuleImplementing<ProceduralFairingSide>() is ProceduralFairingSide)
                            return true;
                    }
                }
                return false;
            }
        }

        void RemoveTopPartJoints()
        {
            if (getTopPart() is Part topPart && getBottomPart() is Part bottomPart &&
                topPart.gameObject.GetComponents<ConfigurableJoint>() is ConfigurableJoint[] components)
            {
                foreach (ConfigurableJoint configurableJoint in components)
                {
                    if (configurableJoint.connectedBody == bottomPart.Rigidbody)
                        Destroy(configurableJoint);
                }
            }
        }
    }
}
