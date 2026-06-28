using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SortingGroup))]
public class SortByY : MonoBehaviour
{
    public int precision = 100;
    SortingGroup sg;

    void Awake() => sg = GetComponent<SortingGroup>();

    void LateUpdate()
    {
        // Ниже по Y -> БОЛЬШЕ order -> рисуется поверх
        int order = -Mathf.RoundToInt(transform.position.y * precision);
        sg.sortingOrder = order;
        // Debug: посмотри, что получится: -1 => 100, -0.5 => 50, 0.1 => -10
        // Debug.Log($"{name}: Y={transform.position.y} → Order={order}");
    }
}
