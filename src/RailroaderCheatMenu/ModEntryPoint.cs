using Railroader.Modding;
using UnityEngine;

namespace CAG.Sylvrwynd.RailroaderCheatMenu
{
    public sealed class ModEntryPoint : IModEntryPoint, IWorldLifecycle
    {
        private static GameObject menuObject;
        private IModLogger log;

        public void OnModLoad(IModContext context)
        {
            log = context.Log;
            log.Info("Railroader Cheat / Debug Menu loaded from " + context.ModDirectory);
            RuntimePatches.Install(log);

            if (menuObject == null)
            {
                menuObject = new GameObject("CAG.Sylvrwynd.RailroaderCheatMenu");
                Object.DontDestroyOnLoad(menuObject);
                menuObject.AddComponent<CheatMenuBehaviour>().Initialize(log);
            }
        }

        public void OnWorldLoad()
        {
            CheatMenuBehaviour.SetWorldLoaded(true);
            log?.Info("Railroader Cheat / Debug Menu world hooks ready.");
        }

        public void OnWorldUnload()
        {
            GameActions.ResetSessionOverrides();
            CheatMenuBehaviour.SetWorldLoaded(false);
            log?.Info("Railroader Cheat / Debug Menu world hooks cleared.");
        }

        public void OnModUnload()
        {
            GameActions.ResetSessionOverrides();
            CheatMenuBehaviour.SetWorldLoaded(false);
            RuntimePatches.Uninstall();
            if (menuObject != null)
            {
                Object.Destroy(menuObject);
                menuObject = null;
            }
            log?.Info("Railroader Cheat / Debug Menu unloaded.");
        }
    }
}

