using Scripts.Help.ScriptableObjects;
using Scripts.Help.ScriptableObjects.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Plugins.ScriptableObjects
{
    [CustomEditor(typeof(GameEvent))]
    internal class EventEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUI.enabled = Application.isPlaying;

            GameEvent e = target as GameEvent;
            if (GUILayout.Button("Raise"))
                e.Raise();
        }
    }
}
