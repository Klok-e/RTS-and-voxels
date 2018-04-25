using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.ScriptableObjects.Containers
{
    [Serializable]
    public class FloatReference
    {
        [SerializeField] private bool useConstant = false;
        [SerializeField] private float constantValue;
        [SerializeField] private FloatContainer referencedValue;

        public float Value
        {
            get
            {
                return useConstant ? constantValue : referencedValue.Value;
            }
        }
    }
}
