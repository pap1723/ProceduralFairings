//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using UnityEngine;

namespace Keramzit
{
    public abstract class KzPartResizer : PartModule, IPartCostModifier, IPartMassModifier
    {
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Size", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 5, incrementLarge = 1.25f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        public float size = 1.25f;

        [KSPField] public float diameterStepLarge = 1.25f;
        [KSPField] public float diameterStepSmall = 0.125f;

        [KSPField] public Vector4 specificMass = new Vector4 (0.005f, 0.011f, 0.009f, 0f);
        [KSPField] public float specificBreakingForce = 1536;
        [KSPField] public float specificBreakingTorque = 1536;
        [KSPField] public float costPerTonne = 2000;

        [KSPField] public string minSizeName = "PROCFAIRINGS_MINDIAMETER";
        [KSPField] public string maxSizeName = "PROCFAIRINGS_MAXDIAMETER";

        [KSPField(guiActiveEditor = true, guiName = "Mass", groupName = PFUtils.PAWGroup)]
        public string massDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Cost", groupName = PFUtils.PAWGroup)]
        public string costDisplay;

        protected float oldSize = -1000;
        public float totalMass;

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defcost, ModifierStagingSituation sit) => (totalMass * costPerTonne) - defcost;
        public float GetModuleMass(float defmass, ModifierStagingSituation sit) => totalMass - defmass;

        public override void OnStart (StartState state)
        {
            base.OnStart (state);

            if (HighLogic.LoadedSceneIsEditor)
            {
                (Fields[nameof(size)].uiControlEditor as UI_FloatEdit).onFieldChanged += OnSizeChanged;
                (Fields[nameof(size)].uiControlEditor as UI_FloatEdit).onSymmetryFieldChanged += OnSizeChanged;
                ConfigureTechLimits();
                StartCoroutine(EditorChangeDetector());
            }

            updateNodeSize(size);
            resizePart(size, false);
            oldSize = size;
        }

        private void OnSizeChanged(BaseField f, object obj)
        {
            if (size != oldSize)
            {
                resizePart(size, true);
                if (part.GetComponent<ProceduralFairingBase>() is ProceduralFairingBase fbase)
                    fbase.UpdateShape();
            }
            oldSize = size;
        }

        public void ConfigureTechLimits()
        {
            if (PFUtils.canCheckTech())
            {
                float minSize = PFUtils.getTechMinValue(minSizeName, 0.25f);
                float maxSize = PFUtils.getTechMaxValue(maxSizeName, 30);

                PFUtils.setFieldRange(Fields[nameof(size)], minSize, maxSize);

                (Fields[nameof(size)].uiControlEditor as UI_FloatEdit).incrementLarge = diameterStepLarge;
                (Fields[nameof(size)].uiControlEditor as UI_FloatEdit).incrementSmall = diameterStepSmall;
            }
            else if (HighLogic.LoadedSceneIsEditor && ResearchAndDevelopment.Instance == null)
            {
                Debug.LogError($"[PF] ConfigureTechLimits() in Editor but R&D not ready!");
            }
        }

        private System.Collections.IEnumerator EditorChangeDetector()
        {
            while (HighLogic.LoadedSceneIsEditor)
            {
                yield return new WaitForFixedUpdate();
                if (size != oldSize)
                    OnSizeChanged(Fields[nameof(size)], oldSize);
            }
        }

        public void scaleNode (AttachNode node, float scale, bool setSize, bool pushAttachments)
        {
            if (node is AttachNode)
            {
                Vector3 oldPosWorld = part.transform.TransformPoint(node.position);
                node.position = node.originalPosition * scale;

                if (pushAttachments)
                    PFUtils.updateAttachedPartPos(node, part, oldPosWorld);

                if (setSize)
                    node.size = Mathf.RoundToInt(scale / diameterStepLarge);

                if (node.attachedPart is Part)
                {
                    var baseEventDatum = new BaseEventDetails(0);

                    baseEventDatum.Set("location", node.position);
                    baseEventDatum.Set("orientation", node.orientation);
                    baseEventDatum.Set("secondaryAxis", node.secondaryAxis);
                    baseEventDatum.Set("node", node);

                    node.attachedPart.SendEvent("OnPartAttachNodePositionChanged", baseEventDatum);
                }
            }
        }

