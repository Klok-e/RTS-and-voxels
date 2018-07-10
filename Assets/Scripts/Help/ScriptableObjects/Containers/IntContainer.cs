using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Help.ScriptableObjects.Containers
{
    [CreateAssetMenu]
    internal class IntContainer : ScriptableObject
    {
        public int _value;
        public int Value { get { return _value; } }
    }
}
