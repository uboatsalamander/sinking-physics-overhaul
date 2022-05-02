
using DWS.Common.InjectionFramework;
using DWS.Common.Resources;
using DWS.Common.Tweening;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Effects;
using UnityEngine;
using UnityEngine.Bindings;

namespace SinkingPhysicsOverhaul
{
    public class HullPatches
    {

        public static float fullStopPitchPositiveThreshold = 3f;
        public static float fullStopPitchNegativeThreshold = 6f;
        public static float fullStopRollThreshold = 16f;
        public static float forceSinkPitchThreshold = 28f;
        public static float forceSinkRollThreshold = 50f;
        public static float forceSinkDamage = 0.1f;


        [Inject]
        private static ResourceManager resourceManager;

        private static string escapingAirFoamPrefabPath = "Effects/Torpedo Water Gush";

        [HarmonyPatch(typeof(Hull), "Start")]
        public class Start_Patch
        {
            private static void Postfix(ref float ___sinkThreshold, ref float ___compartmentCapacity, ref float ___floodThreshold, Vector3 ___max, ref Vector3 ___initialCenterOfMass, Hull __instance)
            {

                if (ignoreEntities(__instance.ParentEntity.name)) return;
                // Debug.LogFormat($"{Constants.MOD_TAG}"+"{0} spawned.", __instance.ParentEntity.name);

                Rigidbody rigidBody = __instance.ParentEntity.GetComponent<Rigidbody>();

                ___floodThreshold = 0.4f;
                //___sinkThreshold = 0.75f; // Do not change this for now as it seems some ships were not able to reach threshold to sink reliably.
                //___compartmentCapacity *= 0.75f;

                if (__instance.ParentEntity.Name.Contains("Ol-Class")) // Lower the sink threshold for the Ol-Class as they have huge buoyancy reserve in the game physics model and are very hard to sink.
                {
                    ___sinkThreshold = 0.5f;
                    ___compartmentCapacity *= 1.1f; // Also slightly increase compartment capacity.
                    Debug.LogFormat("{0} Ol-Class sink threshold reduced!", System.DateTime.Now.ToString("HH:mm:ss.ff"));
                }

                if (___initialCenterOfMass.y <= __instance.ParentEntity.GetComponent<Rigidbody>().centerOfMass.y)
                {
                    float delta = ___max.y - ___initialCenterOfMass.y;
                    ___initialCenterOfMass.y += delta * UnityEngine.Random.Range(0.04f, 0.12f);
                }

                HullPatches.resourceManager.RetrieveAssetAsync<GameObject>(HullPatches.escapingAirFoamPrefabPath, (object)__instance, new Action<Resource<GameObject>>(HullPatches.OnEffectLoaded));
            }
        }

        [HarmonyPatch(typeof(Hull), "AddDamage")]
        public class AddDamage_Patch
        {
            private static void Prefix(float damage, ref float radius, bool isTorpedoImpact, Hull __instance)
            {                
                if (ignoreEntities(__instance.ParentEntity.name)) return;

                if(!__instance.ParentEntity.Blueprint.Type.IsMilitary) // Reduce damage to merchants.
                    damage *= 0.65f;

                if (!isTorpedoImpact) // Reduce damage radius of shells.
                {
                    radius *= 0.45f;
                }                
            }
        }

        [HarmonyPatch(typeof(Hull), "Update")]
        public class Update_Patch
        {
            private static void Prefix(Vector3 ___totalCenterOfMass, out Vector3 __state)
            {
                __state = ___totalCenterOfMass;
            }

            /*
             * increases compartment center of mass vector magnitude by factorizing linear interpolation between its center and flood level vector.
             * */
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);

                for (var i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand.Equals(0.15f))
                    {
                        codes[i].operand = 0.021f;
                        break;
                    }
                }

