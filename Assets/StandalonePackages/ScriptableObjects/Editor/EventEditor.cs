using ScriptableObjects.Events;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjects
{
    [CustomEditor(typeof(GameEvent))]
    internal class EventEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUI.enabled = Application.isPlaying;

            GameEvent e = target as GameEvent;
            if(GUILayout.Button("Raise"))
                e.Raise();
        }
    }
}
