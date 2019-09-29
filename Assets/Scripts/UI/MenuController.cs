using System;
using UnityEngine;

namespace UI
{
    public class MenuController : MonoBehaviour
    {
        private bool isPaused;

        [SerializeField]
        private GameObject menuGraphic;

        private void Start()
        {
            menuGraphic.SetActive(false);
        }

        public void WhenEscPressed()
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }

        private void Pause()
        {
            if (isPaused)
                throw new Exception();

            isPaused = true;

            menuGraphic.SetActive(true);

            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.Confined;
        }

        private void Resume()
        {
            if (!isPaused)
                throw new Exception();

            isPaused = false;

            menuGraphic.SetActive(false);

            Cursor.visible   = false;
            Cursor.lockState = CursorLockMode.Confined;
        }
    }
}