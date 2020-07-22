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
            if (!morejointsadded)
            {
                AttachNode attachNode1 = base.part.FindAttachNode ("bottom");

                Debug.Log($"[PF]: Adding Joints to Vessel: {vessel.vesselName}, (Launch ID: {base.part.launchID}) (GUID: {base.vessel.id})");
                Debug.Log($"[PF]: For PF Part: {base.part.name} ({base.part.craftID})");

                Part targetPart = null;
                if (attachNode1 is AttachNode && attachNode1.attachedPart is Part)
                {
                    targetPart = attachNode1.attachedPart;

                    bottomNodePart = targetPart;

                    addStrut (targetPart, base.part, Vector3.zero);
                    Debug.Log($"[PF]: Bottom Part: {targetPart.name} ({targetPart.craftID})");
                }

                Debug.Log("[PF]: Top Parts:");

                if (base.part.FindAttachNodes("top") is AttachNode[] topNodes)
                {
                    foreach (AttachNode attachNode in topNodes)
                    {
                        if (attachNode.attachedPart is Part)
                        {
                            if (targetPart is Part)
                                AddPartJoint (attachNode.attachedPart, targetPart, attachNode.FindOpposingNode(), attachNode1.FindOpposingNode());

                            addStrut (attachNode.attachedPart, base.part, Vector3.zero);

                            nodeParts.Add (attachNode.attachedPart);
                            Debug.Log($"[PF]: {attachNode.attachedPart.name} ({attachNode.attachedPart.craftID})");
                        }
                    }
                }

                if (base.part.FindAttachNodes("interstage") is AttachNode[] interNodes)
                {
                    foreach (AttachNode an in interNodes)
                    {
                        if (an.attachedPart is Part)
                        {
                            if (targetPart is Part)
                                AddPartJoint (an.attachedPart, targetPart, an.FindOpposingNode (), attachNode1.FindOpposingNode ());

                            addStrut (an.attachedPart, base.part, Vector3.zero);

                            nodeParts.Add (an.attachedPart);
                            Debug.Log($"[PF]: {an.attachedPart.name} ({an.attachedPart.craftID})");
                        }
                    }
                }

                morejointsadded = true;
            }
        }

        void AddPartJoint (Part p, Part pp, AttachNode pnode, AttachNode ppnode)
        {
            PartJoint partJoint = PartJoint.Create(p, pp, pnode, ppnode, 0);
            partJoint.SetBreakingForces(breakingForce, breakingForce);

            // This seems unlikely to be working as intended.
            PartJoint partJoint1 = p.gameObject.AddComponent<PartJoint>();
            partJoint1 = partJoint;
        }

        ConfigurableJoint addStrut (Part p, Part pp, Vector3 pos)
        {
            ConfigurableJoint configurableJoint = null;

            if (p != pp && pp.Rigidbody is Rigidbody rigidbody && rigidbody != p.Rigidbody)
            {
                configurableJoint = p.gameObject.AddComponent<ConfigurableJoint>();

                configurableJoint.xMotion = 0;
                configurableJoint.yMotion = 0;
                configurableJoint.zMotion = 0;
                configurableJoint.angularXMotion = 0;
                configurableJoint.angularYMotion = 0;
                configurableJoint.angularZMotion = 0;
                configurableJoint.projectionDistance = 0.1f;
                configurableJoint.projectionAngle = 5f;
                configurableJoint.breakForce = breakingForce;
                configurableJoint.breakTorque = breakingForce;
                configurableJoint.connectedBody = rigidbody;
                configurableJoint.targetPosition = pos;
            }

            return configurableJoint;
        }

        void ClearJointLines ()
        {
            foreach (LineRenderer r in jointLines)
                Destroy(r.gameObject);
            jointLines.Clear ();
        }

        public virtual void FixedUpdate ()
        {
            if (!morejointsadded)
            {
                if (!FlightGlobals.ready || vessel.packed ? false : vessel.loaded)
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
            ClearJointLines ();

            foreach (Part thePart in FlightGlobals.ActiveVessel.parts)
            {
                if (thePart.GetComponents<ConfigurableJoint>() is ConfigurableJoint[] components)
                {
                    foreach (ConfigurableJoint cj in components)
                    {
                        string s = $"[PF]: <ConfigurableJoint>, {thePart.name}, {(cj.connectedBody == null ? "<none>" : cj.connectedBody.name)}";
                        string pos = (cj.connectedBody == null) ? "--" : $"{thePart.transform.position - cj.connectedBody.position}";
                        s += $", {cj.breakForce}, {cj.breakTorque}, {cj.anchor}, {cj.connectedAnchor}, {pos}, {cj.linearLimitSpring.damper:F2}, {cj.linearLimitSpring.spring:F2}";
                        Debug.Log(s);
                    }
                }

                if (thePart.GetComponents<PartJoint>() is PartJoint[] pjArr)
                {
                    foreach (PartJoint partJoint in pjArr)
                    {
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
                            if (thePart.FindAttachNodeByPart(partJoint.Target) is AttachNode attachNode)
                            {
                                Debug.Log($"[PF]: <AttachNode>, {partJoint.Host.name}, {partJoint.Target.name}, {attachNode.breakingForce}, {attachNode.breakingTorque}, {attachNode.contactArea:F2}, {attachNode.attachMethod}, {attachNode.rigid}, {attachNode.radius:F2}");
                                if (attachNode.FindOpposingNode() is AttachNode attachNode1 && attachNode1.owner)
                                    Debug.Log($"[PF]: <Opposing AttachNode>, {attachNode1.owner.name}, {(attachNode1.attachedPart != null ? attachNode1.attachedPart.name : "<none>")}, {attachNode1.breakingForce}, {attachNode1.breakingTorque}, {attachNode1.contactArea:F2}, {attachNode1.attachMethod}, {attachNode1.rigid}, {attachNode1.radius:F2}");
                            }
                        }
                    }
                }

                if (thePart.GetComponents<FixedJoint>() is FixedJoint[] fja)
                {
                    foreach (FixedJoint fj in fja)
                    {
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

            GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequested);
            GameEvents.onVesselWasModified.Remove(OnVesselModified);
            GameEvents.onPartJointBreak.Remove (OnPartJointBreak);
        }

        void OnGameSceneLoadRequested (GameScenes scene)
        {
            if (scene != GameScenes.FLIGHT && viewJoints)
            {
                viewJoints = false;
                ClearJointLines ();
            }
        }

        void OnPartJointBreak (PartJoint pj, float value)
        {
            Part host;
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
                        foreach (Part p1 in nodeParts)
                        {
                            RemoveJoints (p1, bottomNodePart);
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
                    RemoveJoints (host, bottomNodePart);

                RemoveJoints (host, part);

                flag = nodeParts.Remove (host);
            }
            else if (host == bottomNodePart)
            {
                foreach (Part p in nodeParts)
                {
                    RemoveJoints (p, bottomNodePart);
                }
                RemoveJoints (bottomNodePart, part);
            }
        }

        public override void OnStart (PartModule.StartState state)
        {
            base.OnStart (state);

            Fields[nameof(viewJoints)].uiControlFlight.onFieldChanged += UIviewJoints_changed;
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequested);
            GameEvents.onVesselWasModified.Add(OnVesselModified);
            GameEvents.onPartJointBreak.Add(OnPartJointBreak);
        }

        void OnVesselModified (Vessel v)
        {
            if (v == vessel && viewJoints)
                ViewJoints();
        }

        void RemoveJoints (Part p, Part pp)
        {
            if ((p == null || p.Rigidbody == null || pp == null ? false : !(pp.Rigidbody == null)))
            {
                foreach (ConfigurableJoint configurableJoint in p.GetComponents<ConfigurableJoint>())
                {
                    if (configurableJoint.connectedBody == pp.Rigidbody)
                    {
                        try
                        {
                            Destroy(configurableJoint);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                            Debug.LogError($"[PF]: RemoveJoint Anomaly ({p}, {pp}): {e.Message}");
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

            foreach (Part p in FlightGlobals.ActiveVessel.parts)
            {
                if (p.GetComponents<ConfigurableJoint>() is ConfigurableJoint[] components)
                {
                    foreach (ConfigurableJoint configurableJoint in components)
                    {
                        if (configurableJoint.connectedBody != null)
                        {
                            var vector3 = new Vector3 (0f, 5f, 0f);
                            var vector31 = new Vector3 (0.25f, 0f, 0f);

                            Vector3 _position = p.transform.position + vector3;
                            Vector3 _position1 = configurableJoint.connectedBody.position + vector3;
                            Vector3 _position2 = (p.transform.position + (p.transform.rotation * configurableJoint.anchor)) + vector3;

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
