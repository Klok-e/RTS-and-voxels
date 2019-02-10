using Scripts.Pathfinding;
using UnityEngine;

namespace Scripts.Units
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PathfinderAgentController))]
    public class UnitController : MonoBehaviour
    {
        [SerializeField]
        private GameObject selectedUnitHighlighter;

        private PathfinderAgentController _pathfinder;

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