        public void setNodeSize (AttachNode node, float scale)
        {
            if (node is AttachNode)
                node.size = Mathf.RoundToInt(scale / diameterStepLarge);
        }

        public virtual void updateNodeSize (float scale)
        {
            setNodeSize (part.FindAttachNode ("top"), scale);
            setNodeSize (part.FindAttachNode ("bottom"), scale);

            if (part.FindAttachNodes("interstage") is AttachNode[] nodes)
                foreach (AttachNode node in nodes)
                {
                    setNodeSize(node, scale);
                }
        }

        public virtual void resizePart (float scale, bool pushAttachments)
        {
            part.mass = totalMass = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale + specificMass.w;

            massDisplay = PFUtils.formatMass (totalMass);
            costDisplay = PFUtils.formatCost (part.partInfo.cost + GetModuleCost(part.partInfo.cost, ModifierStagingSituation.CURRENT));

            part.breakingForce = specificBreakingForce * Mathf.Pow (scale, 2);
            part.breakingTorque = specificBreakingTorque * Mathf.Pow (scale, 2);

            if (part.FindModelTransform("model") is Transform model)
                model.localScale = Vector3.one * scale;
            else
                Debug.LogError ("[PF]: No 'model' transform found in part!", this);

            part.rescaleFactor = scale;

            scaleNode(part.FindAttachNode ("top"), scale, true, pushAttachments);
            scaleNode(part.FindAttachNode ("bottom"), scale, true, pushAttachments);
            if (part.FindAttachNodes("interstage") is AttachNode[] nodes)
                foreach (AttachNode node in nodes)
                {
                    scaleNode(node, scale, true, pushAttachments);
                }
        }
    }

    public class KzFairingBaseResizer : KzPartResizer
    {
        [KSPField] public float sideThickness = 0.05f / 1.25f;

        public float CalcSideThickness() => Mathf.Min(sideThickness * size, size * 0.25f);

        public override void updateNodeSize (float scale)
        {
            float sth = CalcSideThickness();

            float br = size * 0.5f - sth;
            scale = br * 2;

            base.updateNodeSize (scale);

            int sideNodeSize = Math.Max(0, Mathf.RoundToInt (scale / diameterStepLarge) - 1);
            if (part.FindAttachNodes("connect") is AttachNode[] nodes)
                foreach (AttachNode node in nodes)
                {
                    node.size = sideNodeSize;
                }
        }

        public override void resizePart (float scale, bool pushAttachments)
        {
            float sth = CalcSideThickness();

            float br = size * 0.5f - sth;
            scale = br * 2;

            base.resizePart(scale, pushAttachments);

            var topNode = part.FindAttachNode ("top");
            var bottomNode = part.FindAttachNode ("bottom");

            float y = (topNode.position.y + bottomNode.position.y) / 2f;

            int sideNodeSize = Math.Max(0, Mathf.RoundToInt(scale / diameterStepLarge) - 1);
            if (part.FindAttachNodes("connect") is AttachNode[] nodes)
                foreach (AttachNode node in nodes)
                {
                    Vector3 oldPosWorld = part.transform.TransformPoint(node.position);
                    node.position.y = y;
                    node.size = sideNodeSize;

                    if (pushAttachments)
                        PFUtils.updateAttachedPartPos(node, part, oldPosWorld);
                }

            if (part.GetComponent<KzNodeNumberTweaker>() is KzNodeNumberTweaker nnt)
                nnt.SetRadius(size / 2, pushAttachments);

            if (part.GetComponent<ProceduralFairingBase>() is ProceduralFairingBase fbase)
            {
                fbase.baseSize = br * 2;
                fbase.sideThickness = sth;
                fbase.recalcShape();
            }
        }
    }

    public class KzThrustPlateResizer : KzPartResizer
    {
        public override void resizePart(float scale, bool pushAttachments)
        {
            base.resizePart(scale, pushAttachments);

            if (part.FindAttachNode("bottom") is AttachNode node &&
                part.FindAttachNodes("bottom") is AttachNode[] nodes)
            {
                foreach (AttachNode n in nodes)
                {
                    Vector3 oldPosWorld = part.transform.TransformPoint(n.position);
                    n.position.y = node.position.y;
                    if (pushAttachments)
                        PFUtils.updateAttachedPartPos(n, part, oldPosWorld);
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
