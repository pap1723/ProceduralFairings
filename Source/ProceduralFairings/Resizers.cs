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

        [KSPField] public float dragAreaScale = 1;

        [KSPField(guiActiveEditor = true, guiName = "Mass", groupName = PFUtils.PAWGroup)]
        public string massDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Cost", groupName = PFUtils.PAWGroup)]
        public string costDisplay;

        protected float oldSize = -1000;
        protected bool justLoaded;
        public float totalMass;

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defcost, ModifierStagingSituation sit) => (totalMass * costPerTonne) - defcost;
        public float GetModuleMass(float defmass, ModifierStagingSituation sit) => totalMass - defmass;

        public void Start ()
        {
            part.mass = totalMass;
        }

        public override void OnStart (StartState state)
        {
            base.OnStart (state);

            if (HighLogic.LoadedSceneIsEditor)
                ConfigureTechLimits();

            updateNodeSize (size);
            part.mass = totalMass;
        }

        public override void OnLoad (ConfigNode cfg)
        {
            base.OnLoad (cfg);

            justLoaded = true;

            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                updateNodeSize (size);
            }
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

        public virtual void FixedUpdate ()
        {
            if (!size.Equals (oldSize))
            {
                resizePart (size);

                StartCoroutine (PFUtils.updateDragCubeCoroutine (part, dragAreaScale));
            }

            justLoaded = false;
        }

        public void scaleNode (AttachNode node, float scale, bool setSize)
        {
            if (node == null)
            {
                return;
            }

            node.position = node.originalPosition * scale;

            if (!justLoaded)
            {
                PFUtils.updateAttachedPartPos (node, part);
            }

            if (setSize)
            {
                node.size = Mathf.RoundToInt (scale / diameterStepLarge);
            }

            if (node.attachedPart != null)
            {
                var baseEventDatum = new BaseEventDetails (0);

                baseEventDatum.Set<Vector3>("location", node.position);
                baseEventDatum.Set<Vector3>("orientation", node.orientation);
                baseEventDatum.Set<Vector3>("secondaryAxis", node.secondaryAxis);
                baseEventDatum.Set<AttachNode>("node", node);

                node.attachedPart.SendEvent ("OnPartAttachNodePositionChanged", baseEventDatum);
            }
        }

        public void setNodeSize (AttachNode node, float scale)
        {
            if (node == null)
            {
                return;
            }

            node.size = Mathf.RoundToInt (scale / diameterStepLarge);
        }

        public virtual void updateNodeSize (float scale)
        {
            setNodeSize (part.FindAttachNode ("top"), scale);
            setNodeSize (part.FindAttachNode ("bottom"), scale);

            var nodes = part.FindAttachNodes ("interstage");

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    setNodeSize (nodes [i], scale);
                }
            }
        }

        public virtual void resizePart (float scale)
        {
            oldSize = size;

            part.mass = totalMass = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale + specificMass.w;

            massDisplay = PFUtils.formatMass (totalMass);
            costDisplay = PFUtils.formatCost (part.partInfo.cost + GetModuleCost(part.partInfo.cost, ModifierStagingSituation.CURRENT) + part.partInfo.cost);

            part.breakingForce = specificBreakingForce * Mathf.Pow (scale, 2);
            part.breakingTorque = specificBreakingTorque * Mathf.Pow (scale, 2);

            var model = part.FindModelTransform ("model");

            if (model != null)
            {
                model.localScale = Vector3.one * scale;
            }
            else
            {
                Debug.LogError ("[PF]: No 'model' transform found in part!", this);
            }

            part.rescaleFactor = scale;

            scaleNode(part.FindAttachNode ("top"), scale, true);
            scaleNode(part.FindAttachNode ("bottom"), scale, true);

            var nodes = part.FindAttachNodes ("interstage");

            if (nodes != null)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    scaleNode (nodes [i], scale, true);
                }
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

            int sideNodeSize = Mathf.RoundToInt (scale / diameterStepLarge) - 1;

            if (sideNodeSize < 0)
            {
                sideNodeSize = 0;
            }

            var nodes = part.FindAttachNodes ("connect");

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes [i];

                n.size = sideNodeSize;
            }
        }

        public override void resizePart (float scale)
        {
            float sth = CalcSideThickness();

            float br = size * 0.5f - sth;
            scale = br * 2;

            base.resizePart (scale);

            var topNode = part.FindAttachNode ("top");
            var bottomNode = part.FindAttachNode ("bottom");

            float y = (topNode.position.y + bottomNode.position.y) * 0.5f;

            int sideNodeSize = Mathf.RoundToInt(scale / diameterStepLarge) - 1;

            if (sideNodeSize < 0)
            {
                sideNodeSize = 0;
            }

            var nodes = part.FindAttachNodes ("connect");

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes [i];

                n.position.y = y;
                n.size = sideNodeSize;

                if (!justLoaded)
                {
                    PFUtils.updateAttachedPartPos (n, part);
                }
            }

            var nnt = part.GetComponent<KzNodeNumberTweaker>();

            if (nnt)
            {
                nnt.radius = size * 0.5f;
            }

            var fbase = part.GetComponent<ProceduralFairingBase>();

            if (fbase)
            {
                fbase.baseSize = br * 2;
                fbase.sideThickness = sth;
                fbase.needShapeUpdate = true;
            }
        }
    }

    public class KzThrustPlateResizer : KzPartResizer
    {
        public override void resizePart (float scale)
        {
            base.resizePart (scale);

            var node = part.FindAttachNode ("bottom");

            var nodes = part.FindAttachNodes ("bottom");

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes [i];

                n.position.y = node.position.y;

                if (!justLoaded)
                {
                    PFUtils.updateAttachedPartPos (n, part);
                }
            }

            var nnt = part.GetComponent<KzNodeNumberTweaker>();

            if (nnt)
            {
                float mr = size * 0.5f;

                if (nnt.radius > mr)
                {
                    nnt.radius = mr;
                }

                ((UI_FloatEdit) nnt.Fields["radius"].uiControlEditor).maxValue = mr;
            }
        }
    }
}
