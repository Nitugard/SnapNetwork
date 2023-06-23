using System;
using System.Collections.Generic;

namespace Ibc.Game
{
    public class SceneCameraManager : SceneSingleton<SceneCameraManager>
    {

        private SceneCamera _fallbackCamera;

        private Dictionary<string, SceneCamera> _sceneCameras = new Dictionary<string, SceneCamera>();
        private List<SceneCamera> _cameras = new List<SceneCamera>();

        private SceneCamera _activeCamera;


        public bool TryGetSceneCamera(string name, out SceneCamera sceneCamera)
        {
            return _sceneCameras.TryGetValue(name, out sceneCamera);
        }

        internal bool Register(SceneCamera cam)
        {
            if (_sceneCameras.TryAdd(cam.Name, cam))
            {
                if (_activeCamera == null)
                {
                    SwitchCamera(cam);
                    _fallbackCamera = cam;
                }

                _cameras.Add(cam);
                return true;
            }
            
            return false;
        }
        
        internal bool UnRegister(SceneCamera cam)
        {
            if (_activeCamera == cam)
                SwitchCamera(_fallbackCamera);
            
            if (_sceneCameras.Remove(cam.Name))
            {
                _cameras.Remove(cam);
                return true;
            }

            return false;
            
        }
        
        public bool SwitchCamera(string name)
        {
            if (TryGetSceneCamera(name, out var cam))
            {
                SwitchCamera(cam);
                return true;
            }
            
            return false;
        }

        public void SwitchCamera(SceneCamera cam)
        {
            if (cam == null)
                throw new NullReferenceException(nameof(cam));
            if(_activeCamera == cam)
                return;
            
            if(_activeCamera != null)
                _activeCamera.gameObject.SetActive(false);
            
            cam.gameObject.SetActive(true);
            _activeCamera = cam;
        }

        public void SwitchCameraToFallback()
        {
            SwitchCamera(_fallbackCamera);
        }
    }
}
