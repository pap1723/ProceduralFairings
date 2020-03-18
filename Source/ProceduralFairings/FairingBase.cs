//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keramzit
{
    public class ProceduralFairingBase : PartModule
    {
        public const float MaxCylinderDimension = 50;

        [KSPField] public float outlineWidth = 0.05f;
        [KSPField] public int outlineSlices = 12;
        [KSPField] public Vector4 outlineColor = new Vector4(0, 0, 0.2f, 1);
        [KSPField] public float verticalStep = 0.1f;
        [KSPField] public float baseSize = 1.25f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Extra radius", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatRange(minValue = -1, maxValue = 2, stepIncrement = 0.01f)]
        public float extraRadius;

        [KSPField] public int circleSegments = 24;
        [KSPField] public float sideThickness = 0.05f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Fairing Auto-struts", groupName = PFUtils.PAWGroup)]
        [UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool autoStrutSides = true;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Fairing Auto-shape", groupName = PFUtils.PAWGroup)]
        [UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool autoShape = true;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Max. size", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 5, incrementLarge = 1.25f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        public float manualMaxSize = 0.625f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Cyl. start", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0, maxValue = MaxCylinderDimension, incrementLarge = 1.0f, incrementSmall = 0.1f, incrementSlide = 0.001f)]
        public float manualCylStart = 0;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Cyl. end", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0, maxValue = MaxCylinderDimension, incrementLarge = 1.0f, incrementSmall = 0.1f, incrementSlide = 0.001f)]
        public float manualCylEnd = 1;

        [KSPField] public float diameterStepLarge = 1.25f;
        [KSPField] public float diameterStepSmall = 0.125f;

        [KSPField] public float heightStepLarge = 1.0f;
        [KSPField] public float heightStepSmall = 0.1f;

        public bool needShapeUpdate = true;
        LineRenderer line;
        readonly List<LineRenderer> outline = new List<LineRenderer>();
        readonly List<ConfigurableJoint> joints = new List<ConfigurableJoint>();

        public override string GetInfo() => "Attach side fairings and they will be shaped for your attached payload.\nRemember to enable the decoupler if you need one.";

        public override void OnStart (StartState state)
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;

            PFUtils.hideDragStuff(part);
            if (HighLogic.LoadedSceneIsEditor)
            {
                ConfigureTechLimits();
                if (line)
                    line.transform.Rotate (0, 90, 0);

                DestroyAllLineRenderers ();
                DestroyOutline();
                InitializeFairingOutline(outlineSlices, outlineColor, outlineWidth);

                SetUIChangedCallBacks();
                SetUIFieldVisibility();
                SetUIFieldLimits();
                GameEvents.onPartAttach.Add(OnPartAttach);
                GameEvents.onPartRemove.Add(OnPartRemove);
                StartCoroutine(EditorChangeDetector());
            }
            else
            {
                GameEvents.onVesselWasModified.Add(OnVesselModified);
            }
        }

        public override void OnStartFinished(StartState state) 
        {
            base.OnStartFinished(state);
            PFUtils.updateDragCube(part, 1);
        }

        public void OnDestroy()
        {
            GameEvents.onPartAttach.Remove(OnPartAttach);
            GameEvents.onPartRemove.Remove(OnPartRemove);
            GameEvents.onVesselWasModified.Remove(OnVesselModified);

            if (line)
            {
                Destroy(line.gameObject);
                line = null;
            }
            DestroyAllLineRenderers();
            DestroyOutline();
        }

        private System.Collections.IEnumerator EditorChangeDetector()
        {
            while (HighLogic.LoadedSceneIsEditor)
            {
                yield return new WaitForFixedUpdate();
                if (needShapeUpdate) recalcShape();
                needShapeUpdate = false;
            }
        }

        private System.Collections.IEnumerator DisplayFairingOutline()
        {
            yield return new WaitForFixedUpdate();
            SetFairingOutlineEnabled(!HasTopOrSideNode());
        }

        void SetUIChangedCallBacks()
        {
            Fields[nameof(autoShape)].uiControlEditor.onFieldChanged += OnChangeAutoshapeUI;
            Fields[nameof(autoShape)].uiControlEditor.onSymmetryFieldChanged += OnChangeAutoshapeUI;
            Fields[nameof(extraRadius)].uiControlEditor.onFieldChanged += OnChangeShapeUI;
            Fields[nameof(extraRadius)].uiControlEditor.onSymmetryFieldChanged += OnChangeShapeUI;
            Fields[nameof(manualMaxSize)].uiControlEditor.onFieldChanged += OnChangeShapeUI;
            Fields[nameof(manualMaxSize)].uiControlEditor.onSymmetryFieldChanged += OnChangeShapeUI;
            Fields[nameof(manualCylStart)].uiControlEditor.onFieldChanged += OnChangeShapeUI;
            Fields[nameof(manualCylStart)].uiControlEditor.onSymmetryFieldChanged += OnChangeShapeUI;
            Fields[nameof(manualCylEnd)].uiControlEditor.onFieldChanged += OnChangeShapeUI;
            Fields[nameof(manualCylEnd)].uiControlEditor.onSymmetryFieldChanged += OnChangeShapeUI;
        }

        void SetUIFieldVisibility()
        {
            Fields[nameof(manualMaxSize)].guiActiveEditor = !autoShape;
            Fields[nameof(manualCylStart)].guiActiveEditor = !autoShape;
            Fields[nameof(manualCylEnd)].guiActiveEditor = !autoShape;
        }

        void OnChangeAutoshapeUI(BaseField bf, object obj)
        {
            SetUIFieldVisibility();
            recalcShape();
        }

        void OnChangeShapeUI(BaseField bf, object obj)
        {
            SetUIFieldLimits();
            recalcShape();
        }

        private void SetUIFieldLimits()
        {
            UI_FloatEdit start = Fields[nameof(manualCylStart)].uiControlEditor as UI_FloatEdit;
            UI_FloatEdit end = Fields[nameof(manualCylEnd)].uiControlEditor as UI_FloatEdit;
            start.maxValue = Mathf.Min(manualCylEnd, MaxCylinderDimension - 0.1f);
            end.minValue = Mathf.Min(manualCylStart, MaxCylinderDimension - 0.1f);
            bool refresh = manualCylStart > start.maxValue || manualCylEnd < end.minValue;
            manualCylStart = Mathf.Min(manualCylStart, start.maxValue);
            manualCylEnd = Mathf.Max(manualCylEnd, end.minValue);
            if (refresh) 
                MonoUtilities.RefreshPartContextWindow(part);
        }

        public void OnPartPack() => removeJoints();

        public void onShieldingDisabled(List<Part> shieldedParts) => removeJoints();

        public void onShieldingEnabled(List<Part> shieldedParts)
        {
            if (HighLogic.LoadedSceneIsFlight && autoStrutSides)
                StartCoroutine(createAutoStruts(shieldedParts));
        }

        void OnPartAttach(GameEvents.HostTargetAction<Part, Part> action)
        {
            // Resizer.cs handles our scale factor/mesh.
            // Shape Update only handles the fairing side shape.
            // On loading any craft, the sideFairing knows its shape already.
            // Thus only need to do this when our attachment state will change.
            needShapeUpdate = HighLogic.LoadedSceneIsEditor;
        }

        void OnPartRemove(GameEvents.HostTargetAction<Part, Part> action)
        {
            StartCoroutine(DisplayFairingOutline());
        }

        void OnVesselModified(Vessel v)
        {
            if (vessel == v && !part.packed && 
                part.GetComponent<ProceduralFairingAdapter>() is ProceduralFairingAdapter adapter)
            {
                if (adapter.getTopPart() == null)
                    removeJoints();
            }
        }

        public void ConfigureTechLimits()
        {
            if (PFUtils.canCheckTech())
            {
                float minSize = PFUtils.getTechMinValue("PROCFAIRINGS_MINDIAMETER", 0.25f);
                float maxSize = PFUtils.getTechMaxValue("PROCFAIRINGS_MAXDIAMETER", 30);

                PFUtils.setFieldRange(Fields[nameof(manualMaxSize)], minSize, maxSize * 2);

                (Fields[nameof(manualMaxSize)].uiControlEditor as UI_FloatEdit).incrementLarge = diameterStepLarge;
                (Fields[nameof(manualMaxSize)].uiControlEditor as UI_FloatEdit).incrementSmall = diameterStepSmall;

                (Fields[nameof(manualCylStart)].uiControlEditor as UI_FloatEdit).incrementLarge = heightStepLarge;
                (Fields[nameof(manualCylStart)].uiControlEditor as UI_FloatEdit).incrementSmall = heightStepSmall;
                (Fields[nameof(manualCylEnd)].uiControlEditor as UI_FloatEdit).incrementLarge = heightStepLarge;
                (Fields[nameof(manualCylEnd)].uiControlEditor as UI_FloatEdit).incrementSmall = heightStepSmall;
            }
            else if (HighLogic.LoadedSceneIsEditor && ResearchAndDevelopment.Instance == null)
            {
                Debug.LogError($"[PF] ConfigureTechLimits() in Editor but R&D not ready!");
            }
        }

        private Part FindTopBasePart()
        {
            Part top = null;
            if (part.GetComponent<ProceduralFairingAdapter>() is ProceduralFairingAdapter adapter)
            {
                top = adapter.getTopPart();
            }
            else
            {
                var scan = scanPayload();
                if (scan.targets.Count > 0)
                    top = scan.targets[0];
            }
            return top;
        }

        #region Struts and Joints

        public void removeJoints()
        {
            foreach (ConfigurableJoint joint in joints)
                Destroy(joint);
            joints.Clear();
        }

        IEnumerator<YieldInstruction> createAutoStruts(List<Part> shieldedParts)
        {
            while (!FlightGlobals.ready || vessel.packed || !vessel.loaded)
            {
                yield return new WaitForFixedUpdate();
            }
            if (part.GetComponent<KzNodeNumberTweaker>() is KzNodeNumberTweaker nnt &&
                part.FindAttachNodes("connect") is AttachNode[] attached)
            {
                Part topBasePart = FindTopBasePart();
                for (int i = 0; i < nnt.numNodes; ++i)
                {
                    if (attached[i].attachedPart is Part p && p.Rigidbody &&
                        attached[i > 0 ? i - 1 : nnt.numNodes - 1].attachedPart is Part pp)
                    {
                        addStrut(p, pp);
                        if (topBasePart != null)
                            addStrut(p, topBasePart);
                    }
                }
            }
        }

        ConfigurableJoint addStrut(Part p, Part pp)
        {
            if (p && p != pp && p.Rigidbody != pp.Rigidbody && pp.Rigidbody is Rigidbody rb &&
                p.gameObject.AddComponent<ConfigurableJoint>() is ConfigurableJoint joint)
            {
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = ConfigurableJointMotion.Locked;
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Locked;
                joint.projectionDistance = 0.1f;
                joint.projectionAngle = 5;
                joint.breakForce = p.breakingForce;
                joint.breakTorque = p.breakingTorque;
                joint.connectedBody = rb;

                joints.Add(joint);
                return joint;
            }
            return null;
        }

        #endregion

        #region LineRenderers

        LineRenderer MakeLineRenderer(string gameObjectName, Color color, float wd)
        {
            var o = new GameObject (gameObjectName);

            o.transform.parent = part.transform;
            o.transform.localPosition = Vector3.zero;
            o.transform.localRotation = Quaternion.identity;

            var lineRenderer = o.AddComponent<LineRenderer>();

            lineRenderer.useWorldSpace = false;
            lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = wd;
            lineRenderer.endWidth = wd;
            lineRenderer.positionCount = 0;

            return lineRenderer;
        }

        void DestroyOutline()
        {
            foreach (LineRenderer r in outline)
                Destroy(r.gameObject);
            outline.Clear();
        }

        /// <summary>
        /// Fix for the blue ghost lines showing invalid outlines when cloning or symmetry-placing fairing bases in the editor.
        /// Find any already assigned (copied) LineRenderers and delete them.
        /// </summary>

        void DestroyAllLineRenderers()
        {
            foreach (LineRenderer r in FindObjectsOfType<LineRenderer>())
            {
                if (r?.transform?.parent?.gameObject is GameObject go &&
                    (go == this || go == this.gameObject))
                {
                    Destroy(r.gameObject);
                }
            }
        }

        #endregion

        #region Fairing Shapes
        private void InitializeFairingOutline(int slices, Vector4 color, float width)
        {
            for (int i = 0; i < slices; ++i)
            {
                var r = MakeLineRenderer("fairing outline", color, width);
                outline.Add(r);
                r.transform.Rotate(0, i * 360f / slices, 0);
            }
        }

        static public Vector3 [] buildFairingShape (float baseRad, float maxRad, float cylStart, float cylEnd, float noseHeightRatio, Vector4 baseConeShape, Vector4 noseConeShape, int baseConeSegments, int noseConeSegments, Vector4 vertMapping, float mappingScaleY)
        {
            float baseConeRad = maxRad - baseRad;
            float tip = maxRad * noseHeightRatio;

            var baseSlope = new BezierSlope (baseConeShape);
            var noseSlope = new BezierSlope (noseConeShape);

            float baseV0 = vertMapping.x / mappingScaleY;
            float baseV1 = vertMapping.y / mappingScaleY;
            float noseV0 = vertMapping.z / mappingScaleY;
            float noseV1 = vertMapping.w / mappingScaleY;

            var shape = new Vector3 [1 + (cylStart.Equals (0) ? 0 : baseConeSegments) + 1 + noseConeSegments];

            int vi = 0;

            if (!cylStart.Equals (0))
            {
                for (int i = 0; i <= baseConeSegments; ++i, ++vi)
                {
                    float t = (float) i / baseConeSegments;

                    var p = baseSlope.interp (t);

                    shape [vi] = new Vector3 (p.x * baseConeRad + baseRad, p.y * cylStart, Mathf.Lerp (baseV0, baseV1, t));
                }
            }
            else
            {
                shape [vi++] = new Vector3 (baseRad, 0, baseV1);
            }

            for (int i = 0; i <= noseConeSegments; ++i, ++vi)
            {
                float t = (float) i / noseConeSegments;

                var p = noseSlope.interp (1 - t);

                shape [vi] = new Vector3 (p.x * maxRad, (1 - p.y) * tip + cylEnd, Mathf.Lerp (noseV0, noseV1, t));
            }

            return shape;
        }

        static public Vector3 [] buildInlineFairingShape (float baseRad, float maxRad, float topRad, float cylStart, float cylEnd, float top, Vector4 baseConeShape, int baseConeSegments, Vector4 vertMapping, float mappingScaleY)
        {
            float baseConeRad = maxRad - baseRad;
            float topConeRad = maxRad - topRad;

            var baseSlope = new BezierSlope (baseConeShape);

            float baseV0 = vertMapping.x / mappingScaleY;
            float baseV1 = vertMapping.y / mappingScaleY;
            float noseV0 = vertMapping.z / mappingScaleY;

            var shape = new Vector3 [2 + (cylStart.Equals (0) ? 0 : baseConeSegments + 1) + (cylEnd.Equals (top) ? 0 : baseConeSegments + 1)];

            int vi = 0;

            if (!cylStart.Equals(0))
            {
                for (int i = 0; i <= baseConeSegments; ++i, ++vi)
                {
                    float t = (float) i / baseConeSegments;

                    var p = baseSlope.interp (t);

                    shape [vi] = new Vector3 (p.x * baseConeRad + baseRad, p.y * cylStart, Mathf.Lerp (baseV0, baseV1, t));
                }
            }

            shape [vi++] = new Vector3 (maxRad, cylStart, baseV1);
            shape [vi++] = new Vector3 (maxRad, cylEnd, noseV0);

            if (!cylEnd.Equals (top))
            {
                for (int i = 0; i <= baseConeSegments; ++i, ++vi)
                {
                    float t = (float) i / baseConeSegments;

                    var p = baseSlope.interp (1 - t);

                    shape [vi] = new Vector3 (p.x * topConeRad + topRad, Mathf.Lerp (top, cylEnd, p.y), Mathf.Lerp (baseV1, baseV0, t));
                }
            }

            return shape;
        }

        #endregion

        PayloadScan scanPayload ()
        {
            //  Scan the payload and build it's profile.
            var scan = new PayloadScan (part, verticalStep, extraRadius);

            if (part.FindAttachNode("top") is AttachNode node)
            {
                scan.ofs = node.position.y;
                if (node.attachedPart != null)
                    scan.addPart (node.attachedPart, part);
            }

            if (part.FindAttachNodes("interstage") is AttachNode[] nodes)
            {
                foreach (AttachNode n in nodes)
                {
                    if (n.attachedPart != null)
                        scan.addPart (n.attachedPart, part);
                }
            }

            for (int i = 0; i < scan.payload.Count; ++i)
            {
                var cp = scan.payload [i];

                //  Add any connected payload parts.
                scan.addPart (cp.parent, cp);
                foreach (Part child in cp.children)
                {
                    scan.addPart(child, cp);
                }

                //  Scan for the part colliders.
                foreach (Collider coll in cp.FindModelComponents<Collider>())
                {
                    //  Skip ladders etc...
                    if (coll.tag.Equals("Untagged"))
                        scan.addPayload(coll);
                }
            }

            return scan;
        }

        AttachNode HasNodeComponent<type>(AttachNode[] nodes)
        {
            if (nodes != null)
            {
                foreach (AttachNode node in nodes)
                {
                    if (node.attachedPart is Part p && p.GetComponent<type>() is type)
                        return node;
                }
            }
            return null;
        }

        private void SetFairingOutlineEnabled(bool enabled)
        {
            foreach (LineRenderer lr in outline)
                lr.enabled = enabled;
        }

        private void BuildFairingOutline(Vector3[] shape)
        {
            foreach (LineRenderer lr in outline)
            {
                lr.positionCount = shape.Length;
                for (int i = 0; i < shape.Length; ++i)
                {
                    lr.SetPosition(i, new Vector3(shape[i].x, shape[i].y));
                }
            }
        }

        private bool HasTopOrSideNode()
        {
            var adapter = part.GetComponent<ProceduralFairingAdapter>();
            if (!adapter && scanPayload() is PayloadScan scan && scan.targets.Count > 0)
                return true;
            if (HasNodeComponent<ProceduralFairingSide>(part.FindAttachNodes("connect")) is AttachNode sideNode)
                return true;
            return false;
        }

        private void FillProfileOutline(PayloadScan scan)
        {
            if (line is LineRenderer)
            {
                line.positionCount = scan.profile.Count * 2 + 2;
                float prevRad = 0;
                int hi = 0;
                for (int i = 0; i < scan.profile.Count; i++)
                {
                    var r = scan.profile[i];

                    line.SetPosition(hi * 2, new Vector3(prevRad, hi * verticalStep + scan.ofs, 0));
                    line.SetPosition(hi * 2 + 1, new Vector3(r, hi * verticalStep + scan.ofs, 0));

                    hi++; prevRad = r;
                }

                line.SetPosition(hi * 2, new Vector3(prevRad, hi * verticalStep + scan.ofs, 0));
                line.SetPosition(hi * 2 + 1, new Vector3(0, hi * verticalStep + scan.ofs, 0));
            }
        }

        public void recalcShape ()
        {
            var scan = scanPayload ();

            //  Check for reversed bases (inline fairings).

            float topY = 0;
            float topRad = 0;

            AttachNode topSideNode = null;
            bool isInline = false;

            if (part.GetComponent<ProceduralFairingAdapter>() is ProceduralFairingAdapter adapter)
            {
                isInline = true;
                topY = Mathf.Max(scan.ofs, adapter.height + adapter.extraHeight);
                topRad = adapter.topRadius;
            }
            else if (scan.targets.Count > 0)
            {
                isInline = true;
                var topBase = scan.targets [0].GetComponent<ProceduralFairingBase>();
                topY = Mathf.Max(scan.ofs, scan.w2l.MultiplyPoint3x4(topBase.part.transform.position).y);
                topSideNode = HasNodeComponent<ProceduralFairingSide>(topBase.part.FindAttachNodes ("connect"));
                topRad = topBase.baseSize * 0.5f;
            }

            //  No payload case.

            if (scan.profile.Count <= 0)
            {
                scan.profile.Add (extraRadius);
            }

            //  Fill profile outline (for debugging).
            FillProfileOutline(scan);

            //  Check for attached side parts.
            var attached = part.FindAttachNodes ("connect");
            var sideNode = HasNodeComponent<ProceduralFairingSide>(attached);

            //  Get the number of available fairing attachment nodes from NodeNumberTweaker.
            var nnt = part.GetComponent<KzNodeNumberTweaker>();
            int numSideParts = nnt.numNodes;

            ProceduralFairingSide sf = sideNode?.attachedPart.GetComponent<ProceduralFairingSide>();

            var baseConeShape = sf ? sf.baseConeShape : new Vector4 (0, 0, 0, 0);
            var noseConeShape = sf ? sf.noseConeShape : new Vector4 (0, 0, 0, 0);
            var mappingScale = sf ? sf.mappingScale : new Vector2 (1024, 1024);
            var stripMapping = sf ? sf.stripMapping : new Vector2 (992, 1024);
            var horMapping = sf ? sf.horMapping : new Vector4 (0, 480, 512, 992);
            var vertMapping = sf ? sf.vertMapping : new Vector4 (0, 160, 704, 1024);

            float baseCurveStartX = sf ? sf.baseCurveStartX : 0;
            float baseCurveStartY = sf ? sf.baseCurveStartY : 0;
            float baseCurveEndX = sf ? sf.baseCurveEndX : 0;
            float baseCurveEndY = sf ? sf.baseCurveEndY : 0;
            int baseConeSegments = sf ? Convert.ToInt32(sf.baseConeSegments) : 1;

            float noseCurveStartX = sf ? sf.noseCurveStartX : 0;
            float noseCurveStartY = sf ? sf.noseCurveStartY : 0;
            float noseCurveEndX = sf ? sf.noseCurveEndX : 0;
            float noseCurveEndY = sf ? sf.noseCurveEndY : 0;
            int noseConeSegments = sf ? Convert.ToInt32(sf.noseConeSegments) : 1 ;
            float noseHeightRatio = sf ? sf.noseHeightRatio : 1;
            float minBaseConeAngle = sf ? sf.minBaseConeAngle : 20;
            float density = sf ? sf.density : 0;

            //   Compute the fairing shape.

            float baseRad = baseSize * 0.5f;
            float minBaseConeTan = Mathf.Tan (minBaseConeAngle * Mathf.Deg2Rad);

            float cylStart = 0;
            float maxRad;

            int profTop = scan.profile.Count;

            if (isInline)
            {
                profTop = Mathf.CeilToInt ((topY - scan.ofs) / verticalStep);
                profTop = Math.Min(profTop, scan.profile.Count);

                maxRad = 0;
                for (int i = 0; i < profTop; ++i)
                {
                    maxRad = Mathf.Max (maxRad, scan.profile [i]);
                }

                maxRad = Mathf.Max (maxRad, topRad);
            }
            else
            {
                maxRad = PFUtils.GetMaxValueFromList (scan.profile);
            }

            if (maxRad > baseRad)
            {
                //  Try to fit the base cone as high as possible.

                cylStart = scan.ofs;

                for (int i = 1; i < scan.profile.Count; ++i)
                {
                    float y = i * verticalStep + scan.ofs;
                    float r0 = baseRad;
                    float k = (maxRad - r0) / y;

                    if (k < minBaseConeTan)
                    {
                        break;
                    }

                    bool ok = true;

                    float r = r0 + k * scan.ofs;

                    for (int j = 0; j < i; ++j, r += k * verticalStep)
                    {
                        if (scan.profile [j] > r)
                        {
                            ok = false;

                            break;
                        }
                    }

                    if (!ok)
                    {
                        break;
                    }

                    cylStart = y;
                }
            }
            else
            {
                //  No base cone, just a cylinder and a nose.

                maxRad = baseRad;
            }

            float cylEnd = scan.profile.Count * verticalStep + scan.ofs;

            if (isInline)
            {
                float r0 = topRad;

                if (profTop > 0 && profTop < scan.profile.Count)
                {
                    r0 = Mathf.Max (r0, scan.profile [profTop - 1]);

                    if (profTop - 2 >= 0) r0 = Mathf.Max (r0, scan.profile [profTop - 2]);
                }

                if (maxRad > r0)
                {
                    if (cylEnd > topY)
                    {
                        cylEnd = topY - verticalStep;
                    }

                    //  Try to fit the top cone as low as possible.

                    for (int i = profTop - 1; i >= 0; --i)
                    {
                        float y = i * verticalStep + scan.ofs;
                        float k = (maxRad - r0) / (y - topY);

                        bool ok = true;

                        float r = maxRad + k * verticalStep;

                        for (int j = i; j < profTop; ++j, r += k * verticalStep)
                        {
                            if (r < r0)
                            {
                                r = r0;
                            }

                            if (scan.profile [j] > r)
                            {
                                ok = false;

                                break;
                            }
                        }

                        if (!ok)
                        {
                            break;
                        }

                        cylEnd = y;
                    }
                }
                else
                {
                    cylEnd = topY;
                }
            }
            else
            {
                //  Try to fit the nose cone as low as possible.

                for (int i = scan.profile.Count - 1; i >= 0; --i)
                {
                    float s = verticalStep / noseHeightRatio;

                    bool ok = true;

                    float r = maxRad - s;

                    for (int j = i; j < scan.profile.Count; ++j, r -= s)
                    {
                        if (scan.profile [j] > r)
                        {
                            ok = false;

                            break;
                        }
                    }

                    if (!ok) break;

                    float y = i * verticalStep + scan.ofs;

                    cylEnd = y;
                }
            }

            if (autoShape)
            {
                manualMaxSize = maxRad * 2;
                manualCylStart = cylStart;
                manualCylEnd = cylEnd;
            }
            else
            {
                maxRad = manualMaxSize * 0.5f;
                cylStart = manualCylStart;
                cylEnd = manualCylEnd;
            }
            cylStart = Math.Min(cylStart, cylEnd);

            //  Build the fairing shape line.

            Vector3[] shape = isInline ? buildInlineFairingShape(baseRad, maxRad, topRad, cylStart, cylEnd, topY, baseConeShape, baseConeSegments, vertMapping, mappingScale.y) :
                                        buildFairingShape(baseRad, maxRad, cylStart, cylEnd, noseHeightRatio, baseConeShape, noseConeShape, baseConeSegments, noseConeSegments, vertMapping, mappingScale.y);

            BuildFairingOutline(shape);
            SetFairingOutlineEnabled(sideNode == null && topSideNode == null);

            //  Rebuild the side parts.

            int numSegs = Math.Max(2, circleSegments / numSideParts);

            foreach (AttachNode sn in attached)
            {
                if (sn.attachedPart is Part sp &&
                    sp.GetComponent<ProceduralFairingSide>() is ProceduralFairingSide sf2 &&
                    !sf2.shapeLock &&
                    sp.FindModelComponent<MeshFilter>("model") is MeshFilter mf)
                {
                    var nodePos = sn.position;

                    mf.transform.position = part.transform.position;
                    mf.transform.rotation = part.transform.rotation;

                    float ra = Mathf.Atan2(-nodePos.z, nodePos.x) * Mathf.Rad2Deg;

                    mf.transform.Rotate(0, ra, 0);

                    sf2.meshPos = mf.transform.localPosition;
                    sf2.meshRot = mf.transform.localRotation;
                    sf2.numSegs = numSegs;
                    sf2.numSideParts = numSideParts;
                    sf2.baseRad = baseRad;
                    sf2.maxRad = maxRad;
                    sf2.cylStart = cylStart;
                    sf2.cylEnd = cylEnd;
                    sf2.topRad = topRad;
                    sf2.inlineHeight = topY;
                    sf2.sideThickness = sideThickness;
                    sf2.baseCurveStartX = baseCurveStartX;
                    sf2.baseCurveStartY = baseCurveStartY;
                    sf2.baseCurveEndX = baseCurveEndX;
                    sf2.baseCurveEndY = baseCurveEndY;
                    sf2.baseConeSegments = baseConeSegments;
                    sf2.noseCurveStartX = noseCurveStartX;
                    sf2.noseCurveStartY = noseCurveStartY;
                    sf2.noseCurveEndX = noseCurveEndX;
                    sf2.noseCurveEndY = noseCurveEndY;
                    sf2.noseConeSegments = noseConeSegments;
                    sf2.noseHeightRatio = noseHeightRatio;
                    sf2.density = density;

                    sf2.rebuildMesh();
                }
            }

            if (part.GetComponent<KzFairingBaseShielding>() is KzFairingBaseShielding shielding)
                shielding.reset ();
        }
    }
}
