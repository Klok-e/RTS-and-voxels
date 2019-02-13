using Scripts.Units;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeamManagerController : MonoBehaviour
{
    private List<Team> teams;

    private void Start()
    {
        teams = new List<Team>();
        AddTeam(1);
        AddTeam(2);
    }

    private void AddTeam(int index)
    {
        teams.Add(new Team(index));
    }

    private class Team
    {
        public List<UnitController> _units;

        public int Index { get; }

        public Team(int index)
        {
            _units = new List<UnitController>();
            Index = index;
        }
    }
}
