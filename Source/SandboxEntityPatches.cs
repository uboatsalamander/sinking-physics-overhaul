
using DWS.Common.InjectionFramework;
using DWS.Common.Resources;
using DWS.Common.Tweening;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Sandbox;
using UnityEngine;

//using PlayWay.Water; // This probably still cannot be used in steam version of the mod as it was causing 0kB dll in the past. Used for local debug when needed.
//using PlayWay.Water.Extensions;

namespace SinkingPhysicsOverhaul
{
    public class SandboxEntityPatches
    {
        [HarmonyPatch(typeof(SandboxEntity), "GetTooltipContents")]
        public class SandboxEntityGetTooltipContentsPatch
        {
            [Inject] private static PlayerShip playerShip;

            private static void Postfix(SandboxEntity __instance, ref string __result)
            {
                if (__instance.SpawnedEntity && (__instance.Blueprint.Type.Category & EntityTypeCategory.UnnamedObjects) == (EntityTypeCategory)0)
                {
                    DirectObservation observation = playerShip.GetObservation(__instance.SpawnedEntity);
                    if (observation != null)
                    {
                        StringBuilder stringBuilder = new StringBuilder();

                        stringBuilder.AppendLine(string.Format("<line-height=100%>\n"));
                        stringBuilder.AppendLine(string.Format("Type: {0}, {1:0} GRT", __instance.Blueprint.Type.LocalizedName, __instance.GRT));
                        stringBuilder.AppendLine(string.Format("Name: {0}", __instance.Name));
                        Hull hull = __instance.SpawnedEntity.GetEquipment<Hull>();
                        if (hull) stringBuilder.AppendLine(string.Format("Entity name: {0}", hull.ParentEntity.Name));

                        Rigidbody rigidbody = __instance.SpawnedTransform.GetComponent<Rigidbody>();
                        if (rigidbody)
                        {
                            stringBuilder.AppendLine(string.Format("Propulsion: {0:0}, Speed: {1:0.0}kn", __instance.Velocity, Mathf.Sqrt(rigidbody.velocity.x * rigidbody.velocity.x + rigidbody.velocity.z * rigidbody.velocity.z) * 1.943f));
                            float pitch = rigidbody.rotation.eulerAngles.x;
                            if (pitch >= 180f) pitch = pitch - 360f;
                            stringBuilder.AppendLine(string.Format("Pitch: {0:0.0}° ES <-6°..3°> FS > 28°", pitch));
                            float roll = rigidbody.rotation.eulerAngles.z;
                            if (roll >= 180f) roll = roll - 360f;
                            stringBuilder.AppendLine(string.Format("Roll: {0:0.0}° ES <-16°..16°> FS > 50°", roll));
                            stringBuilder.AppendLine(string.Format("Center of mass: {0} m", rigidbody.centerOfMass.ToString("F1")));
                        }

                        if (hull)
                        {
                            stringBuilder.AppendLine(string.Format("Entity health: {0:0}%", hull.ParentEntity.Health * 100));
                            stringBuilder.AppendLine(string.Format("Hull health: {0:0}%, Projected: {1:0}%", hull.Health * 100, hull.ProjectedHealth * 100));
                            stringBuilder.AppendLine(string.Format("Total compartment damage: {0:0.00}", hull.TotalCompartmentDamage));
                            stringBuilder.AppendLine(string.Format("Total mass: {0:0}t", rigidbody.mass / 1000));
                            stringBuilder.AppendLine(string.Format("Water tanked: {0:0}t", hull.TotalBallast / 1000));
                            stringBuilder.AppendLine(string.Format("Compartments XYZ: {0:0},{1:0},{2:0}", hull.CompartmentsX, hull.CompartmentsY, hull.CompartmentsZ));

                            /*NeuralNetworkPhysics component = hull.ParentEntity.GetComponent<NeuralNetworkPhysics>(); // Needs PlayWay.Water.Extensions;
                            if (component)
                            {
                                stringBuilder.AppendLine(string.Format("Buoyancy factor: {0:0.000}", component.Volume * component.BuoyancyIntensity * 999.8f));
                            }
                            WaterPhysics component2 = hull.ParentEntity.GetComponentInChildren<WaterPhysics>();
                            if (component2)
                            {
                                stringBuilder.AppendLine(string.Format("Buoyancy factor2: {0:0.000}", component2.Volume * component2.BuoyancyIntensity * 999.8f));
                            }
                            FastWaterPhysics component3 = hull.ParentEntity.GetComponent<FastWaterPhysics>();
                            if (component3)
                            {
                                stringBuilder.AppendLine(string.Format("Buoyancy factor3: {0:0.000}", component3.Volume * component3.BuoyancyIntensity * 999.8f));
                            }*/
                        }

                        Modifier enginesDisabledModifier = __instance.Velocity.GetScaleModifier("Engines Disabled", false);
                        if (enginesDisabledModifier != null) stringBuilder.AppendLine(string.Format("Engines disabled!"));
                        if (__instance.IsCrippled) stringBuilder.AppendLine(string.Format("Unit is crippled!"));
                        if (__instance.IsPacified) stringBuilder.AppendLine(string.Format("Unit is pacified!"));
                        Modifier forcedSinkingModifier = __instance.Velocity.GetScaleModifier("Forced Sinking", false);
                        if (forcedSinkingModifier != null) stringBuilder.AppendLine(string.Format("Forced sinking!"));

                        __result = __result + stringBuilder.ToString();
                    }
                }
            }
        }
    }
}
