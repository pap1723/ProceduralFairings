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

        public bool changed = true;

        abstract public float minHeight { get; }

        float lastBaseSize = -1000;
        float lastTopSize = -1000;
        float lastHeight = -1000;

        protected bool justLoaded;

        public virtual void checkTweakables ()
        {
            if (!baseSize.Equals (lastBaseSize))
            {
                lastBaseSize = baseSize;

                changed = true;
            }

            if (!topSize.Equals (lastTopSize))
            {
                lastTopSize = topSize;

                changed = true;
            }

            if (!height.Equals (lastHeight))
            {
                lastHeight = height;

                changed = true;
            }
        }

        public virtual void FixedUpdate ()
        {
            checkTweakables ();

            if (changed)
            {
                updateShape ();
            }

            justLoaded = false;
        }

        public virtual void updateShape ()
        {
            changed = false;

            float topheight = 0;
            float topnodeheight = 0;

            if (part.FindAttachNode("bottom") is AttachNode node1)
                node1.size = Mathf.RoundToInt(baseSize / diameterStepLarge);

            if (part.FindAttachNode("top") is AttachNode node2)
            {
                node2.size = Mathf.RoundToInt (baseSize / diameterStepLarge);
                topheight = node2.position.y;
            }

            if (part.FindAttachNode(topNodeName) is AttachNode node3)
            {
                node3.position = new Vector3 (0, height, 0);
                node3.size = Mathf.RoundToInt (topSize / diameterStepLarge);

                if (!justLoaded)
                    PFUtils.updateAttachedPartPos (node3, part);

                topnodeheight = height;
            }
            else
            {
                Debug.LogError($"[PF]: No '{topNodeName}' node in part {part}!");
            }

            if (part.FindAttachNodes("interstage") is AttachNode[] internodes)
            {
                var inc = (topnodeheight - topheight) / (internodes.Length / 2 + 1);

                for (int i = 0, j = 0; i < internodes.Length; i = i + 2)
                {
                    var height = topheight + (j + 1) * inc;

                    j++;

                    AttachNode node = internodes [i];

                    node.position.y = height;
                    node.size = node.size = Mathf.RoundToInt (topSize / diameterStepLarge) - 1;

                    if (!justLoaded)
                        PFUtils.updateAttachedPartPos (node, part);

                    node = internodes [i + 1];

                    node.position.y = height;
                    node.size = node.size = Mathf.RoundToInt (topSize / diameterStepLarge) - 1;

                    if (!justLoaded)
                        PFUtils.updateAttachedPartPos (node, part);
                }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart (state);

            if (state == StartState.None)
            {
                return;
            }

            StartCoroutine (FireFirstChanged ());

        }

        public IEnumerator<YieldInstruction> FireFirstChanged ()
        {
            while(!(part.editorStarted || part.started))
            {
                yield return new WaitForFixedUpdate ();
            }

            //  Wait a little more...

            yield return new WaitForSeconds (.01f);

            changed = true;
        }

        public override void OnLoad (ConfigNode cfg)
        {
            base.OnLoad (cfg);

            justLoaded = true;
            changed = true;
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

        public bool isTopNodePartPresent = true;
        public bool isFairingPresent = true;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Extra height", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0, maxValue = 50, incrementLarge = 1.0f, incrementSmall = 0.1f, incrementSlide = 0.001f)]
        public float extraHeight = 0;

        public bool engineFairingRemoved;

        [KSPField(guiActiveEditor = true, guiName = "Mass", groupName = PFUtils.PAWGroup)]
        public string massDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Cost", groupName = PFUtils.PAWGroup)]
        public string costDisplay;

        float lastExtraHt = -1000;

        [KSPEvent (name = "decNoFairings", active = true, guiActive = true, guiActiveEditor = true, guiActiveUnfocused = true, guiName = "text")]
        public void UIToggleTopNodeDecouple ()
        {
            topNodeDecouplesWhenFairingsGone = !topNodeDecouplesWhenFairingsGone;
            UpdateUIdecNoFairingsText (topNodeDecouplesWhenFairingsGone);
        }

        void UpdateUIdecNoFairingsText (bool flag)
        {
            Events[nameof(UIToggleTopNodeDecouple)].guiName = $"Decouple when Fairing gone: {(flag ? "Yes" : "No")}";
        }

        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defcost, ModifierStagingSituation sit) => (totalMass * costPerTonne) - defcost;
        public float GetModuleMass (float defmass, ModifierStagingSituation sit) => totalMass - defmass;
        public float CalcSideThickness() => Mathf.Min(sideThickness * Mathf.Max(baseSize, topSize), Mathf.Min(baseSize, topSize) * 0.25f);

        public float topRadius
        {
            get => (topSize / 2) - CalcSideThickness();
        }
        public override float minHeight
        {
            get => baseSize * 0.2f;
        }

        public override void checkTweakables ()
        {
            base.checkTweakables ();

            if (!extraHeight.Equals (lastExtraHt))
            {
                lastExtraHt = extraHeight;

                changed = true;
            }
        }

        void RemoveTopPartJoints ()
        {
            if (getTopPart() is Part topPart && getBottomPart() is Part bottomPart &&
                topPart.gameObject.GetComponents<ConfigurableJoint>() is ConfigurableJoint[] components)
            {
                foreach (ConfigurableJoint configurableJoint in components)
                {
                    if (configurableJoint.connectedBody == bottomPart.Rigidbody)
                        Destroy (configurableJoint);
                }
            }
        }

        public void Start ()
        {
            part.mass = totalMass;
        }

        public override void OnStart (StartState state)
        {
            base.OnStart (state);
            if (HighLogic.LoadedSceneIsEditor)
                ConfigureTechLimits();

            part.mass = totalMass;

            isFairingPresent = CheckForFairingPresent ();

            isTopNodePartPresent = getTopPart() is Part;

            UpdateUIdecNoFairingsText (topNodeDecouplesWhenFairingsGone);

            GameEvents.onEditorShipModified.Add(OnEditorShipModified);
            GameEvents.onVesselWasModified.Add(OnVesselWasModified);
            GameEvents.onVesselCreate.Add(OnVesselCreate);
            GameEvents.onVesselGoOffRails.Add(OnVesselGoOffRails);
            GameEvents.onVesselLoaded.Add(OnVesselLoaded);
            GameEvents.onStageActivate.Add(OnStageActivate);
        }

        public void OnDestroy ()
        {
            GameEvents.onEditorShipModified.Remove(OnEditorShipModified);
            GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
            GameEvents.onVesselGoOffRails.Remove(OnVesselGoOffRails);
            GameEvents.onVesselLoaded.Remove(OnVesselLoaded);
            GameEvents.onStageActivate.Remove(OnStageActivate);
        }

        bool isShipModified = true;
        bool isStaged;

        int stageNum;

        //  Lets catch some events...

        void OnEditorShipModified (ShipConstruct sc)
        {
            isShipModified = true;
        }

        void OnVesselWasModified (Vessel ves)
        {
            isShipModified = true;
        }

        void OnVesselCreate (Vessel ves)
        {
            isShipModified = true;
        }

        void OnVesselGoOffRails (Vessel ves)
        {
            isShipModified = true;
        }

        void OnVesselLoaded (Vessel ves)
        {
            isShipModified = true;
        }

        void OnStageActivate (int stage)
        {
            isStaged = true;

            stageNum = stage;
        }

        public float totalMass;

        public override void updateShape ()
        {
            base.updateShape ();

            float sth = CalcSideThickness();

            float br = baseSize * 0.5f - sth;
            float scale = br * 2;

            part.mass = totalMass = ((specificMass.x * scale + specificMass.y) * scale + specificMass.z) * scale + specificMass.w;

            massDisplay = PFUtils.formatMass (totalMass);
            costDisplay = PFUtils.formatCost (part.partInfo.cost + GetModuleCost (part.partInfo.cost, ModifierStagingSituation.CURRENT));

            part.breakingForce = specificBreakingForce * Mathf.Pow (br, 2);
            part.breakingTorque = specificBreakingTorque * Mathf.Pow (br, 2);

            if (part.FindModelTransform("model") is Transform model)
                model.localScale = Vector3.one * scale;
            else
                Debug.LogError($"[PF]: No 'model' transform found in part {part}!");

            part.rescaleFactor = scale;

            var node = part.FindAttachNode ("top");
            node.position = node.originalPosition * scale;

            if (!justLoaded)
                PFUtils.updateAttachedPartPos (node, part);

            var topNode = part.FindAttachNode("top");
            var bottomNode = part.FindAttachNode("bottom");

            float y = (topNode.position.y + bottomNode.position.y) * 0.5f;
            int sideNodeSize = Math.Max(0, Mathf.RoundToInt(scale / diameterStepLarge) - 1);

            if (part.FindAttachNodes("connect") is AttachNode[] nodes)
            {
                foreach (AttachNode n in nodes)
                {
                    n.position.y = y;
                    n.size = sideNodeSize;

                    if (!justLoaded)
                        PFUtils.updateAttachedPartPos (n, part);
                }
            }

            if (part.FindAttachNodes("interstage") is AttachNode[] internodes && part.FindAttachNode(topNodeName) is AttachNode topnode2)
            {
                var topheight = topNode.position.y;
                var topnode2height = topnode2.position.y;

                var inc = (topnode2height - topheight) / (internodes.Length / 2 + 1);

                for (int i = 0, j = 0; i < internodes.Length; i = i + 2)
                {
                    var baseHeight = topheight + (j + 1) * inc;

                    j++;

                    node = internodes [i];

                    node.position.y = baseHeight;
                    node.size = topNode.size;

                    if (!justLoaded)
                        PFUtils.updateAttachedPartPos (node, part);

                    node = internodes [i + 1];

                    node.position.y = baseHeight;
                    node.size = sideNodeSize;

                    if (!justLoaded)
                        PFUtils.updateAttachedPartPos (node, part);
                }
            }

            if (part.GetComponent<KzNodeNumberTweaker>() is KzNodeNumberTweaker nnt)
                nnt.radius = baseSize / 2;

            if (part.GetComponent<ProceduralFairingBase>() is ProceduralFairingBase fbase)
            {
                fbase.baseSize = br * 2;
                fbase.sideThickness = sth;
                fbase.needShapeUpdate = true;
            }

            StartCoroutine (PFUtils.updateDragCubeCoroutine (part, dragAreaScale));
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

        public override void FixedUpdate ()
        {
            base.FixedUpdate ();

            if (isShipModified)
            {
                isShipModified = false;

				//  We used to remove the engine fairing (if there is any) from topmost node, but lately that's been causing NREs.  
				//  Since KSP gives us this option nativley, let's just use KSP to do that if we want.

                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (isTopNodePartPresent)
                    {
                        if (getTopPart() is Part tp)
                        {
                            if (topNodeDecouplesWhenFairingsGone && !CheckForFairingPresent())
                            {
                                if (part.FindModuleImplementing<ModuleDecouple>() is ModuleDecouple item)
                                {
                                    RemoveTopPartJoints();
                                    item.Decouple();
                                    part.stackIcon.RemoveIcon();
                                    StageManager.Instance.SortIcons(true);
                                    isFairingPresent = false;
                                    isTopNodePartPresent = false;
                                    Events[nameof(UIToggleTopNodeDecouple)].guiActive = false;
                                }
                                else
                                {
                                    Debug.LogError($"[PF]: Cannot decouple from top part! {this}");
                                }
                            }
                        }
                        else
                        {
                            isTopNodePartPresent = false;
                            Events[nameof(UIToggleTopNodeDecouple)].guiActive = false;
                        }
                    }

                    if (isStaged)
                    {
                        isStaged = false;

                        if (part is Part && stageNum == part.inverseStage)
                        {
                            part.stackIcon.RemoveIcon ();
                            StageManager.Instance.SortIcons (true);
                            Events[nameof(UIToggleTopNodeDecouple)].guiActive = false;
                        }
                    }
                }
            }
        }

        public Part getBottomPart() => (part.FindAttachNode("bottom") is AttachNode node) ? node.attachedPart : null;
        public Part getTopPart() => (part.FindAttachNode(topNodeName) is AttachNode node) ? node.attachedPart : null;

        public bool CheckForFairingPresent()
        {
            if (isFairingPresent && part.FindAttachNodes("connect") is AttachNode[] nodes)
            {
                foreach (AttachNode n in nodes)
                {
                    if (n.attachedPart is Part)
                        return true;
                }
            }
            return false;
        }

        public override void OnLoad (ConfigNode cfg)
        {
            base.OnLoad (cfg);

            if (cfg.HasValue ("baseRadius") && cfg.HasValue ("topRadius"))
            {
                //  Load legacy settings.

                float br = float.Parse (cfg.GetValue ("baseRadius"));
                float tr = float.Parse (cfg.GetValue ("topRadius"));

                baseSize = (br + sideThickness * br) * 2;
                topSize = (tr + sideThickness * br) * 2;

                sideThickness *= 1.15f / 1.25f;
            }
        }
    }
}
