//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Keramzit
{
    struct BezierSlope
    {
        Vector2 p1, p2;

        public BezierSlope (Vector4 v)
        {
            p1 = new Vector2 (v.x, v.y);
            p2 = new Vector2 (v.z, v.w);
        }

        public Vector2 interp (float t)
        {
            Vector2 a = Vector2.Lerp (Vector2.zero, p1, t);
            Vector2 b = Vector2.Lerp (p1, p2, t);
            Vector2 c = Vector2.Lerp (p2, Vector2.one, t);
            Vector2 d = Vector2.Lerp (a, b, t);
            Vector2 e = Vector2.Lerp (b, c, t);

            return Vector2.Lerp (d, e, t);
        }
    }

    public static class PFUtils
    {
        public const string PAWName = "ProceduralFairings";
        public const string PAWGroup = "ProceduralFairings";
        public static bool canCheckTech () => HighLogic.LoadedSceneIsEditor && (ResearchAndDevelopment.Instance != null || (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX));

        public static bool haveTech (string name)
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
            {
                return name == "sandbox";
            }

            return ResearchAndDevelopment.GetTechnologyState (name) == RDTech.State.Available;
        }

        public static void GetTechLimits(string cfgName, out float minVal, out float maxVal)
        {
            minVal = float.PositiveInfinity;
            maxVal = float.NegativeInfinity;
            foreach (ConfigNode tech in GameDatabase.Instance.GetConfigNodes(cfgName))
            {
                foreach (ConfigNode.Value value in tech.values)
                {
                    if (haveTech(value.name))
                    {
                        minVal = Mathf.Min(minVal, float.Parse(value.value));
                        maxVal = Mathf.Max(maxVal, float.Parse(value.value));
                    }
                }
            }
        }

        public static float getTechMinValue(string cfgname, float defVal)
        {
            GetTechLimits(cfgname, out float minVal, out float _);
            return float.IsPositiveInfinity(minVal) ? defVal : minVal;
        }

        public static float getTechMaxValue(string cfgname, float defVal)
        {
            GetTechLimits(cfgname, out float _, out float maxVal);
            return float.IsNegativeInfinity(maxVal) ? defVal : maxVal;
        }

        public static void setFieldRange(BaseField field, float minval, float maxval)
        {
            if (field.uiControlEditor is UI_FloatRange fr)
            {
                fr.minValue = minval;
                fr.maxValue = maxval;
            }

            if (field.uiControlEditor is UI_FloatEdit fe)
            {
                fe.minValue = minval;
                fe.maxValue = maxval;
            }
        }

        public static void updateAttachedPartPos (AttachNode node, Part part, Vector3 oldPosWorld)
        {
            if (node is AttachNode && part is Part && 
                node.attachedPart is Part ap && ap.FindAttachNodeByPart(part) is AttachNode an)
            {
                var dp = part.transform.TransformPoint(node.position) - oldPosWorld;

                if (ap == part.parent)
                {
                    while (ap.parent) ap = ap.parent;
                    ap.transform.position += dp;
                    part.transform.position -= dp;
                }
                else
                {
                    ap.transform.position += dp;
                }
            }
        }

        public static string formatMass(float mass) => (mass < 0.01) ? $"{mass * 1e3:N3}kg" : $"{mass:N3}t";
        public static string formatCost(float cost) => $"{cost:N0}";

        public static void enableRenderer (Transform t, bool e)
        {
            if (t is Transform && t.GetComponent<Renderer>() is Renderer r)
                r.enabled = e;
        }

        public static void hideDragStuff(Part part) => enableRenderer(part.FindModelTransform("dragOnly"), false);

        public static bool FARinstalled, FARchecked;

        public static bool isFarInstalled()
        {
            if (!FARchecked)
            {
                FARinstalled = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "FerramAerospaceResearch");
                FARchecked = true;
            }
            return FARinstalled;
        }

        public static void updateDragCube (Part part, float areaScale)
        {
            if (isFarInstalled())
            {
                Debug.Log ("[PF]: Calling FAR to update voxels...");
                part.SendMessage ("GeometryPartModuleRebuildMeshData");
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                enableRenderer(part.FindModelTransform("dragOnly"), true);
                var dragCube = DragCubeSystem.Instance.RenderProceduralDragCube(part);
                enableRenderer(part.FindModelTransform("dragOnly"), false);

                for (int i = 0; i < 6; ++i)
                {
                    dragCube.Area[i] *= areaScale;
                }

                part.DragCubes.ClearCubes();
                part.DragCubes.Cubes.Add(dragCube);
                part.DragCubes.ResetCubeWeights();
                part.DragCubes.ForceUpdate(true, true, false);
            }
        }

        public static IEnumerator<YieldInstruction> updateDragCubeCoroutine (Part part, float areaScale)
        {
            while (true)
            {
                if (part == null) yield break;
                if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) yield break;
                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (part.vessel == null) yield break;
                    if (!FlightGlobals.ready || part.packed || !part.vessel.loaded)
                    {
                        yield return new WaitForFixedUpdate ();
                        continue;
                    }
                    break;
                } else if (HighLogic.LoadedSceneIsEditor)
                {
                    yield return new WaitForFixedUpdate ();
                    break;
                }
            }
            updateDragCube(part, areaScale);
        }

        public static Part partFromHit (this RaycastHit hit)
        {
            if (hit.collider == null || hit.collider.gameObject == null)
            {
                return null;
            }

            var go = hit.collider.gameObject;

            var p = Part.FromGO (go);

            while (p == null)
            {
                if (go?.transform?.parent?.gameObject != null)
                    go = go.transform.parent.gameObject;
                else
                    break;

                p = Part.FromGO (go);
            }

            return p;
        }

        public static List<Part> getAllChildrenRecursive (this Part rootPart, bool root)
        {
            var children = new List<Part>();

            if (!root)
                children.Add (rootPart);

            foreach (Part child in rootPart.children)
            {
                children.AddRange(child.getAllChildrenRecursive(false));
            }

            return children;
        }

        public static float GetMaxValueFromList (List<float> list)
        {
            float max = float.NegativeInfinity;
            foreach (float f in list)
                max = Mathf.Max(max, f);
            return max;
        }
    }

    [KSPAddon (KSPAddon.Startup.EditorAny, false)]
    public class EditorScreenMessager : MonoBehaviour
    {
        static float osdMessageTime;
        static string osdMessageText;

        public static void showMessage (string msg, float delay)
        {
            osdMessageText = msg;
            osdMessageTime = Time.time + delay;
        }

        void OnGUI ()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            if (Time.time < osdMessageTime)
            {
                GUI.skin = HighLogic.Skin;

                var style = new GUIStyle ("Label");

                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 20;
                style.normal.textColor = Color.black;

                GUI.Label (new Rect (2, 2 + (Screen.height / 9), Screen.width, 50), osdMessageText, style);

                style.normal.textColor = Color.yellow;

                GUI.Label (new Rect (0, Screen.height / 9, Screen.width, 50), osdMessageText, style);
            }
        }
    }
}
