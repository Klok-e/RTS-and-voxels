using Scripts.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Units
{
    public class UnitManagerController : MonoBehaviour
    {
        [SerializeField]
        private SelectedUnitsContainer selectedUnits;

        [SerializeField]
        private Camera trackedCamera;

        private void Update()
        {
            if (Input.GetMouseButtonDown(1))
            {
                var ray = trackedCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit))
                {
                    foreach (var unit in selectedUnits.UnitsSelected)
                    {
                        unit.Move(hit.point + (hit.normal * VoxelWorldController._blockSize / 2f));
                    }
                }
            }
        }
    }
}
