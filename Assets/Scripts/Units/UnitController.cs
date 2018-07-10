using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Scripts.Units
{
    public class UnitController : MonoBehaviour, IPointerClickHandler
    {
        private bool isSelected;

        #region MonoBehaviour implementation

        private void Start()
        {
            isSelected = false;
        }

        #endregion MonoBehaviour implementation

        #region IPointerClickHandler implementation

        public void OnPointerClick(PointerEventData eventData)
        {
            isSelected = true;
        }

        #endregion IPointerClickHandler implementation
    }
}
