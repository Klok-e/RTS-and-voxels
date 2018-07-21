using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects.Containers
{
    [CreateAssetMenu(menuName = "Containers/IntContainer")]
    internal class IntContainer : ScriptableObject
    {
        public int _value;
        public int Value { get { return _value; } }
    }
}
