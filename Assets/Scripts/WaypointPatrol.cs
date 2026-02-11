using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class WaypointPatrol : MonoBehaviour
{
    public NavMeshAgent navMeshAgent;
    public List<Transform> waypoints = new();

    int m_CurrentWaypointIndex;

    void Awake()
    {
        if (waypoints == null)
        {
            waypoints = new List<Transform>();
        }
    }

    public void StartAI()
    {
        if (waypoints.Count > 0 && navMeshAgent != null)
        {
            navMeshAgent.SetDestination(waypoints[0].position);
        }
    }

    void Update()
    {
        if (waypoints == null || waypoints.Count == 0 || navMeshAgent == null) return;

        if (navMeshAgent.remainingDistance < navMeshAgent.stoppingDistance)
        {
            m_CurrentWaypointIndex = (m_CurrentWaypointIndex + 1) % waypoints.Count;
            navMeshAgent.SetDestination(waypoints[m_CurrentWaypointIndex].position);
        }
    }
}