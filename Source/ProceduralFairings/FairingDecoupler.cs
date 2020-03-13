//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

using KSP.UI.Screens;
using System.Collections;
using UnityEngine;

namespace Keramzit
{
    public class ProceduralFairingDecoupler : PartModule
    {
        [KSPField] public float ejectionDv = 15;
        [KSPField] public float ejectionTorque = 10;
        [KSPField] public float ejectionLowDv;
        [KSPField] public float ejectionLowTorque;

        [KSPField] public string ejectSoundUrl = "Squad/Sounds/sound_decoupler_fire";
        public FXGroup ejectFx;

        [KSPField] public string transformName = "nose_collider";
        [KSPField] public Vector3 forceVector = Vector3.right;
        [KSPField] public Vector3 torqueVector = -Vector3.forward;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Ejection power", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatRange(minValue = 0, maxValue = 1, stepIncrement = 0.01f)]
        public float ejectionPower = 0.32f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Ejection torque", groupName = PFUtils.PAWGroup)]
        [UI_FloatRange(minValue = 0, maxValue = 1, stepIncrement = 0.01f)]
        public float torqueAmount = 0.01f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Fairing Decoupler", groupName = PFUtils.PAWGroup)]
        [UI_Toggle(disabledText = "Off", enabledText = "On", affectSymCounterparts = UI_Scene.All)]
        public bool fairingStaged = true;

        [KSPAction("Jettison Fairing", actionGroup = KSPActionGroup.None)]
        public void ActionJettison (KSPActionParam param) => OnJettisonFairing();

        [KSPEvent(name = "Jettison", guiName = "Jettison Fairing", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        public void OnJettisonFairing() => StartCoroutine(HandleFairingDecouple());

        public override void OnStart (StartState state)
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                (Fields[nameof(fairingStaged)].uiControlEditor as UI_Toggle).onFieldChanged += OnUpdateUI;
                (Fields[nameof(fairingStaged)].uiControlEditor as UI_Toggle).onSymmetryFieldChanged += OnUpdateUI;
            }

            ejectFx.audio = part.gameObject.AddComponent<AudioSource>();
            ejectFx.audio.volume = GameSettings.SHIP_VOLUME;
            ejectFx.audio.rolloffMode = AudioRolloffMode.Logarithmic;
            ejectFx.audio.maxDistance = 100;
            ejectFx.audio.loop = false;
            ejectFx.audio.playOnAwake = false;
            ejectFx.audio.dopplerLevel = 0f;
            ejectFx.audio.spatialBlend = 1.0f;
            ejectFx.audio.panStereo = 0f;

            if (GameDatabase.Instance.ExistsAudioClip (ejectSoundUrl))
                ejectFx.audio.clip = GameDatabase.Instance.GetAudioClip (ejectSoundUrl);
            else
                Debug.LogError ($"[PF]: Cannot find decoupler sound: {ejectSoundUrl} for {this}");

            //  Set the state of the "Jettison Fairing" PAW button.
            SetJettisonEvents();
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            // Previous version stated "the staging icons cannot be updated correctly via OnStart()"
            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
                OnSetStagingIcons();
        }

        public void OnSetStagingIcons()
        {
            //  Set the staging icon for the parent part.
            if (fairingStaged)
                part.stackIcon.CreateIcon();
            else
                part.stackIcon.RemoveIcon();

            StageManager.Instance.SortIcons(true);
        }

        private void SetJettisonEvents()
        {
            Events[nameof(OnJettisonFairing)].guiActive = fairingStaged;
            Events[nameof(OnJettisonFairing)].active = fairingStaged;
            Actions[nameof(ActionJettison)].active = fairingStaged;
        }

        void OnUpdateUI(BaseField bf, object obj) => OnSetStagingIcons();

        private IEnumerator HandleFairingDecouple()
        {
            yield return new WaitForFixedUpdate();
            if (part.parent)
            {
                var pfa = part.parent.GetComponent<ProceduralFairingAdapter>();
                foreach (Part p in part.parent.children)
                {
                    //  Check if the top node allows decoupling when the fairing is also decoupled.
                    if (pfa && !pfa.topNodeDecouplesWhenFairingsGone && p.GetComponent<ProceduralFairingSide>() is ProceduralFairingSide)
                        continue;

                    if (p.GetComponents<ConfigurableJoint>() is ConfigurableJoint[] joints)
                    {
                        foreach (ConfigurableJoint joint in joints)
                        {
                            if (joint.GetComponent<Rigidbody>() == part.Rigidbody || joint.connectedBody == part.Rigidbody)
                                Destroy(joint);
                        }
                    }
                }

                part.decouple(0);
                ejectFx.audio.Play();
            }
            yield return new WaitForFixedUpdate();
            ApplyForces();
            fairingStaged = false;
            SetJettisonEvents();
        }

        private void ApplyForces()
        {
            if (part.FindModelTransform(transformName) is Transform tr)
            {
                part.Rigidbody.AddForce(tr.TransformDirection(forceVector) * Mathf.Lerp(ejectionLowDv, ejectionDv, ejectionPower), ForceMode.VelocityChange);
                part.Rigidbody.AddTorque(tr.TransformDirection(torqueVector) * Mathf.Lerp(ejectionLowTorque, ejectionTorque, torqueAmount), ForceMode.VelocityChange);
            }
            else
                Debug.LogError($"[PF]: No '{transformName}' transform in part {part}!");
        }
    }
}
