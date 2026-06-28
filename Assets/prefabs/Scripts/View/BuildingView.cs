using UnityEngine;
using UnityEngine.UI;
using System; // ← добавь

public class BuildingView : MonoBehaviour
{
    public Image timerFill;
    public float buildDuration = 30f;
    float startTime; bool built;

    public event Action onBuilt; // ← добавь это

    void OnEnable() { startTime = Time.time; built = false; timerFill.fillAmount = 0; }

    void Update()
    {
        if (built) return;
        float t = Mathf.Clamp01((Time.time - startTime) / buildDuration);
        timerFill.fillAmount = t;

        if (t >= 1f)
        {
            built = true;
            timerFill.transform.parent.gameObject.SetActive(false);
            onBuilt?.Invoke(); // ← добавь это
        }
    }
}