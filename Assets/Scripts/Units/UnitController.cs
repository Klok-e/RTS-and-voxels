using Scripts.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Scripts.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PathfinderAgentController))]
    public class UnitController : MonoBehaviour, IPointerClickHandler
    {
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                isSelected = value;
                if (value)
                    selectedUnitsContainer.Register(this);
                else
                    selectedUnitsContainer.UnRegister(this);

                selectedUnitHighlighter.SetActive(value);
            }
        }

        private bool isSelected;

        [SerializeField]
        private SelectedUnitsContainer selectedUnitsContainer;

        [SerializeField]
        private GameObject selectedUnitHighlighter;

        private PathfinderAgentController _pathfinder;

        #region MonoBehaviour implementation

        private void Start()
        {
            IsSelected = false;
            _pathfinder = GetComponent<PathfinderAgentController>();
        }

        #endregion MonoBehaviour implementation

        #region IPointerClickHandler implementation

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == 0)
                IsSelected = true;
        }

        #endregion IPointerClickHandler implementation

        public void Move(Vector3 destination)
        {
            _pathfinder.MoveTo(destination);
        }
    }
}
