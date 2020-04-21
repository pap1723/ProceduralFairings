using System;
using SaveUpgradePipeline;
using UnityEngine;

namespace ProceduralFairings
{
    [UpgradeModule(LoadContext.SFS | LoadContext.Craft, sfsNodeUrl = "GAME/FLIGHTSTATE/VESSEL/PART", craftNodeUrl = "PART")]
    public class DeprecateAdapterAndResizer : UpgradeScript
    {
        public override string Name { get => "ProceduralFairings 2.0 PartModule Deprecater";  }
        public override string Description { get => "Upgrades pre-2.0 ProceduralFairings parts"; }
        public override Version EarliestCompatibleVersion { get => new Version(1, 0, 0); }
        public override Version TargetVersion { get => new Version(1,8,1); }
        protected override bool CheckMaxVersion(Version v) => true; // Upgrades are ProcFairings-dependent, not KSP version.
        // protected override TestResult VersionTest(Version v) => true;    // Could also do it this way.

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            nodeName = NodeUtil.GetPartNodeNameValue(node, loadContext);
            ConfigNode[] nodes = node.GetNodes("MODULE");
            ConfigNode pfBaseNode = node.GetNode("MODULE", "name", "ProceduralFairingBase");
            ConfigNode adapterNode = node.GetNode("MODULE", "name", "ProceduralFairingAdapter");
            ConfigNode resizerNode = node.GetNode("MODULE", "name", "KzFairingBaseResizer");
            //Debug.Log($"[PF] UpgradePipeline() Context: {loadContext}\nInput node: {node}\nfound Base: {pfBaseNode}\nAdapter: {adapterNode}\nResizer: {resizerNode}");
            TestResult res = (pfBaseNode is ConfigNode && (adapterNode is ConfigNode || resizerNode is ConfigNode)) ? TestResult.Upgradeable : TestResult.Pass;
            Debug.Log($"[PF] UpgradePipeline OnTest() context {loadContext} for {nodeName}.  Upgrade requested: {res}");
            return res;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            string nodeName = NodeUtil.GetPartNodeNameValue(node, loadContext);
            Debug.Log($"[PF] UpgradePipeline OnUpgrade() context {loadContext} nodeName {nodeName}");
            // When the node is the PART, ParentNode is the Ship
            ConfigNode pfBaseNode = node.GetNode("MODULE", "name", "ProceduralFairingBase");
            if (node.GetNode("MODULE", "name", "ProceduralFairingAdapter") is ConfigNode adapterNode)
            {
                if (adapterNode.HasValue("baseSize"))
                    pfBaseNode.SetValue("baseSize", adapterNode.GetValue("baseSize"), true);
                if (adapterNode.HasValue("topSize"))
                    pfBaseNode.SetValue("topSize", adapterNode.GetValue("topSize"), true);
                if (adapterNode.HasValue("height"))
                    pfBaseNode.SetValue("height", adapterNode.GetValue("height"), true);
                if (adapterNode.HasValue("extraHeight"))
                    pfBaseNode.SetValue("extraHeight", adapterNode.GetValue("extraHeight"), true);
                if (adapterNode.HasValue("topNodeDecouplesWhenFairingsGone"))
                    pfBaseNode.SetValue("autoDecoupleTopNode", adapterNode.GetValue("topNodeDecouplesWhenFairingsGone"), true);
                if (adapterNode.HasValue("topNodeName"))
                    pfBaseNode.SetValue("topNodeName", adapterNode.GetValue("topNodeName"), true);
                pfBaseNode.SetValue("mode", $"{Keramzit.ProceduralFairingBase.BaseMode.Adapter}", true);
                Debug.Log($"[PF] Updated ProceduralFairingBase with Adapter data to {pfBaseNode}");
                node.RemoveNode(adapterNode);
            }
            if (node.GetNode("MODULE", "name", "KzFairingBaseResizer") is ConfigNode resizerNode)
            {
                if (resizerNode.HasValue("size"))
                    pfBaseNode.SetValue("baseSize", resizerNode.GetValue("size"), true);
                pfBaseNode.SetValue("mode", $"{Keramzit.ProceduralFairingBase.BaseMode.Payload}", true);
                Debug.Log($"[PF] Updated ProceduralFairingBase with Resizer data to {pfBaseNode}");
                node.RemoveNode(resizerNode);
            }
        }
    }
}
