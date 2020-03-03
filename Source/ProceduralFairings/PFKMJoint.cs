//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Keramzit
{
    public class PFKMJoints : PartModule
    {
        [KSPField (isPersistant = true)]
        public float breakingForce = 500000f;

        [KSPField(guiActive = true, guiName = "View Joints", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool viewJoints;

        const float w1 = 0.05f;
        const float w2 = 0.15f;

        readonly List<LineRenderer> jointLines = new List<LineRenderer>();
        bool morejointsadded;
        Part bottomNodePart;
        readonly List<Part> nodeParts = new List<Part>();

        void AddMoreJoints ()
        {
            int i;

            AttachNode attachNode;

            if (!morejointsadded)
            {
                AttachNode attachNode1 = base.part.FindAttachNode ("bottom");

                AttachNode[] attachNodeArray = base.part.FindAttachNodes("interstage");
                AttachNode[] attachNodeArray1 = base.part.FindAttachNodes("top");

                Debug.Log($"[PF]: Adding Joints to Vessel: {vessel.vesselName}, (Launch ID: {base.part.launchID}) (GUID: {base.vessel.id})");
                Debug.Log($"[PF]: For PF Part: {base.part.name} ({base.part.craftID})");

                Part part = null;

                if ((attachNode1 == null ? false : attachNode1.attachedPart != null))
                {
                    part = attachNode1.attachedPart;

                    bottomNodePart = part;

                    addStrut (part, base.part, Vector3.zero);
                    Debug.Log($"[PF]: Bottom Part: {part.name} ({part.craftID})");
                }

                Debug.Log("[PF]: Top Parts:");

                if (attachNodeArray1 != null)
                {
                    for (i = 0; i < (int)attachNodeArray1.Length; i++)
                    {
                        attachNode = attachNodeArray1 [i];

                        if (attachNode.attachedPart != null)
                        {
                            if (part != null)
                            {
                                AddPartJoint (attachNode.attachedPart, part, attachNode.FindOpposingNode (), attachNode1.FindOpposingNode ());
                            }

                            addStrut (attachNode.attachedPart, base.part, Vector3.zero);

                            nodeParts.Add (attachNode.attachedPart);
                            Debug.Log($"[PF]: {attachNode.attachedPart.name} ({attachNode.attachedPart.craftID})");
                        }
                    }
                }

                if (attachNodeArray != null)
                {
                    for (i = 0; i < (int)attachNodeArray.Length; i++)
                    {
                        attachNode = attachNodeArray [i];

                        if (attachNode.attachedPart != null)
                        {
                            if (part != null)
                            {
                                AddPartJoint (attachNode.attachedPart, part, attachNode.FindOpposingNode (), attachNode1.FindOpposingNode ());
                            }

                            addStrut (attachNode.attachedPart, base.part, Vector3.zero);

                            nodeParts.Add (attachNode.attachedPart);
                            Debug.Log($"[PF]: {attachNode.attachedPart.name} ({attachNode.attachedPart.craftID})");
                        }
                    }
                }

                morejointsadded = true;
            }
        }

        void AddPartJoint (Part p, Part pp, AttachNode pnode, AttachNode ppnode)
        {
            PartJoint partJoint = PartJoint.Create (p, pp, pnode, ppnode, 0);

            partJoint.SetBreakingForces (breakingForce, breakingForce);

            PartJoint partJoint1 = p.gameObject.AddComponent<PartJoint>();

            partJoint1 = partJoint;
        }

        ConfigurableJoint addStrut (Part p, Part pp, Vector3 pos)
        {
            ConfigurableJoint configurableJoint;

            if (p != pp)
            {
                Rigidbody rigidbody = pp.Rigidbody;

                if ((rigidbody == null ? false : !(rigidbody == p.Rigidbody)))
                {
                    ConfigurableJoint configurableJoint1 = p.gameObject.AddComponent<ConfigurableJoint>();

                    configurableJoint1.xMotion = 0;
                    configurableJoint1.yMotion = 0;
                    configurableJoint1.zMotion = 0;
                    configurableJoint1.angularXMotion = 0;
                    configurableJoint1.angularYMotion = 0;
                    configurableJoint1.angularZMotion = 0;
                    configurableJoint1.projectionDistance = 0.1f;
                    configurableJoint1.projectionAngle = 5f;
                    configurableJoint1.breakForce = breakingForce;
                    configurableJoint1.breakTorque = breakingForce;
                    configurableJoint1.connectedBody = rigidbody;
                    configurableJoint1.targetPosition = pos;
                    configurableJoint = configurableJoint1;
                }
                else
                {
                    configurableJoint = null;
                }
            }
            else
            {
                configurableJoint = null;
            }

            return configurableJoint;
        }

        void ClearJointLines ()
        {
            for (int i = 0; i < jointLines.Count; i++)
            {
                UnityEngine.Object.Destroy (jointLines [i].gameObject);
            }

            jointLines.Clear ();
        }

        public virtual void FixedUpdate ()
        {
            if (!morejointsadded)
            {
                if ((!FlightGlobals.ready || vessel.packed ? false : vessel.loaded))
                {
                    AddMoreJoints ();
                }
            }
        }

        LineRenderer JointLine (Vector3 posp, Vector3 pospp, Color col, float width)
        {
            LineRenderer lineRenderer = makeLineRenderer ("JointLine", col, width);

            lineRenderer.positionCount = 2;

            lineRenderer.SetPosition (0, posp);
            lineRenderer.SetPosition (1, pospp);

            lineRenderer.useWorldSpace = true;

            return lineRenderer;
        }

        public void ListJoints ()
        {
            int j;

            string [] _name;

            float _breakForce;

            Vector3 _anchor;

            string str;
            string str1;

            ClearJointLines ();

            List<Part> activeVessel = FlightGlobals.ActiveVessel.parts;

            for (int i = 0; i < activeVessel.Count; i++)
            {
                ConfigurableJoint [] components = activeVessel[i].gameObject.GetComponents<ConfigurableJoint>();

                if (components != null)
                {
                    for (j = 0; j < (int)components.Length; j++)
                    {
                        ConfigurableJoint cj = components [j];
                        string s = $"[PF]: <ConfigurableJoint>, {activeVessel[i].name}, {(cj.connectedBody == null ? "<none>" : cj.connectedBody.name)}";
                        string pos = (cj.connectedBody == null) ? "--" : $"{activeVessel[i].transform.position - cj.connectedBody.position}";
                        s += $", {cj.breakForce}, {cj.breakTorque}, {cj.anchor}, {cj.connectedAnchor}, {pos}, {cj.linearLimitSpring.damper:F2}, {cj.linearLimitSpring.spring:F2}";
                        Debug.Log(s);
                    }
                }

                PartJoint [] partJointArray = activeVessel[i].gameObject.GetComponents<PartJoint>();

                if (partJointArray != null)
                {
                    for (j = 0; j < (int)partJointArray.Length; j++)
                    {
                        PartJoint partJoint = partJointArray [j];

                        if (partJoint.Host || partJoint.Target)
                        {
                            string target = partJoint.Target == null ? "<none>" : partJoint.Target.name;
                            string forces = partJoint.Joint ? $"{partJoint.Joint.breakForce}, {partJoint.Joint.breakTorque}" : $"<no single joint> ({partJoint.joints.Count})";
                            Debug.Log($"[PF]: <PartJoint>, {partJoint.Host.name}, {target}, {forces}, {partJoint.stiffness:F2}, {partJoint.HostAnchor}, {partJoint.TgtAnchor}");
                        }
                        else
                        {
                            Debug.Log ("[PF]: <PartJoint>, <none>, <none>");
                        }

                        if (partJoint.Target)
                        {
                            AttachNode attachNode = activeVessel [i].FindAttachNodeByPart (partJoint.Target);

                            if (attachNode != null)
                            {
                                Debug.Log($"[PF]: <AttachNode>, {partJoint.Host.name}, {partJoint.Target.name}, {attachNode.breakingForce}, {attachNode.breakingTorque}, {attachNode.contactArea:F2}, {attachNode.attachMethod}, {attachNode.rigid}, {attachNode.radius:F2}");

                                AttachNode attachNode1 = attachNode.FindOpposingNode ();

                                if (attachNode1 != null && attachNode1.owner)
                                {
                                    Debug.Log($"[PF]: <Opposing AttachNode>, {attachNode1.owner.name}, {(attachNode1.attachedPart != null ? attachNode1.attachedPart.name : "<none>")}, {attachNode1.breakingForce}, {attachNode1.breakingTorque}, {attachNode1.contactArea:F2}, {attachNode1.attachMethod}, {attachNode1.rigid}, {attachNode1.radius:F2}");
                                }
                            }
                        }
                    }
                }

                FixedJoint [] fixedJointArray = activeVessel [i].gameObject.GetComponents<FixedJoint>();

                if (fixedJointArray != null)
                {
                    for (j = 0; j < (int)fixedJointArray.Length; j++)
                    {
                        FixedJoint fj = fixedJointArray [j];
                        Debug.Log($"[PF]: <FixedJoint>, {fj.name}, {(fj.connectedBody == null ? "<none>" : fj.connectedBody.name)}, {fj.breakForce}, {fj.breakTorque}, {fj.anchor}, {fj.connectedAnchor}");
                    }
                }
            }
        }

        LineRenderer makeLineRenderer (string objectName, Color color, float wd)
        {
            var gameObjectLine = new GameObject (objectName);

            gameObjectLine.transform.parent = part.transform;

            gameObjectLine.transform.localPosition = Vector3.zero;
            gameObjectLine.transform.localRotation = Quaternion.identity;

            LineRenderer lineRenderer = gameObjectLine.AddComponent<LineRenderer>();

            lineRenderer.useWorldSpace = true;
            lineRenderer.material = new Material (Shader.Find ("Legacy Shaders/Particles/Additive"));
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = wd;
            lineRenderer.endWidth = wd;
            lineRenderer.positionCount = 0;

            return lineRenderer;
        }

        public void OnDestroy ()
        {
            viewJoints = false;

            ClearJointLines ();

            GameEvents.onGameSceneLoadRequested.Remove (new EventData<GameScenes>.OnEvent (OnGameSceneLoadRequested));
            GameEvents.onVesselWasModified.Remove (new EventData<Vessel>.OnEvent (OnVesselModified));
            GameEvents.onPartJointBreak.Remove (new EventData<PartJoint, float>.OnEvent (OnPartJointBreak));
        }

        void OnGameSceneLoadRequested (GameScenes scene)
        {
            if ((scene == GameScenes.FLIGHT ? false : viewJoints))
            {
                viewJoints = false;

                ClearJointLines ();
            }
        }

        void OnPartJointBreak (PartJoint pj, float value)
        {
            Part host;
            int i;
            bool flag;

            if (pj.Host != part)
            {
                if (pj.Target == part)
                {
                    host = pj.Host;

                    if (nodeParts.Contains(host))
                    {
                        if (bottomNodePart != null)
                        {
                            RemoveJoints (host, bottomNodePart);
                        }

                        RemoveJoints (host, part);

                        flag = nodeParts.Remove (host);
                    }
                    else if (host == bottomNodePart)
                    {
                        for (i = 0; i < nodeParts.Count; i++)
                        {
                            RemoveJoints (nodeParts [i], bottomNodePart);
                        }

                        RemoveJoints (bottomNodePart, part);
                    }

                    return;
                }

                return;
            }

            host = pj.Target;

            if (nodeParts.Contains(host))
            {
                if (bottomNodePart != null)
                {
                    RemoveJoints (host, bottomNodePart);
                }

                RemoveJoints (host, part);

                flag = nodeParts.Remove (host);
            }
            else if (host == bottomNodePart)
            {
                for (i = 0; i < nodeParts.Count; i++)
                {
                    RemoveJoints (nodeParts [i], bottomNodePart);
                }

                RemoveJoints (bottomNodePart, part);
            }
        }

        public override void OnStart (PartModule.StartState state)
        {
            base.OnStart (state);

            var _uiControlFlight = (UI_Toggle) Fields["viewJoints"].uiControlFlight;

            _uiControlFlight.onFieldChanged = (Callback<BaseField, object>)Delegate.Combine (_uiControlFlight.onFieldChanged, new Callback<BaseField, object>(UIviewJoints_changed));

            GameEvents.onGameSceneLoadRequested.Add (new EventData<GameScenes>.OnEvent (OnGameSceneLoadRequested));
            GameEvents.onVesselWasModified.Add (new EventData<Vessel>.OnEvent (OnVesselModified));
            GameEvents.onPartJointBreak.Add (new EventData<PartJoint, float>.OnEvent (OnPartJointBreak));
        }

        void OnVesselModified (Vessel v)
        {
            if (v == vessel && viewJoints)
            {
                ViewJoints();
            }
        }

        void RemoveJoints (Part p, Part pp)
        {
            if ((p == null || p.Rigidbody == null || pp == null ? false : !(pp.Rigidbody == null)))
            {
                ConfigurableJoint [] components = p.gameObject.GetComponents<ConfigurableJoint>();

                for (int i = 0; i < (int)components.Length; i++)
                {
                    ConfigurableJoint configurableJoint = components [i];

                    if (configurableJoint.connectedBody == pp.Rigidbody)
                    {
                        try
                        {
                            UnityEngine.Object.Destroy (configurableJoint);
                        }
                        catch (Exception exception1)
                        {
                            Exception exception = exception1;

                            string [] str = { "[PF]: RemoveJoint Anomaly (", p.ToString(), ", ", pp.ToString(), "): ", exception.Message };

                            Debug.Log (string.Concat (str));
                        }
                    }
                }
            }
        }

        void UIviewJoints_changed (BaseField bf, object obj)
        {
            if (viewJoints)
            {
                ListJoints ();

                ViewJoints ();
            }
            else
            {
                ClearJointLines();
            }
        }

        public void ViewJoints ()
        {
            ClearJointLines ();

            List<Part> activeVessel = FlightGlobals.ActiveVessel.parts;

            for (int i = 0; i < activeVessel.Count; i++)
            {
                ConfigurableJoint [] components = activeVessel [i].gameObject.GetComponents<ConfigurableJoint>();

                if (components != null)
                {
                    for (int j = 0; j < (int)components.Length; j++)
                    {
                        ConfigurableJoint configurableJoint = components [j];

                        if (configurableJoint.connectedBody != null)
                        {
                            var vector3 = new Vector3 (0f, 5f, 0f);
                            var vector31 = new Vector3 (0.25f, 0f, 0f);

                            Vector3 _position = activeVessel [i].transform.position + vector3;
                            Vector3 _position1 = configurableJoint.connectedBody.position + vector3;
                            Vector3 _position2 = (activeVessel [i].transform.position + (activeVessel [i].transform.rotation * configurableJoint.anchor)) + vector3;

                            Vector3 vector32 = (configurableJoint.connectedBody.position + (configurableJoint.connectedBody.rotation * configurableJoint.connectedAnchor)) + vector3;

                            jointLines.Add (JointLine (_position, _position1, Color.blue, w1));
                            jointLines.Add (JointLine (_position2, vector32 + vector31, Color.yellow, w2));
                            jointLines.Add (JointLine (_position, _position2, Color.gray, 0.03f));
                            jointLines.Add (JointLine (_position1, vector32, Color.gray, 0.03f));
                        }
                    }
                }
            }
        }
    }
}
