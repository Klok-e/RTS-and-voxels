using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects.Containers
{
    [CreateAssetMenu(menuName = "Containers/FloatContainer")]
    internal class FloatContainer : ScriptableObject
    {
        public float _value;
        public float Value { get { return _value; } }
    }
}
