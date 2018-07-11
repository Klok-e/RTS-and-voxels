using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Scripts.Units
{
    [CreateAssetMenu]
    public class SelectedUnitsContainer : ScriptableObject
    {
        public IReadOnlyCollection<UnitController> UnitsSelected
        {
            get
            {
                return unitsSelected.AsReadOnly();
            }
        }

        private List<UnitController> unitsSelected;

        private void Awake()
        {
            unitsSelected = new List<UnitController>();
        }

        public void Register(UnitController unit)
        {
            if (!unitsSelected.Contains(unit))
                unitsSelected.Add(unit);
        }

        public void UnRegister(UnitController unit)
        {
            if (unitsSelected.Contains(unit))
                unitsSelected.Remove(unit);
        }

        public void UnRegisterAll()
        {
            unitsSelected.Clear();
        }
    }
}
