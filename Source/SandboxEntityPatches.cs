
using DWS.Common.InjectionFramework;
using DWS.Common.Resources;
using DWS.Common.Tweening;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Sandbox;
using UnityEngine;

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

                        stringBuilder.AppendLine(string.Format("Type: {0}, {1:0} GRT", __instance.Blueprint.Type.LocalizedName, __instance.GRT));
                        stringBuilder.AppendLine(string.Format("Name: {0}, Crew: {1:0}", __instance.Name, __instance.CrewCount));

                        Rigidbody rigidbody = __instance.SpawnedTransform.GetComponent<Rigidbody>();
                        if (rigidbody)
                        {
                            stringBuilder.AppendLine(string.Format("Vel: {0} XYZ m/s", rigidbody.velocity.ToString("F2")));
                            stringBuilder.AppendLine(string.Format("Rot: {0} XYZ deg", rigidbody.rotation.eulerAngles.ToString("F1")));
                            stringBuilder.AppendLine(string.Format("CoM: {0} XYZ m", rigidbody.centerOfMass.ToString("F1")));
                        }

                        Hull hull = __instance.SpawnedEntity.GetEquipment<Hull>();
                        if (hull)
                        {
                            stringBuilder.AppendLine(string.Format("Health: {0:0.00} Projected: {1:0.00}", hull.Health, hull.ProjectedHealth));
                            stringBuilder.AppendLine(string.Format("Mass: {0:0} Total ballast: {1:0}", rigidbody.mass/1000, hull.TotalBallast/1000));
                            stringBuilder.AppendLine(string.Format("Total compartment damage: {0:0}", hull.TotalCompartmentDamage));
                            stringBuilder.AppendLine(string.Format("Compartments XYZ: {0:0},{1:0},{2:0}", hull.CompartmentsX, hull.CompartmentsY, hull.CompartmentsZ));
                        }

                        __result = stringBuilder.ToString();
                    }
                }
            }
        }
    }
}
