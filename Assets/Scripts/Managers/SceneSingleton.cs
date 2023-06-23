using System;
using UnityEngine;

namespace Ibc.Game
{
    public abstract class SceneSingleton<T> : MonoBehaviour where T : Component
    {


        /// <summary>
        /// Cached instance.
        /// </summary>
        private static T _instance;
        
        /// <summary>
        /// Search for instance in the scene and return it. Cached.
        /// </summary>
        /// <value>The instance.</value>
        public static T Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<T>();

                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                Debug.Log(_instance);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                Debug.LogError($"Instance of type {typeof(T).Name} already present in the scene");
            }
        }
    }
}
