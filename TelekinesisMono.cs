using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using TotT;
using UnityEngine;

namespace TotT_Plus
{

    public class TelekinesisModule : ItemModule
    {
        public toggleMethod toggleMethod;
        public override void OnItemLoaded(Item item)
        {
            // Load the base class.
            base.OnItemLoaded(item);
            // Add the HolsterShield component for initialization and set the module.
            item.gameObject.AddComponent<TelekinesisMono>().ItemModule = this;
        }
    }

    public class TelekinesisMono : ArmModule
    {
        bool AltModeOn = false;
        private Color OnColor = new Color(8f, 4f, 0f, 1f);
        private Color OnColorOrange = new Color(8f, 4f, 0f, 1f);
        private Color OnColorPurple = new Color(4f, 0f, 8f, 1f);
        private Color OffColor = new Color(0.1f, 0.1f, .1f, 1f);
        private Color CurrentColor;
        private Rigidbody jointPoint;
        private Joint joint;
        private float angleOfDetection = 45f;
        private float rangeOfDetection = 7f;
        private Creature creatureTargeted;
        private Item itemTargeted;
        private Transform directionOfTarget;
        private Transform Meshes;
        private Transform Colliders;
        private float rotSpeed = 5f;
        private float rotSpeedIdle = 50f;
        private float rotSpeedUse = 150f;
        private float tempRotSpeed = 5f;
        private bool useTempSpeed = false;
        private float speedTransitionRot = 0.5f;
        private float timeTransitionRot = 0f;
        private bool wasActivated = false;
        private float forceOfThrowCreatures = 7f;
        private float forceOfThrowItems = 7f;
        private bool gripWasPressed = false;
        private float rotationValueMin = 0f;
        private float rotationValueMax = 180f;
        private float rotationValue = 0f;
        private float speedTransitionRotationWeapon = 0.5f;
        private float timeTransitionRotationWeapon = 0f;
        private bool reverseRotationWeapon = false;
        public TelekinesisModule ItemModule { get; internal set; }
        private toggleMethod toggleMethod;
        private EffectInstance spinEffectInstance;
        private EffectData spinEffectDataActivated;
        private EffectData spinEffectDataCreature;
        private EffectData spinEffectDataItem;
        private EffectData SFXTelekinesisModuleStart;
        private EffectData SFXTelekinesisModuleLoop;
        private EffectData SFXTelekinesisModuleThrow;
        private EffectData SFXTelekinesisModuleRotate;
        private EffectData SFXTelekinesisModuleSwapCreature;
        private EffectData SFXTelekinesisModuleSwapItem;
        private EffectInstance SFXTelekinesisModuleStartInstance;
        private EffectInstance SFXTelekinesisModuleLoopInstance;
        private EffectInstance SFXTelekinesisModuleThrowInstance;
        private EffectInstance SFXTelekinesisModuleRotateInstance;
        private EffectInstance SFXTelekinesisModuleSwapCreatureInstance;
        private EffectInstance SFXTelekinesisModuleSwapItemInstance;
        private AudioContainer startSFX;
        private List<Material> materialMeshes = new List<Material>();
        private bool soundLoop = false;

        public override void OnStart()
        {
            base.OnStart();
            toggleMethod = ItemModule.toggleMethod;
            HasAltMode = true;
            customData = new ArmModuleSave();
            ModuleHandWatcher[] checker = item.GetComponents<ModuleHandWatcher>();
            foreach (ModuleHandWatcher watcher in checker)
            {
                UnityEngine.Object.Destroy(watcher);
            }
            item.TryGetCustomData<ArmModuleSave>(out customData);
            if (customData != null)
            {
                if (customData.OnOff)
                    OnOff = true;
                else
                    OnOff = false;
            }
            else
            {
                customData = new ArmModuleSave();
            }
            OnOff = true;
            if (item.holder != null)
            {
                OnSnapEvent(item.holder);
            }
            CurrentColor = OnColor;
            jointPoint = new GameObject().AddComponent<Rigidbody>();
            jointPoint.useGravity = false;
            jointPoint.isKinematic = true;
            directionOfTarget = item.GetCustomReference("DirectionOfScan");
            Meshes = item.GetCustomReference("Meshes");
            Colliders = item.GetCustomReference("Colliders");
            foreach(MeshRenderer meshRenderer in Meshes.GetComponentsInChildren<MeshRenderer>())
            {
                materialMeshes.Add(meshRenderer.material);
            }
            spinEffectDataCreature = Catalog.GetData<EffectData>("ToTT_PlusTelekinesisCreature");
            spinEffectDataItem = Catalog.GetData<EffectData>("ToTT_PlusTelekinesisItem");
            spinEffectDataActivated = spinEffectDataItem;
            SFXTelekinesisModuleStart = Catalog.GetData<EffectData>("SFXTelekinesisModuleStart");
            SFXTelekinesisModuleLoop = Catalog.GetData<EffectData>("SFXTelekinesisModuleLoop");
            SFXTelekinesisModuleThrow = Catalog.GetData<EffectData>("SFXTelekinesisModuleThrow");
            SFXTelekinesisModuleRotate = Catalog.GetData<EffectData>("SFXTelekinesisModuleRotate");
            SFXTelekinesisModuleSwapCreature = Catalog.GetData<EffectData>("SFXTelekinesisModuleSwapCreature");
            SFXTelekinesisModuleSwapItem = Catalog.GetData<EffectData>("SFXTelekinesisModuleSwapItem");
            Catalog.LoadAssetAsync<AudioContainer>("Neeshka.TotT_Plus.SFXTelekinesisModuleStart", resultAudioContainer =>
            {
                startSFX = resultAudioContainer;
            }, "startSFX");
        }

