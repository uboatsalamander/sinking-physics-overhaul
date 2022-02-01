
using DWS.Common.InjectionFramework;
using System;
using System.Collections.Generic;
using UBOAT.Game.Core;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Sandbox;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Selectables;
using UnityEngine;

namespace SinkingPhysicsOverhaul
{
    public class ConsoleCommands
    {

        [Inject]
        private static PlayerShip playerShip;

        [Inject]
        private static SelectionManager selections;

        [Inject]
        private static Sandbox sandbox;

        [Inject]
        private static SandboxPlayerWolfpack wolfpack;

        public static void SwitchHPBar(string[] arguments)
        {
            try
            {
                if (arguments.Length == 2)
                {
                    bool newState = false;
                    if (bool.TryParse(arguments[1], out newState))
                    {
                        Settings.hpBar = newState;
                    }
                    else throw new ArgumentException(string.Format("Invalid argument {0}", arguments[1]));
                } else throw new ArgumentException(string.Format("Invalid number of arguments. "));
            } catch(ArgumentException ae)
            {
                Debug.LogErrorFormat($"{Constants.MOD_TAG} " + "{0}", ae.Message);
                Debug.LogFormat($"{Constants.MOD_TAG} " + "Use: HPBar true  - to activate hp bar");
                Debug.LogFormat($"{Constants.MOD_TAG} " + "Use: HPBar false - to deactivate hp bar");
            } catch (NullReferenceException nre)
            {
                Debug.LogErrorFormat($"{Constants.MOD_TAG} " + "ERROR! Unable to load settings controller.");
            }
        }

        public static void PrintObservations(string[] arguments)
        {
            List<DirectObservation> observations = playerShip.GetObservationsDirect();
            int count = observations.Count;
            for (int index = 0; index < count; ++index)
            {
                DirectObservation observation = observations[index];
                Debug.LogFormat("id:{0} pname:{1}, lost:{2}", observation.Entity.GetInstanceID(), observation.PerceivedName, observation.IsLost);
            }

        }

        public static void TargetInfo(string[] arguments)
        {
            List<ISelectable> selection = selections.Selection;
            if(selection.Count == 1)
            {
                Entity entity = (Entity)selection[0];
                if(entity is Ship)
                {
                    Ship ship = (Ship)entity;
                    Hull hull = entity.Hull;
                    Rigidbody rigidBody = hull.ParentEntity.GetComponent<Rigidbody>();
                    

                    string shipName = ship.name;
                    Debug.LogFormat("shipName {0}", shipName);

                    string category = ship.Category;
                    Debug.LogFormat("category {0}", category);

                    EntityType shipType = ship.Blueprint.Type;
                    Debug.LogFormat("shipType {0}", shipType.LocalizedName);

                    Country country = ship.Blueprint.Country;
                    Debug.LogFormat("country {0}", country.CountryCode);
                
                    Parameter crewCount = ship.Blueprint.CrewCount;
                    Debug.LogFormat("crewCount {0}", crewCount.Value);

                    Parameter length = ship.Blueprint.Length;
                    Debug.LogFormat("length {0}", length.Value);

                    Parameter beam = ship.Blueprint.Beam;
                    Debug.LogFormat("beam {0}", beam.Value);

                    Parameter drought = ship.Blueprint.Draught;
                    Debug.LogFormat("drought {0}", drought.Value);

                    Parameter velocity = ship.Blueprint.Velocity;
                    Debug.LogFormat("velocity {0}", velocity.Value);

                    float mass = ship.Mass.Value;
                    Debug.LogFormat("mass {0:0.##}", mass);

                }
            }            
        }

        public static void TargetPosition(string[] arguments)
        {
            List<ISelectable> selection = selections.Selection;
            if (selection.Count == 1)
            {
                Entity entity = (Entity)selection[0];
                Rigidbody rigidBody = entity.GetComponent<Rigidbody>();
                Vector3 position = rigidBody.position;
                Debug.LogFormat("position: {0}", position.ToString("F3"));
            }
        }

        public static void TargetRotation(string[] arguments)
        {
            List<ISelectable> selection = selections.Selection;
            if (selection.Count == 1)
            {
                Entity entity = (Entity)selection[0];
                Rigidbody rigidBody = entity.GetComponent<Rigidbody>();
                Quaternion rotation = rigidBody.rotation;                
                Vector3 eulerAngles = rotation.eulerAngles;
                Debug.LogFormat("rotation: {0}", eulerAngles.ToString("F3"));
            }
        }
             
        public static void TorpedoTarget(string [] arguments)
        {
            List<ISelectable> selection = selections.Selection;
            if (selection.Count == 1)
            {
                Entity entity = (Entity)selection[0];
                SandboxEntity sandboxEntity = SandboxEntity.Create("G7e Torpedo T3 - Pi2", ConsoleCommands.sandbox.NeutralCountry);
                sandboxEntity.SpawnsInstantly = true;
                ConsoleCommands.wolfpack.AddEntity(sandboxEntity);
                sandboxEntity.UpdatePosition();
                Vector3 position = entity.transform.position;
                if (entity is Ship)
                    position += entity.transform.forward.MaskY(0.0f) * (((Ship)entity).AverageVelocity * 15f);
                StoredTorpedo component = sandboxEntity.SpawnedEntity.GetComponent<StoredTorpedo>();
                component.transform.position = position + Quaternion.AngleAxis(UnityEngine.Random.value * 360f, Vector3.up) * new Vector3(0.0f, 0.0f, 270f);
                Vector3 normalized = (position - component.transform.position).MaskY(0.0f).normalized;
                component.transform.rotation = Quaternion.AngleAxis(Mathf.Atan2(normalized.x, normalized.z) * 57.29578f, Vector3.up);
                component.DudSeed = 1f;
                component.Launch((Ship)ConsoleCommands.playerShip, entity.transform, position, position, 0, (float)component.Speed1);
                Debug.LogFormat("Launched {0} at {1}.", (object)component.name, (object)entity.name);
            }
        }

    }
}
