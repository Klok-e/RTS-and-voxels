using UnityEngine;
using UnityEngine.Events;

namespace UI
{
    public class ButtonPressedEventInvokerController : MonoBehaviour
    {
        public KeyCode    button;
        public UnityEvent toCallWhenButtonPressed;

        private void Update()
        {
            if (Input.GetKeyDown(button)) toCallWhenButtonPressed.Invoke();
        }
    }
}