                for (var i = 0; i < codes.Count; i++)
                {

                    if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand.ToString().Contains("RemoveAt"))
                    {
                        codes.Insert(i + 22, new CodeInstruction(OpCodes.Ldc_R4, 2.0f));
                        codes.Insert(i + 23, new CodeInstruction(OpCodes.Mul));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }

            private static void Postfix(ref Vector3 ___totalCenterOfMass, int ___compartmentsX, int ___compartmentsY, 
                int ___compartmentsZ, float ___maxCompartmentDamage, ref float ___floodThreshold, Hull.ShipCompartment[] ___compartments, NonPlayableCrew ___crew,
                Hull __instance, Vector3 __state)
            {
                //Debug.Log("ParentEntity.name = " + __instance.ParentEntity.name + " / category = " + __instance.ParentEntity.Blueprint.Type.CategoryString);
                if (ignoreEntities(__instance.ParentEntity.name)) return;
                //if (__instance.ParentEntity is Ship && ignoreEntities(__instance.ParentEntity.name)) return;
                try {
                    Ship ship = (Ship)__instance.ParentEntity;
                    int instanceID = __instance.GetInstanceID();

                    // calculate orientation angles for rigid body of the ship
                    Vector3 angles = __instance.ParentEntity.GetComponent<Rigidbody>().rotation.eulerAngles;

                    // variate total center of mass vector magnitude by a random factor for non military ships                
                    if (!ship.Blueprint.Type.IsMilitary && !___totalCenterOfMass.Equals(__state))
                    {
                        float deltaZ = Mathf.Abs(___totalCenterOfMass.z) - Mathf.Abs(__state.z);
                        deltaZ *= UnityEngine.Random.Range(-0.1f, 0.1f);

                        if (___totalCenterOfMass.z > 0)
                            ___totalCenterOfMass.z += deltaZ;
                        else if (___totalCenterOfMass.z < 0)
                            ___totalCenterOfMass.z -= deltaZ;
                    }

                    Engines activeEngines = ship.ActiveEngines;
                    Modifier enginesDisabledModifier = __instance.ParentEntity.SandboxEntity.Velocity.GetScaleModifier("Engines Disabled", false);
                    Rigidbody rigidbody = __instance.ParentEntity.GetComponent<Rigidbody>();
                    float pitch = rigidbody.rotation.eulerAngles.x;
                    if (pitch >= 180f) pitch = pitch - 360f;
                    float roll = rigidbody.rotation.eulerAngles.z;
                    if (roll >= 180f) roll = roll - 360f;

                    // Condition for engine stop when the ship pitcher or rolls too much.
                    // Added additional condition that ship actually must be damaged to prevent engine shutdown just from the too high pitch/roll caused by the rough seas.
                    if (ship.ReceivedDamages.Count > 0 && __instance.Health < 0.8f && (pitch > fullStopPitchPositiveThreshold || pitch < -fullStopPitchNegativeThreshold || roll > fullStopRollThreshold || roll < -fullStopRollThreshold))
                    {
                        /*Rudder[] rudders = ship.Rudders; // Not touching the rudders for now, might be left for different condition.
                        for (int i = 0; i < rudders.Length; i++)
                        {
                             rudders[i].Locked = true;
                        }*/

                        __instance.ParentEntity.SandboxEntity.OnCrippled(); // Setting the unit to crippled will detach it from the group and sets its speed to 0, so it will not travel on the map anymore. Disabling of the engines is however still needed.
                        if (enginesDisabledModifier == null) __instance.ParentEntity.SandboxEntity.Velocity.AddScaleModifier("Engines Disabled", false); // Modifier that stores the disabled condition is attached to SandboxEntity as it looks like the modifier attached directly to the engines is not preserved after leaving the scene.
                        //if (!___crew.Evacuation) ___crew.StartEvacuation(); // Evacuation could be triggered as well but will be probably used in some other condition.
                    }

                    // This is to make sure that once the engines were disabled and the ship even right itself after playership leaves the scene or you reload the game, they will be re-disabled.                    
                    if (enginesDisabledModifier != null && activeEngines != null)
                    {
                        activeEngines.PowerModifier = 0f;
                        // Secondary method to disable the engines by adding second scale modifier, which the game do not know about (and therefore will not change it) and will set the power permanently to 0. This will disable the engines also under 140x TC.
                        Modifier powerModifier = activeEngines.Power.GetScaleModifier("Engines Disabled", false);
                        if (powerModifier == null)
                        {
                            powerModifier = activeEngines.Power.AddScaleModifier("Engines Disabled", false);
                            powerModifier.Value = 0f;
                        }
                    }

                    // Evaluate angles and force sink, additionally force sink when health dropped to 0 which should facilitate fast sinking after hull break. If the force sinking was once initiated, ensure that it will continue no matter the angles.
                    Modifier forcedSinkingModifier = __instance.ParentEntity.SandboxEntity.Velocity.GetScaleModifier("Forced Sinking", false);
                    if (forcedSinkingModifier != null || __instance.ParentEntity.Health < 0.01f || __instance.Health < 0.01f || ship.ReceivedDamages.Count > 0 && __instance.Health < 1f && (pitch > forceSinkPitchThreshold || pitch < -forceSinkPitchThreshold || roll > forceSinkRollThreshold || roll < -forceSinkRollThreshold))
                    {
                        if (forcedSinkingModifier == null) __instance.ParentEntity.SandboxEntity.Velocity.AddScaleModifier("Forced Sinking", false);

                        float maxDmg = 0;
                        int maxDmgZ = 0;
                        int maxDmgY = 0;
                        int maxDmgX = 0;

                        // Search the most damaged but not yet destroyed compartement.
                        for (int z = 0; z < ___compartmentsZ; z++)
                        {
                            for (int x = 0; x < ___compartmentsX; x++)
                            {
                                for (int y = 0; y < ___compartmentsY; y++)
                                {
                                    //if (UnityEngine.Random.Range(0f, 1f) > 0.99f) __instance.DamageCompartment(x, y, z, forceSinkDamage * Time.deltaTime); // Small chance to introduce damage to other compartments to "seed" new point from which damage can propagate.
                                    float compDamage = __instance.GetCompartmentDamage(x, y, z);
                                    if (compDamage > maxDmg && compDamage < ___maxCompartmentDamage) // Additional condition to exclude fully destroyed compartments to ensure propagation thru the hull.
                                    {
                                        maxDmg = compDamage;
                                        maxDmgX = x;
                                        maxDmgY = y;
                                        maxDmgZ = z;
                                    }

                                    if (compDamage > ___maxCompartmentDamage - 0.1f && compDamage < ___maxCompartmentDamage) // Print out the damaged compartment info just before destruction.
                                    {
                                        int index = y * ___compartmentsZ * ___compartmentsX + x * ___compartmentsZ + z;
                                        //Debug.LogFormat("{0} [SPO] {1} Destroying compartement XYZ {2:0},{3:0},{4:0}, Damage:{5:0.00}, Ballast:{6:0.00}", System.DateTime.Now.ToString("HH:mm:ss.ff"), __instance.ParentEntity.name, x, y, z, ___compartments[index].damage, ___compartments[index].ballast);
                                    }
                                }
                            }
                        }

                        // Destroy compartements in same section with the compartement having heaviest damage, if it was not yet destroyed.
                        float currentDamage = __instance.GetCompartmentDamage(maxDmgX, maxDmgY, maxDmgZ);
                        if (currentDamage < ___maxCompartmentDamage)
                        {
                            __instance.DamageCompartments(maxDmgZ, forceSinkDamage * Time.deltaTime);
                        }
                            
                        // Destroy compartements previous section if they are not yet destroyed.
                        if (maxDmgZ - 1 >= 0)
                        {
                            currentDamage = __instance.GetCompartmentDamage(maxDmgX, maxDmgY, maxDmgZ - 1);
                            if (currentDamage < ___maxCompartmentDamage)
                            {
                                __instance.DamageCompartments(maxDmgZ - 1, forceSinkDamage * Time.deltaTime);
                            }
                        }

                        // Destroy compartements in next section if they are not yet destroyed.
                        if (maxDmgZ + 1 < ___compartmentsZ)
                        {
                            currentDamage = __instance.GetCompartmentDamage(maxDmgX, maxDmgY, maxDmgZ + 1);
                            if (currentDamage < ___maxCompartmentDamage)
                            {
                                __instance.DamageCompartments(maxDmgZ + 1, forceSinkDamage * Time.deltaTime);
                            }
                        }

                        // Drop flooding threshold.
                        if (___floodThreshold > 0.021f)
                        {
                            ___floodThreshold = 0.021f; // It seems it must be specifically 0.021f, which is the lowest value that will not result in negative result after substraction in vanilla code.
                        }

                    }

                }
                catch (Exception e) {
                    Debug.Log("We would have crashed");
                    return;
                }

            }
        }

        public static bool ignoreEntities(string entityName)
        {
            string[] ignored = {"Pontoon", "Torpedo", "Mine", "Buoy", "Wreckage" , "Checkpoint", "Gun", "Empty", "Port",
                "Air Base", "Aircraft", "Submarine Pen", "Submarine", "Scene", "Sonar Decoy", "Campaign", "Iceberg", "Marine Animal" }; //this may be an issue with UBE
            return Array.Exists(ignored, e => entityName.Contains(e));
        }

        private static void OnEffectLoaded(Resource<GameObject> effectResource)
        {
        }

        /* [HarmonyPatch(typeof(HullCrack), "AdjustFinalMass")]
         public class HullCrackAdjustFinalMassPatch
         {
             private static bool Prefix(Rigidbody rigidBody)
             {
                 Debug.LogFormat("{0} [SPO] Skipping final mass adjustment on hull crack!", System.DateTime.Now.ToString("HH:mm:ss.ff"));
                 return false;
             }

             private static void Postfix(Rigidbody rigidBody)
             {
                 rigidBody.mass *= 0.8f; 
             }
         }*/

         [HarmonyPatch(typeof(HullCrack), "RelaxBreakJoint")]  // Experimental stuff to improve the mechanics of hull breaking where one part fell apar instantly whgile the other part floated for a long time
         public class HullCrackRelaxBreakJointPatch
         {
             private static bool Prefix(float f, SpringJoint ___breakJoint, Hull ___hull)
             {
                if (___breakJoint)
                {
                   if (___hull != null)
                   {
                        Rigidbody rigidBody = ___hull.ParentEntity.GetComponent<Rigidbody>();
                        if (rigidBody != null)
                        {
                            ___breakJoint.damper = ___breakJoint.spring / 2f;
                            ___breakJoint.tolerance = 0.2f;
                            ___breakJoint.maxDistance = 0.5f;
                            /*___breakJoint.spring = rigidBody.mass * 2f;
                            ___breakJoint.autoConfigureConnectedAnchor = false;
                            ___breakJoint.anchor.Set(0f, 2f, 3f);
                            ___breakJoint.connectedAnchor.Set(0f, 0f, -3f);*/
                            //Debug.LogFormat("{0} [SPO] Spring break joint {1:0.00} {2} {3}", System.DateTime.Now.ToString("HH:mm:ss.ff"), ___breakJoint.spring, ___breakJoint.anchor.ToString("F1"), ___breakJoint.connectedAnchor.ToString("F1"));
                        }
                    }
                }
                return false;
             }
         }

        [HarmonyPatch(typeof(HullCrack), "OnBreakJointRelaxed")] // This prevents the spring joints being destroyed  
        public class HullCrackOnBreakJointRelaxedPatch
        {
            private static bool Prefix(SpringJoint ___breakJoint)
            {
                Debug.LogFormat("{0} [SPO] Keeping the break joint connected!", System.DateTime.Now.ToString("HH:mm:ss.ff"));
                return false;
            }
        }





    }
}
