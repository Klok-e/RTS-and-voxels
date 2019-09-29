using System.Collections.Generic;
using Units;
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

        public Team(int index)
        {
            _units = new List<UnitController>();
            Index  = index;
        }

        public int Index { get; }
    }
}