        public void SaveData()
        {
            if (customData != null)
            {
                customData.OnOff = OnOff;
                item.RemoveCustomData<ArmModuleSave>();
                item.AddCustomData<ArmModuleSave>(customData);
            }
        }

        public override void On()
        {
            OnOff = true;
            // Change effects colors
            StartCoroutine(ChangeColor(OnColor));
            SaveData();
            base.On();
        }

        public override void Off()
        {
            OnOff = false;
            // Change effects colors
            StartCoroutine(ChangeColor(OffColor));
            SaveData();
            base.Off();
        }

        public override void Activate()
        {
            Hand.playerHand.controlHand.HapticShort(2f);
            if (!Activated)
            {
                if (AltModeOn)
                {
                    if (Snippet.CenteredCreatureInConeRadius(Hand.transform.position, rangeOfDetection, Hand.PointDir, angleOfDetection) != null)
                    {
                        creatureTargeted = Snippet.CenteredCreatureInConeRadius(Hand.transform.position, rangeOfDetection, Hand.PointDir, angleOfDetection);
                        if (creatureTargeted != null)
                        {
                            GrabCreature();
                            Activated = true;
                            spinEffectDataActivated = spinEffectDataCreature;
                        }
                    }
                }
                else
                {
                    if (Snippet.CenteredItemInConeRadius(Hand.transform.position, rangeOfDetection, Hand.PointDir, angleOfDetection, itemToExclude: item) != null)
                    {
                        itemTargeted = Snippet.CenteredItemInConeRadius(Hand.transform.position, rangeOfDetection, Hand.PointDir, angleOfDetection, itemToExclude: item);
                        if (itemTargeted != null)
                        {
                            GrabItem();
                            Activated = true;
                            spinEffectDataActivated = spinEffectDataItem;
                        }
                    }
                }
            }
            base.Activate();
        }

        public override void Deactivate()
        {
            Activated = false;
            base.Deactivate();
        }

        public override void AltMode()
        {
            // Item
            if (AltModeOn)
            {
                AltModeOn = false;
                OnColor = OnColorOrange;
                StartCoroutine(ChangeColor(OnColor));
                SFXTelekinesisModuleSwapItemInstance = SFXTelekinesisModuleSwapItem.Spawn(item.transform.position, Quaternion.identity);
                SFXTelekinesisModuleSwapItemInstance.Play();
            }
            // Creature
            else
            {
                AltModeOn = true;
                OnColor = OnColorPurple;
                StartCoroutine(ChangeColor(OnColor));
                SFXTelekinesisModuleSwapCreatureInstance = SFXTelekinesisModuleSwapCreature.Spawn(item.transform.position, Quaternion.identity);
                SFXTelekinesisModuleSwapCreatureInstance.Play();
            }
        }

        private void GrabCreature()
        {
            creatureTargeted.brain.AddNoStandUpModifier(this);
            creatureTargeted.ragdoll.SetPhysicModifier(this, 0f);
            creatureTargeted.ragdoll.AddPhysicToggleModifier(this);
            if (!creatureTargeted.isKilled)
            {
                creatureTargeted.ragdoll.SetState(Ragdoll.State.Destabilized);
            }
            float modifier = Mathf.Sqrt(creatureTargeted.ragdoll.totalMass);
            jointPoint.transform.position = creatureTargeted.ragdoll.GetPart(RagdollPart.Type.Torso).transform.position;
            joint = Snippet.CreateSimpleJoint(jointPoint, creatureTargeted.ragdoll.GetPart(RagdollPart.Type.Torso).rb, 1000f * modifier, 150f * modifier);
            joint.connectedAnchor = creatureTargeted.ragdoll.GetPart(RagdollPart.Type.Torso).rb.centerOfMass;
        }

