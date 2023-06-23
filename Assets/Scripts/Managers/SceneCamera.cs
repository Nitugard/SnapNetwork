using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Ibc.Game
{
    public class SceneCamera : MonoBehaviour
    {
        public UnityEvent OnCameraEnable;
        public UnityEvent OnCameraDisable;
        
        public string Name => _name;
        
        /// <summary>
        /// Reference to the internal unity camera.
        /// </summary>
        [SerializeField]
        protected Camera Camera;

        /// <summary>
        /// Camera name.
        /// </summary>
        [SerializeField]
        private string _name;


        /// <summary>
        /// Whether the camera is enabled;
        /// </summary>
        public bool IsEnabled => gameObject.activeSelf;
        
        protected virtual void Awake()
        {
            if (SceneCameraManager.Instance != null)
                SceneCameraManager.Instance.Register(this);
        }

        protected virtual void OnDestroy()
        {
            if (SceneCameraManager.Instance != null)
                SceneCameraManager.Instance.UnRegister(this);
        }


        protected virtual void OnEnable()
        {
            OnCameraEnable.Invoke();
        }
        
        protected virtual void OnDisable()
        {
            OnCameraDisable.Invoke();
        }

        public Camera GetCamera()
        {
            return Camera;
        }
    }
}
