using UnityEngine;

public class BuildingService : MonoBehaviour
{
    public static GameObject buildingPrefab;
    public static void Build(Vector3 position)
    {
        if (!buildingPrefab)
        {
            Debug.LogError("BuildingServicr=e:не назначен");

            return;
        }
        //сщзлаем объект на сцене 
        var go = Object.Instantiate(buildingPrefab, position, Quaternion.identity);
        var view = go.GetComponent<BuildingView>();

        // Подписка на событие: что делать, когда стройка закончится
        view.onBuilt += () =>
        {
            var sr = view.transform.Find("Body").GetComponent<SpriteRenderer>();
            sr.color = Color.white; // пример: меняем цвет на «готово»
            Debug.Log($"{go.name} построено!");
        };
    }
}