        private void ThrowCreature()
        {
            creatureTargeted.brain.RemoveNoStandUpModifier(this);
            creatureTargeted.ragdoll.RemovePhysicModifier(this);
            creatureTargeted.ragdoll.RemovePhysicToggleModifier(this);
            foreach (RagdollPart part in creatureTargeted.ragdoll.parts)
                part.rb.AddForce(Hand.rb.velocity * forceOfThrowCreatures, ForceMode.VelocityChange);
            creatureTargeted = null;
            Activated = false;
            Destroy(joint);
        }

        private void GrabItem()
        {
            if (itemTargeted.isPenetrating)
            {
                itemTargeted.Depenetrate();
            }
            itemTargeted.mainCollisionHandler.SetPhysicModifier(this, 0f);
            itemTargeted.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
            itemTargeted.rb.collisionDetectionMode = Catalog.gameData.collisionDetection.telekinesis;
            itemTargeted.forceThrown = true;
            itemTargeted.Throw(1f, Item.FlyDetection.CheckAngle);
            float modifier = Mathf.Sqrt(itemTargeted.rb.mass < 5f ? itemTargeted.rb.mass * 20f : itemTargeted.rb.mass * 10f);
            jointPoint.transform.position = itemTargeted.rb.worldCenterOfMass;
            joint = Snippet.CreateSimpleJoint(jointPoint, itemTargeted.rb, 1000f * modifier, 150f * modifier);
            joint.connectedAnchor = item.rb.centerOfMass;
        }

        private void ThrowItem()
        {
            itemTargeted.mainCollisionHandler.RemovePhysicModifier(this);
            itemTargeted.rb.AddForce(Hand.rb.velocity * forceOfThrowItems, ForceMode.VelocityChange);
            itemTargeted.Throw(1f, Item.FlyDetection.Forced);
            itemTargeted = null;
            Activated = false;
            Destroy(joint);
        }

