using UnityEngine;
using UnityEngine.Events;

namespace ScriptableObjects.Events
{
    public class GameEventListener : MonoBehaviour
    {
        [Tooltip("Event to register with.")]
        [SerializeField]
        private GameEvent _event;

        [Tooltip("Response to invoke when Event is raised.")]
        [SerializeField]
        private UnityEvent _response;

        private void OnEnable()
        {
            _event.RegisterListener(this);
        }

        private void OnDisable()
        {
            _event.UnregisterListener(this);
        }

        public void OnEventRaised()
        {
            _response.Invoke();
        }
    }
}
