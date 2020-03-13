//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using UnityEngine;

namespace Keramzit
{
    public class KzNodeNumberTweaker : PartModule
    {
        [KSPField] public string nodePrefix = "bottom";
        [KSPField] public int maxNumber;

        [KSPField(guiActiveEditor = true, guiName = "Fairing Nodes", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatRange(minValue = 1, maxValue = 8, stepIncrement = 1)]
        public float uiNumNodes = 2;

        [KSPField(isPersistant = true)]
        public int numNodes = 2;

        int numNodesBefore;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Node offset", guiFormat = "S4", guiUnits = "m", groupName = PFUtils.PAWGroup)]
        [UI_FloatEdit(sigFigs = 3, unit = "m", minValue = 0.1f, maxValue = 5, incrementLarge = 0.625f, incrementSmall = 0.125f, incrementSlide = 0.001f)]
        public float radius = 1.25f;

        [KSPField] public float radiusStepLarge = 0.625f;
        [KSPField] public float radiusStepSmall = 0.125f;

        [KSPField] public bool shouldResizeNodes = true;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Interstage Nodes", groupName = PFUtils.PAWGroup)]
        [UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool showInterstageNodes = true;

        protected float oldRadius = -1000;

        public override string GetInfo() => $"Max Nodes: {maxNumber}";

        public void OnRadiusChanged(BaseField f, object obj)
        {
            UpdateNodePositions(true);
            oldRadius = radius;
        }

        public void OnNumNodesChanged(BaseField f, object obj)
        {
            if (checkNodeAttachments())
            {
                uiNumNodes = numNodesBefore;
            }
            else
            {
                numNodes = Convert.ToInt32(uiNumNodes);
                numNodesBefore = numNodes;
                UpdateNodes(true);

                for (int i = numNodes + 1; i <= maxNumber; ++i)
                {
                    if (findNode(i) is AttachNode node)
                        HideUnusedNode(node);
                }
                if (part.GetComponent<ProceduralFairingBase>() is ProceduralFairingBase fbase)
                    fbase.recalcShape();
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (radius != oldRadius)
                    OnRadiusChanged(Fields[nameof(radius)], oldRadius);

                if (Convert.ToInt32(uiNumNodes) != numNodesBefore)
                    OnNumNodesChanged(Fields[nameof(uiNumNodes)], numNodesBefore);
            }
        }

        //  Slightly hacky...but it removes the ghost nodes.
        void HideUnusedNode (AttachNode node) => node.position.x = 10000;

        public override void OnStart (StartState state)
        {
            base.OnStart (state);

            (Fields[nameof(radius)].uiControlEditor as UI_FloatEdit).incrementLarge = radiusStepLarge;
            (Fields[nameof(radius)].uiControlEditor as UI_FloatEdit).incrementSmall = radiusStepSmall;
            Fields[nameof(radius)].guiActiveEditor = shouldResizeNodes;
            Fields[nameof(radius)].uiControlEditor.onFieldChanged += OnRadiusChanged;
            Fields[nameof(radius)].uiControlEditor.onSymmetryFieldChanged += OnRadiusChanged;

            Fields[nameof(showInterstageNodes)].guiActiveEditor = part.FindAttachNodes("interstage") != null;

            //  Change the GUI text if there are no fairing attachment nodes.
            if (part.FindAttachNodes("connect") == null)
                Fields[nameof(uiNumNodes)].guiName = "Side Nodes";

            (Fields[nameof(uiNumNodes)].uiControlEditor as UI_FloatRange).maxValue = maxNumber;
            Fields[nameof(uiNumNodes)].uiControlEditor.onFieldChanged += OnNumNodesChanged;
            Fields[nameof(uiNumNodes)].uiControlEditor.onSymmetryFieldChanged += OnNumNodesChanged;

            uiNumNodes = numNodes;
            numNodesBefore = numNodes;

            UpdateNodes(false);
        }

        private void UpdateNodes(bool pushAttachments)
        {
            AddRemoveNodes();
            UpdateNodePositions(pushAttachments);
        }

        string nodeName(int i) => $"{nodePrefix}{i:d2}";
        AttachNode findNode(int i) => part.FindAttachNode(nodeName (i));

        bool checkNodeAttachments()
        {
            for (int i = 1; i <= maxNumber; ++i)
            {
                if (findNode(i) is AttachNode node && node.attachedPart is Part)
                {
                    EditorScreenMessager.showMessage ("Please detach any fairing parts before changing the number of nodes!", 1);
                    return true;
                }
            }
            return false;
        }

        private AttachNode GetExemplarNode()
        {
            for (int i = 1; i <= maxNumber; ++i)
            {
                if (findNode(i) is AttachNode node)
                    return node;
            }

            if (part.FindAttachNode("bottom") is AttachNode node2)
                return node2;
            return null;
        }

        private void AddRemoveNodes()
        {
            AttachNode exemplar = GetExemplarNode();
            float y = exemplar is AttachNode ? exemplar.position.y : 0;
            int nodeSize = exemplar is AttachNode ? exemplar.size : 0;
            Vector3 dir = exemplar is AttachNode ? exemplar.orientation : Vector3.up;
            Vector3 pos = new Vector3(0, y, 0);
            int i;
            part.stackSymmetry = numNodes - 1;

            for (i = 1; i <= numNodes; ++i)
            {
                if (findNode(i) == null)
                {
                    //  Create the fairing attachment node.
                    AttachNode node = new AttachNode
                    {
                        id = nodeName(i),
                        owner = part,
                        nodeType = AttachNode.NodeType.Stack,
                        position = pos,
                        orientation = dir,
                        originalPosition = pos,
                        originalOrientation = dir,
                        size = nodeSize
                    };

                    part.attachNodes.Add(node);
                }
            }

            for (; i <= maxNumber; ++i)
            {
                if (findNode(i) is AttachNode node2)
                {
                    if (HighLogic.LoadedSceneIsEditor)
                        HideUnusedNode(node2);
                    else
                        part.attachNodes.Remove(node2);
                }
            }

            if (part.GetComponent<ProceduralFairingBase>() is ProceduralFairingBase fbase)
                fbase.recalcShape();
        }

        private void UpdateNodePositions(bool pushAttachments)
        {
            float d = Mathf.Sin (Mathf.PI / numNodes) * radius * 2;
            int size = Mathf.RoundToInt (d / (radiusStepLarge * 2));

            for (int i = 1; i <= numNodes; ++i)
            {
                if (findNode(i) is AttachNode node)
                {
                    float a = Mathf.PI * 2 * (i - 1) / numNodes;

                    node.position.x = Mathf.Cos(a) * radius;
                    node.position.z = Mathf.Sin(a) * radius;

                    if (shouldResizeNodes)
                        node.size = size;

                    if (pushAttachments)
                        PFUtils.updateAttachedPartPos(node, part);
                }
            }

            for (int i = numNodes + 1; i <= maxNumber; ++i)
            {
                if (findNode(i) is AttachNode node)
                    HideUnusedNode(node);
            }
        }
    }
}