        public IEnumerator ChangeColor(Color newColor)
        {
            yield return new WaitForSeconds(0.2f);
            float tts = 0.2f;
            float timeElapsed = 0f;
            Color toHitC = newColor;
            Color CurrentC = CurrentColor;
            while (timeElapsed <= tts)
            {
                foreach (Material material in materialMeshes)
                {
                    material.SetColor("_EmissionColor", Color.Lerp(CurrentC, toHitC, timeElapsed / tts));
                }
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            foreach (Material material in materialMeshes)
            {
                material.SetColor("_EmissionColor", newColor);
            }
            CurrentColor = newColor;
            yield break;
        }

        private void RotationSpeedAdjustement()
        {
            // Switch speed and reset timer
            if (wasActivated != Activated)
            {
                if (Activated)
                {
                    // If the speed is greater than idle
                    if (rotSpeed >= rotSpeedIdle + 0.1f)
                    {
                        useTempSpeed = true;
                        tempRotSpeed = rotSpeed;
                    }
                    else
                    {
                        useTempSpeed = false;
                        tempRotSpeed = 0f;
                    }
                    spinEffectInstance = spinEffectDataActivated.Spawn(item.transform.position - directionOfTarget.forward * 0.07070017f, Quaternion.LookRotation(directionOfTarget.forward), item.transform);
                    spinEffectInstance.Play();
                    StartCoroutine(SoundLoopPlaying());
                }
                else
                {
                    // If the speed is lower than use
                    if (rotSpeed <= rotSpeedUse - 0.1f)
                    {
                        useTempSpeed = true;
                        tempRotSpeed = rotSpeed;
                    }
                    else
                    {
                        useTempSpeed = false;
                        tempRotSpeed = 0f;
                    }
                    spinEffectInstance.End();
                }
                timeTransitionRot = speedTransitionRot;
                wasActivated = Activated;
            }
            timeTransitionRot -= Time.deltaTime / speedTransitionRot;
            timeTransitionRot = Mathf.Clamp(timeTransitionRot, 0, speedTransitionRot);
            // Increase speed
            if (Activated)
            {
                // Equals to the speed of the Idle
                if (useTempSpeed)
                    rotSpeed = Mathf.Lerp(rotSpeedUse, tempRotSpeed, timeTransitionRot);
                else
                    rotSpeed = Mathf.Lerp(rotSpeedUse, rotSpeedIdle, timeTransitionRot);
            }
            // Slow speed
            else
            {
                if (useTempSpeed)
                    rotSpeed = Mathf.Lerp(rotSpeedIdle, tempRotSpeed, timeTransitionRot);
                else
                    rotSpeed = Mathf.Lerp(rotSpeedIdle, rotSpeedUse, timeTransitionRot);
            }
        }

        private void RotateWeapon()
        {
            timeTransitionRotationWeapon -= Time.deltaTime / speedTransitionRotationWeapon;
            timeTransitionRotationWeapon = Mathf.Clamp(timeTransitionRotationWeapon, 0, speedTransitionRotationWeapon);
            // Increase speed
            if (reverseRotationWeapon)
            {
                rotationValue = Mathf.Lerp(rotationValueMax, rotationValueMin, timeTransitionRotationWeapon);
            }
            // Slow speed
            else
            {
                rotationValue = Mathf.Lerp(rotationValueMin, rotationValueMax, timeTransitionRotationWeapon);
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (joint && Activated)
            {
                if (spinEffectInstance != null)
                    spinEffectInstance.SetIntensity(rotSpeed);
                if (!AltModeOn)
                {
                    itemTargeted.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
                }
                //Hand.playerHand.controlHand.HapticShort(0.2f);
                if (!Hand.playerHand.controlHand.castPressed)
                {
                    if (!soundLoop)
                        SFXTelekinesisModuleStartInstance.Stop();
                    if (soundLoop)
                        SFXTelekinesisModuleLoopInstance.Stop();
                    SFXTelekinesisModuleThrowInstance = SFXTelekinesisModuleThrow.Spawn(item.transform.position, Quaternion.identity);
                    SFXTelekinesisModuleThrowInstance.Play();
                    soundLoop = false;
                    if (AltModeOn)
                    {
                        ThrowCreature();
                    }
                    else
                    {
                        ThrowItem();
                    }
                }
            }
        }
        public void FixedUpdate()
        {
            if (Hand != null)
            {
                jointPoint.transform.position = Hand.transform.position + Hand.PointDir * (Hand.transform.position - (Player.currentCreature.GetChest() + Player.currentCreature.transform.right * (Side.Right == Hand.side ? 0.30f : (-0.30f)))).magnitude * 10f;
                jointPoint.transform.rotation = Hand.side == Side.Right ? Hand.transform.rotation * Quaternion.Euler(0f, 0f, rotationValue) : Hand.transform.rotation * Quaternion.Euler(0f, -180f, 180f - rotationValue);
            }
            RotationSpeedAdjustement();
            Meshes.Rotate(Time.deltaTime * rotSpeed, 0f, 0f, Space.Self);
            Colliders.Rotate(Time.deltaTime * rotSpeed, 0f, 0f, Space.Self);
            if (joint && Activated)
            {
                RotateWeapon();
                if (Hand.playerHand.controlHand.gripPressed && !gripWasPressed)
                {
                    gripWasPressed = Hand.playerHand.controlHand.gripPressed;
                    timeTransitionRotationWeapon = speedTransitionRotationWeapon;
                    reverseRotationWeapon ^= true;
                    // Sounds for rotating stuff
                    if (itemTargeted != null)
                    {
                        SFXTelekinesisModuleRotateInstance = SFXTelekinesisModuleRotate.Spawn(itemTargeted.transform.position, Quaternion.identity);
                    }
                    else
                    {
                        SFXTelekinesisModuleRotateInstance = SFXTelekinesisModuleRotate.Spawn(creatureTargeted.transform.position, Quaternion.identity);
                    }
                    SFXTelekinesisModuleRotateInstance.Play();
                }
                if (!Hand.playerHand.controlHand.gripPressed && gripWasPressed)
                {
                    gripWasPressed ^= true;
                }
            }
        }

        public IEnumerator SoundLoopPlaying()
        {
            SFXTelekinesisModuleStartInstance = SFXTelekinesisModuleStart.Spawn(item.transform.position, Quaternion.identity);
            SFXTelekinesisModuleStartInstance.Play();
            yield return new WaitForSeconds(startSFX.sounds[0].length);
            if (Activated && !soundLoop)
            {
                SFXTelekinesisModuleLoopInstance = SFXTelekinesisModuleLoop.Spawn(item.transform.position, Quaternion.identity, item.transform);
                SFXTelekinesisModuleLoopInstance.Play();
                soundLoop = true;
            }
            yield return null;
        }
    }
}
