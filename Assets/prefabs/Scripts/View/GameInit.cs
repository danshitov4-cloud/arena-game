using UnityEngine;

public class GameInit : MonoBehaviour
{
    public GameObject buildingPrefab; // сюда перетащи Building_Base

    void Awake()
    {
        BuildingService.buildingPrefab = buildingPrefab;
    }
}
