using DWS.Common.InjectionFramework;
using Harmony;
using System;
using System.Reflection;
using UBOAT.Game;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.UI.DevConsole;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SinkingPhysicsOverhaul
{

    /**
     * Loader of Sinking Physics Mod. 
     * Implements IUserMode interface and utilizes Harmony.
     * 
     * Author: ждун 
     * Modified by: ii and salamander
     **/
    [NonSerializedInGameState]
    public class SinkingPhysics : IUserMod
    {        

        public void OnLoaded()
        {
            try
            {
                Debug.Log($"{Constants.MOD_TAG} version: {Constants.VERSION}");                
                HarmonyInstance harmony = HarmonyInstance.Create("com.dws.uboat");
                harmony.PatchAll();
                DevConsole.AddListener("TargetInfo", ConsoleCommands.TargetInfo);
                DevConsole.AddListener("TargetRotation", ConsoleCommands.TargetRotation);
                DevConsole.AddListener("TorpedoTarget", ConsoleCommands.TorpedoTarget);
                DevConsole.AddListener("HPBar", ConsoleCommands.SwitchHPBar);
                DevConsole.AddListener("Observations", ConsoleCommands.PrintObservations);

                Debug.LogFormat($"{Constants.MOD_TAG} " + "HPBar: {0}", Settings.hpBar.ToString());

                SceneEventsListener.OnSceneAwake += SceneEventsListener_OnSceneAwake;
            }
            catch (Exception e)
            {
                Debug.LogError($"{Constants.MOD_TAG} ERROR ");
                Debug.LogException(e);
            }
        }

       private static void SceneEventsListener_OnSceneAwake(Scene scene)
        {

            try
            {
                InjectionFramework.Instance.InjectIntoAssembly(Assembly.GetExecutingAssembly());
            } catch(Exception e)
            {
                Debug.LogErrorFormat($"{Constants.MOD_TAG} ERROR!");
                Debug.LogException(e);
            }
        } 
    }
}

