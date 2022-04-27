
using DWS.Common.InjectionFramework;
using DWS.Common.Resources;
using DWS.Common.Tweening;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace SinkingPhysicsOverhaul
{
    public class HullPatches
    {

        public static float fullStopPitchPositiveThreshold = 2.5f;
        public static float fullStopPitchNegativeThreshold = 6f;
        public static float fullStopRollThreshold = 16f;
        public static float forceSinkPitchThreshold = 28f;
        public static float forceSinkRollThreshold = 50f;
        public static float forceSinkDamage = 0.01f;


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
                ___sinkThreshold = 0.75f;
                ___compartmentCapacity *= 0.75f;

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

                // reduce damage to merchants
                if(!__instance.ParentEntity.Blueprint.Type.IsMilitary)
                    damage *= 0.65f;

                if (!isTorpedoImpact)
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
                int ___compartmentsZ, float ___maxCompartmentDamage, ref float ___floodThreshold, Hull.ShipCompartment[] ___compartments, 
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

                    // evaluate angles and lock rudder and force engine cut off                
                    if ((angles.x > fullStopPitchPositiveThreshold && angles.x < (360f - fullStopPitchNegativeThreshold)) || (angles.z > fullStopRollThreshold && angles.z < (360f - fullStopRollThreshold)))
                    {
                        Engines activeEngines = ship.ActiveEngines;
                        Rudder[] rudders = ship.Rudders;
                        for (int i = 0; i < rudders.Length; i++)
                        {
                            Debug.Log("Locking rudder");
                            rudders[i].Locked = true;
                        }

                        if (activeEngines != null)
                        {
                            // better to set power to 0 then disable the engine, as the crew will just reenable it.
                            Debug.Log("Disabling engines");
                            activeEngines.PowerModifier = 0.0f;
                        }
                    }

                    // evaluate angles and force sink
                    if (ship.ReceivedDamages.Count > 0 && ((angles.x > forceSinkPitchThreshold && angles.x < (360f - forceSinkPitchThreshold)) || (angles.z > forceSinkRollThreshold && angles.z < (360f - forceSinkRollThreshold))))
                    {
                        float maxDmg = 0;
                        int maxDmgZ = 0;
                        int maxDmgY = 0;
                        int maxDmgX = 0;

                        // search most damaged compartement
                        for (int z = 0; z < ___compartmentsZ; z++)
                        {
                            for (int x = 0; x < ___compartmentsX; x++)
                            {
                                for (int y = 0; y < ___compartmentsY; y++)
                                {
                                    float compDamage = __instance.GetCompartmentDamage(x, y, z);
                                    if (compDamage > maxDmg)
                                    {
                                        maxDmg = compDamage;
                                        maxDmgX = x;
                                        maxDmgY = y;
                                        maxDmgZ = z;
                                    }

                                }
                            }
                        }

                        //Debug.Log("");

                        // destroy compartements in same section with the compartement having heaviest damage, if it was not yet destroyed.
                        float currentDamage = __instance.GetCompartmentDamage(maxDmgX, maxDmgY, maxDmgZ);
                        if (currentDamage < ___maxCompartmentDamage)
                        {
                            __instance.DamageCompartments(maxDmgZ, forceSinkDamage);
                            Debug.Log("Destroy compartements in same section");
                        }
                            
                        // destroy compartements previous section if they are not yet destroyed
                        if (maxDmgZ - 1 >= 0)
                        {
                            currentDamage = __instance.GetCompartmentDamage(maxDmgX, maxDmgY, maxDmgZ - 1);
                            if (currentDamage < ___maxCompartmentDamage)
                            {
                                __instance.DamageCompartments(maxDmgZ - 1, forceSinkDamage);
                                Debug.Log("Destroy compartements in previous section");
                            }
                                
                        }

                        // destroy compartements in next section if they are not yet destroyed
                        if (maxDmgZ + 1 < ___compartmentsZ)
                        {
                            currentDamage = __instance.GetCompartmentDamage(maxDmgX, maxDmgY, maxDmgZ + 1);
                            if (currentDamage < ___maxCompartmentDamage)
                            {
                                __instance.DamageCompartments(maxDmgZ + 1, forceSinkDamage);
                                Debug.Log("Destroy compartements in next section");
                            }
                                
                        }

                        // drop flooding threshold 
                        if (___floodThreshold > 0.021f)
                        {
                            ___floodThreshold = 0.021f;
                            Debug.Log("Dropping flooding threshold");
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

    }

}
