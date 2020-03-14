//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keramzit
{
    abstract class ProceduralAdapterBase : PartModule
    {
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Base", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 5, incrementLarge = 1.25f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        public float baseSize = 1.25f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Top", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 5, incrementLarge = 1.25f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        public float topSize = 1.25f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Height", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 50, incrementLarge = 1.0f, incrementSmall = 0.1f, incrementSlide = 0.001f)]
        public float height = 1;

        [KSPField] public string topNodeName = "top1";

        [KSPField] public float diameterStepLarge = 1.25f;
        [KSPField] public float diameterStepSmall = 0.125f;

        [KSPField] public float heightStepLarge = 1.0f;
        [KSPField] public float heightStepSmall = 0.1f;

        abstract public float minHeight { get; }

        float lastBaseSize = -1000;
        float lastTopSize = -1000;
        float lastHeight = -1000;

        public int TopNodeSize => Mathf.RoundToInt(topSize / diameterStepLarge);
        public int BottomNodeSize => Mathf.RoundToInt(baseSize / diameterStepLarge);
        public int InterstageNodeSize => Math.Max(1, TopNodeSize - 1);
        public int FairingBaseNodeSize => Math.Max(1, TopNodeSize - 1);

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Fields[nameof(baseSize)].uiControlEditor.onFieldChanged += OnSizeChanged;
            Fields[nameof(baseSize)].uiControlEditor.onSymmetryFieldChanged += OnSizeChanged;

            Fields[nameof(topSize)].uiControlEditor.onFieldChanged += OnTopSizeChanged;
            Fields[nameof(topSize)].uiControlEditor.onSymmetryFieldChanged += OnTopSizeChanged;

            Fields[nameof(height)].uiControlEditor.onFieldChanged += OnHeightChanged;
            Fields[nameof(height)].uiControlEditor.onSymmetryFieldChanged += OnHeightChanged;
        }

        public override void OnStartFinished(StartState state)
        {
            UpdateShape(false);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (baseSize != lastBaseSize)
                    Fields[nameof(baseSize)].uiControlEditor.onFieldChanged.Invoke(Fields[nameof(baseSize)], lastBaseSize);
                if (topSize != lastTopSize)
                    Fields[nameof(topSize)].uiControlEditor.onFieldChanged.Invoke(Fields[nameof(topSize)], lastTopSize);
                if (height != lastHeight)
                    Fields[nameof(height)].uiControlEditor.onFieldChanged.Invoke(Fields[nameof(height)], lastHeight);
            }
        }

        // Generically, these should check f.GetValue(this) != obj.
        // But obj at least pre-1.8 is not the old value in symmetry field callbacks.
        // So we must track the last value manually.
        public void OnSizeChanged(BaseField f, object obj)
        {
            if (baseSize != lastBaseSize)
                UpdateShape(true);
            lastBaseSize = baseSize;
        }

        public void OnTopSizeChanged(BaseField f, object obj)
        {
            if (topSize != lastTopSize)
                UpdateShape(true);
            lastTopSize = topSize;
        }

        public void OnHeightChanged(BaseField f, object obj)
        {
            if (height != lastHeight)
                UpdateShape(true);
            lastHeight = height;
        }

        public virtual void UpdateShape(bool pushAttachments)
        {
            float topHeight = 0, bottomHeight = 0;

            if (part.FindAttachNode("bottom") is AttachNode bottomNode)
            {
                UpdateNode(bottomNode, (bottomNode.originalPosition * baseSize).y, BottomNodeSize, pushAttachments);
                bottomHeight = bottomNode.position.y;
            }

            if (part.FindAttachNode("top") is AttachNode baseTopNode)
            {
                UpdateNode(baseTopNode, (baseTopNode.originalPosition * baseSize).y, TopNodeSize, pushAttachments);
                topHeight = baseTopNode.position.y;
            }

            if (part.FindAttachNode(topNodeName) is AttachNode topNode)
            {
                UpdateNode(topNode, height, TopNodeSize, pushAttachments);
                //topNode.position = new Vector3 (0, height, 0);
            }
            else
            {
                Debug.LogError($"[PF]: No '{topNodeName}' node in part {part}!");
            }

            if (part.FindAttachNodes("interstage") is AttachNode[] internodes)
            {
                var inc = (height - topHeight) / (internodes.Length / 2 + 1);

                for (int i = 0, j = 0; i < internodes.Length; i += 2)
                {
                    var ht = topHeight + ((j + 1) * inc);
                    j++;
                    UpdateNode(internodes[i], ht, InterstageNodeSize, pushAttachments);
                    UpdateNode(internodes[i+1], ht, InterstageNodeSize, pushAttachments);
                }
            }

            float y = (topHeight + bottomHeight) / 2;
            if (part.FindAttachNodes("connect") is AttachNode[] fairingSideNodes)
            {
                foreach (AttachNode n in fairingSideNodes)
                {
                    UpdateNode(n, y, FairingBaseNodeSize, pushAttachments);
                }
            }
        }

        protected void UpdateNode(AttachNode node, float height, int size, bool pushAttachments)
        {
            node.position.y = height;
            node.size = size;

            if (pushAttachments)
                PFUtils.updateAttachedPartPos(node, part);
        }
    }

    class ProceduralFairingAdapter : ProceduralAdapterBase, IPartCostModifier, IPartMassModifier
    {
        [KSPField] public float sideThickness = 0.05f / 1.25f;
        [KSPField] public Vector4 specificMass = new Vector4 (0.005f, 0.011f, 0.009f, 0f);
        [KSPField] public float specificBreakingForce = 6050;
        [KSPField] public float specificBreakingTorque = 6050;
        [KSPField] public float costPerTonne = 2000;

        [KSPField] public float dragAreaScale = 1;

        [KSPField (isPersistant = true)]
        public bool topNodeDecouplesWhenFairingsGone;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Extra height", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0, maxValue = 50, incrementLarge = 1.0f, incrementSmall = 0.1f, incrementSlide = 0.001f)]
        public float extraHeight = 0;

        [KSPField(guiActiveEditor = true, guiName = "Mass", groupName = PFUtils.PAWGroup)]
        public string massDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Cost", groupName = PFUtils.PAWGroup)]
        public string costDisplay;

        float lastExtraHt = -1000;

        [KSPEvent (name = "decNoFairings", active = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        public void UIToggleTopNodeDecouple()
        {
            topNodeDecouplesWhenFairingsGone = !topNodeDecouplesWhenFairingsGone;
            UpdateUIdecNoFairingsText(topNodeDecouplesWhenFairingsGone);
        }

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defcost, ModifierStagingSituation sit) => (totalMass * costPerTonne) - defcost;
        public float GetModuleMass (float defmass, ModifierStagingSituation sit) => totalMass - defmass;
        public float CalcSideThickness() => Mathf.Min(sideThickness * Mathf.Max(baseSize, topSize), Mathf.Min(baseSize, topSize) * 0.25f);

        public float topRadius { get => (topSize / 2) - CalcSideThickness(); }
        public override float minHeight { get => baseSize * 0.2f;}

        public float totalMass;

        public override void OnLoad(ConfigNode cfg)
        {
            base.OnLoad(cfg);

            //  Load legacy settings.
            if (cfg.HasValue("baseRadius") && cfg.HasValue("topRadius"))
            {
                float br = float.Parse(cfg.GetValue("baseRadius"));
                float tr = float.Parse(cfg.GetValue("topRadius"));
                baseSize = (br + sideThickness * br) * 2;
                topSize = (tr + sideThickness * br) * 2;
                sideThickness *= 1.15f / 1.25f;
            }
        }

        public override void OnStart (StartState state)
        {
            base.OnStart (state);
            if (HighLogic.LoadedSceneIsEditor)
                ConfigureTechLimits();

            Fields[nameof(extraHeight)].uiControlEditor.onFieldChanged += OnExtraHeightChanged;
            Fields[nameof(extraHeight)].uiControlEditor.onSymmetryFieldChanged += OnExtraHeightChanged;

            UpdateUIdecNoFairingsText (topNodeDecouplesWhenFairingsGone);

            GameEvents.onVesselWasModified.Add(OnVesselWasModified);
            GameEvents.onStageActivate.Add(OnStageActivate);
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            HandleAutomaticDecoupling();
        }

        public void OnDestroy ()
        {
            GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            GameEvents.onStageActivate.Remove(OnStageActivate);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (extraHeight != lastExtraHt)
                    Fields[nameof(extraHeight)].uiControlEditor.onFieldChanged.Invoke(Fields[nameof(extraHeight)], lastExtraHt);
            }
        }

        public void OnExtraHeightChanged(BaseField f, object obj) 
        {
            if (extraHeight != lastExtraHt)
                UpdateShape(true);
            lastExtraHt = extraHeight;
        }

        void UpdateUIdecNoFairingsText(bool flag)
        {
            Events[nameof(UIToggleTopNodeDecouple)].guiName = $"Decouple when Fairing gone: {(flag ? "Yes" : "No")}";
        }

        void OnVesselWasModified(Vessel ves) => HandleAutomaticDecoupling();

        void OnStageActivate (int stage)
        {
            HandleAutomaticDecoupling();
            if (part is Part && stage == part.inverseStage)
            {
                part.stackIcon.RemoveIcon();
                StageManager.Instance.SortIcons(true);
                Events[nameof(UIToggleTopNodeDecouple)].guiActive = false;
            }
        }

        public override void UpdateShape(bool pushAttachments)
        {
            base.UpdateShape(pushAttachments);

            float sth = CalcSideThickness();
            float baseRadius = (baseSize / 2) - sth;
            float scale = baseRadius * 2;

            part.mass = totalMass = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale + specificMass.w;

            massDisplay = PFUtils.formatMass (totalMass);
            costDisplay = PFUtils.formatCost (part.partInfo.cost + GetModuleCost (part.partInfo.cost, ModifierStagingSituation.CURRENT));

            part.breakingForce = specificBreakingForce * Mathf.Pow (baseRadius, 2);
            part.breakingTorque = specificBreakingTorque * Mathf.Pow (baseRadius, 2);

            if (part.FindModelTransform("model") is Transform model)
                model.localScale = Vector3.one * scale;
            else
                Debug.LogError($"[PF]: No 'model' transform found in part {part}!");

            part.rescaleFactor = scale;

            if (part.GetComponent<KzNodeNumberTweaker>() is KzNodeNumberTweaker nnt)
                nnt.SetRadius(baseSize / 2);

            if (part.GetComponent<ProceduralFairingBase>() is ProceduralFairingBase fbase)
            {
                fbase.baseSize = baseRadius * 2;
                fbase.sideThickness = sth;
                fbase.recalcShape();
            }

            PFUtils.updateDragCube(part, dragAreaScale);
        }

        public void ConfigureTechLimits()
        {
            if (PFUtils.canCheckTech())
            {
                float minSize = PFUtils.getTechMinValue("PROCFAIRINGS_MINDIAMETER", 0.25f);
                float maxSize = PFUtils.getTechMaxValue("PROCFAIRINGS_MAXDIAMETER", 30);

                PFUtils.setFieldRange(Fields[nameof(baseSize)], minSize, maxSize);
                PFUtils.setFieldRange(Fields[nameof(topSize)], minSize, maxSize);

                (Fields[nameof(baseSize)].uiControlEditor as UI_FloatEdit).incrementLarge = diameterStepLarge;
                (Fields[nameof(baseSize)].uiControlEditor as UI_FloatEdit).incrementSmall = diameterStepSmall;
                (Fields[nameof(topSize)].uiControlEditor as UI_FloatEdit).incrementLarge = diameterStepLarge;
                (Fields[nameof(topSize)].uiControlEditor as UI_FloatEdit).incrementSmall = diameterStepSmall;

                (Fields[nameof(height)].uiControlEditor as UI_FloatEdit).incrementLarge = heightStepLarge;
                (Fields[nameof(height)].uiControlEditor as UI_FloatEdit).incrementSmall = heightStepSmall;
                (Fields[nameof(extraHeight)].uiControlEditor as UI_FloatEdit).incrementLarge = heightStepLarge;
                (Fields[nameof(extraHeight)].uiControlEditor as UI_FloatEdit).incrementSmall = heightStepSmall;
            }
            else if (HighLogic.LoadedSceneIsEditor && ResearchAndDevelopment.Instance == null)
            {
                Debug.LogError($"[PF] ConfigureTechLimits() in Editor but R&D not ready!");
            }
        }

        public void HandleAutomaticDecoupling()
        {
            //  We used to remove the engine fairing (if there is any) from topmost node, but lately that's been causing NREs.  
            //  Since KSP gives us this option nativley, let's just use KSP to do that if we want.
            if (!HighLogic.LoadedSceneIsFlight) return;

            if (TopNodePartPresent && topNodeDecouplesWhenFairingsGone && FairingPresent)
            {
                if (part.FindModuleImplementing<ModuleDecouple>() is ModuleDecouple item)
                {
                    RemoveTopPartJoints();
                    item.Decouple();
                    part.stackIcon.RemoveIcon();
                    StageManager.Instance.SortIcons(true);
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
                        if (n.attachedPart is Part)
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
