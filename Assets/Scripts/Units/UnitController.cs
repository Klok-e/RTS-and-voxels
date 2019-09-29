using Pathfinding;
using UnityEngine;

namespace Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PathfinderAgentController))]
    public class UnitController : MonoBehaviour
    {
        private PathfinderAgentController _pathfinder;

        [SerializeField]
        private GameObject selectedUnitHighlighter;

        #region MonoBehaviour implementation

        private void Start()
        {
            _pathfinder = GetComponent<PathfinderAgentController>();
        }

        #endregion MonoBehaviour implementation

        public void SetSelection(bool set)
        {
            selectedUnitHighlighter.SetActive(set);
        }

        public void Move(Vector3 destination)
        {
            //_pathfinder.MoveTo(destination);
        }
    }
}