//  ==================================================
//  Procedural Fairings plug-in by Alexey Volynskov.

//  Licensed under CC-BY-4.0 terms: https://creativecommons.org/licenses/by/4.0/legalcode
//  ==================================================

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
        [KSPField(isPersistant = true)] public bool decoupled = false;

        [KSPField] public string ejectSoundUrl = "Squad/Sounds/sound_decoupler_fire";
        public FXGroup ejectFx;

        [KSPField] public string transformName = "nose_collider";
        [KSPField] public Vector3 forceVector = Vector3.right;
        [KSPField] public Vector3 torqueVector = -Vector3.forward;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Ejection power", guiFormat = "P0", groupName = PFUtils.PAWGroup, groupDisplayName = PFUtils.PAWName)]
        [UI_FloatRange(minValue = 0, maxValue = 1, stepIncrement = 0.01f)]
        public float ejectionPower = 0.32f;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Ejection torque", guiFormat = "P0", groupName = PFUtils.PAWGroup)]
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
                Fields[nameof(fairingStaged)].uiControlEditor.onFieldChanged += ToggleFairingStaging;
                Fields[nameof(fairingStaged)].uiControlEditor.onSymmetryFieldChanged += ToggleFairingStaging;
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

            part.stagingIcon = DefaultIcons.FUEL_TANK.ToString();
            stagingEnabled = fairingStaged;
            SetJettisonEvents();
        }

        public override void OnActive()
        {
            base.OnActive();
            if (fairingStaged) OnJettisonFairing();
        }

        private void SetJettisonEvents()
        {
            Events[nameof(OnJettisonFairing)].guiActive = fairingStaged;
            Events[nameof(OnJettisonFairing)].active = fairingStaged;
            Actions[nameof(ActionJettison)].active = fairingStaged;
        }

        void ToggleFairingStaging(BaseField bf, object obj)
        {
            stagingEnabled = fairingStaged;
            part.UpdateStageability(false, true);
        }

        private IEnumerator HandleFairingDecouple()
        {
            yield return new WaitForFixedUpdate();
            if (part.parent)
            {
                part.decouple();
                ejectFx.audio.Play();
                decoupled = true;
                stagingEnabled = fairingStaged = false;
                part.UpdateStageability(false, true);
                SetJettisonEvents();
            }
            yield return new WaitForFixedUpdate();
            ApplyForces();
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
