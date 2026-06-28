# Типовые ошибки и решения — ArenaGame

---

## Главное правило

**Исправляй корень проблемы, а не создавай артефакты для исправления старых проблем.**

Артефакт — это код или структура, которые существуют только чтобы компенсировать чужую ошибку, а не решают её. Артефакты накапливаются, скрывают настоящую причину и создают новые проблемы. Перед тем как писать "исправляющий" код — найди где именно данные стали неверными и исправь там.

---

## 1. Пули не летели в сторону клика

**Симптом:** пуля создаётся, но летит всегда по `transform.forward` независимо от позиции мыши.

**Причина:** `Bullet.Initialize()` сохранял направление, но `Start()` вызывался следующим кадром и перезаписывал `_rb.linearVelocity` через `transform.forward`, игнорируя переданное направление.

**Решение:** хранить направление в поле `_direction`. `Start()` применяет `_direction` если оно ненулевое, иначе `transform.forward` как запасной вариант.

```csharp
private Vector3 _direction;

public void Initialize(Vector3 direction, int newDamage) {
    _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
}

private void Start() {
    Vector3 dir = _direction.sqrMagnitude > 0.001f ? _direction : transform.forward;
    _rb.linearVelocity = dir * speed;
}
```

---

## 2. Пули невидимы в URP

**Симптом:** пули спавнятся (Ammo уменьшается), но их не видно.

**Причина:** в `Bullet.prefab` материал не назначен (`fileID: 0` = null). URP не рендерит объект без материала.

**Решение:** создавать материал программно в `Awake()`:

```csharp
var sh = Shader.Find("Universal Render Pipeline/Unlit")
      ?? Shader.Find("Universal Render Pipeline/Lit");
if (sh != null) {
    var mat = new Material(sh);
    mat.SetColor("_BaseColor", Color.yellow);
    GetComponent<MeshRenderer>().material = mat;
}
```

---

## 3. Клик мышью не вызывал стрельбу

**Симптом:** `Mouse.current.leftButton.isPressed` всегда false, Ammo не уменьшается.

**Причина:** прямое чтение устройства через `Mouse.current` требует фокуса Game View. Первый клик даёт фокус, но сам event уже "consumed" и не доходит до кода. Плюс: на объекте Player не было компонента `PlayerInput`.

**Решение:** добавить `PlayerInput` (через Оркестратор `scene.batch.addComponent`) с `Behavior = SendMessages`. Тогда Unity сам роутит нажатие через `OnAttack(InputValue value)` без прямого опроса устройства.

---

## 4. Пули летели в одном направлении (~207–209°)

**Симптом:** клики в разных точках экрана — пули всегда под одним углом.

**Причина (а):** `_mouseScreenPos` кэшировался через standalone `InputAction` с `PassThrough`. Callback `performed` не фаерил пока курсор неподвижен — поле оставалось на начальном значении (центр экрана).

**Причина (б):** Game View имел фиксированное разрешение 1536×2048 (портретное), а `Mouse.current.position` возвращал физические пиксели реального окна. `Camera.ScreenPointToRay` ожидал координаты в пространстве `Screen.width × Screen.height` — несовпадение давало одну и ту же точку на виртуальном экране.

**Решение (а):** читать позицию мыши напрямую в момент клика:
```csharp
public void OnAttack(InputValue value) {
    if (!value.isPressed || Time.time < _nextFireTime) return;
    Shoot(Mouse.current.position.ReadValue()); // актуально прямо сейчас
}
```

**Решение (б):** Game View → кнопка с разрешением → **Free Aspect**. Тогда `Screen.width/Height` = физический размер окна = координаты мыши совпадают.

---

## 5. Пули проходили сквозь стены

**Симптом:** пуля пролетает сквозь `BoxCollider` стены насквозь.

**Причина:** `SphereCollider` пули был триггером (`isTrigger = true`). Триггер не создаёт физического препятствия. `OnTriggerEnter` реагировал только на `EnemyBase` и не вызывал `Expire()` при попадании в стену.

**Решение:**
1. Изменить коллайдер пули на твёрдый (`isTrigger = false`) в префабе.
2. Использовать `OnCollisionEnter` для физических столкновений.
3. В `Start()` игнорировать коллизию с игроком:
```csharp
foreach (var col in playerGO.GetComponentsInChildren<Collider>())
    Physics.IgnoreCollision(GetComponent<Collider>(), col);
```

---

## 6. Враги спавнились за пределами арены

**Симптом:** враги появлялись за стенами арены.

**Причина:** `RandomSpawnPosition()` добавлял `spawnRadius` (до 15 ед.) к позиции игрока без учёта границ арены. При игроке у стены (X=13) враг мог оказаться на X=28.

**Решение:** после расчёта позиции применять `Mathf.Clamp` по X и Z:
```csharp
const float arenaHalf = 13.5f; // стены на ±15, отступ 1.5 на размер врага
pos.x = Mathf.Clamp(pos.x, -arenaHalf, arenaHalf);
pos.z = Mathf.Clamp(pos.z, -arenaHalf, arenaHalf);
```

---

## 7. Враги дрожали по вертикали

**Симптом:** враги вибрируют по Y, иногда проваливаются.

**Причина:** `MoveTowardsPlayer()` каждый `FixedUpdate` устанавливал `linearVelocity = dir * speed` где `dir.y = 0`, обнуляя Y-скорость. При включённой гравитации это создавало бесконечный конфликт: гравитация тянет вниз, код сбрасывает в ноль — враг дрожит. Дополнительно: `useGravity` и `constraints` в коде перезатирали правильные настройки из самих префабов.

**Причина (корень):** гравитация не нужна в top-down игре, и она уже была отключена в префабах (`m_UseGravity: 0`). Проблема возникла потому что код EnemyBase переопределял настройки Rigidbody вместо того чтобы доверять конфигурации префаба.

**Решение:** не трогать `useGravity` и `constraints` в Awake() — они уже верно настроены в префабах. Код должен только читать Rigidbody, а не переконфигурировать его.

---

## 8. Враги спавнились провалившимися в пол

**Симптом:** при спавне враг уходит наполовину под пол.

**Причина:** `pos.y = 0f` в `WaveManager.RandomSpawnPosition()` не учитывал что pivot-точка Unity-объектов находится в **центре** меша, а не в его основании. Пол на Y=0, значит центр врага тоже на Y=0 — нижняя половина под полом.

- TriangleEnemy: CapsuleCollider height=2, scale.y=1.5 → реальная полувысота **1.5 ед**, низ на Y=−1.5
- RectEnemy: BoxCollider size=1, scale.y=1 → полувысота **0.5 ед**, низ на Y=−0.5

**Артефакты которые не решали проблему (и почему):**
- `col.bounds.extents.y` в `Awake()` — ненадёжен, физический движок не успевает вычислить bounds до первого кадра
- Виртуальный `FloorOffset` с коррекцией позиции в Awake() — патч поверх неверного Y спавна; `FreezePositionY` установленный до коррекции мог фиксировать Y на старом значении (0)

**Правильное решение (root fix):** данные о высоте принадлежат врагу, а не WaveManager. Поле `floorOffset` хранится прямо в префабе. WaveManager читает его с префаба **до** `Instantiate()` и передаёт верный Y сразу:

```csharp
// EnemyBase.cs
public float floorOffset = 0.5f; // сериализовано, задаётся в префабе
```

Префабы: `TriangleEnemy.floorOffset = 1.5`, `RectEnemy.floorOffset = 0.5`

```csharp
// WaveManager.SpawnEnemy()
float spawnY = prefab.GetComponent<EnemyBase>()?.floorOffset ?? 0.5f;
Vector3 pos = RandomSpawnPosition();
pos.y = spawnY;
GameObject enemy = Instantiate(prefab, pos, Quaternion.identity);
```

Объект создаётся сразу на нужной высоте — никаких рантайм-коррекций.

---

## 9. Ошибки при работе с Оркестратором

**Симптом:** команды возвращают ошибки несмотря на правильную логику.

**Частые случаи:**
- `query` передан строкой (`"n:Player"`) вместо объекта (`{name:"Player"}`) → `Cannot access child value on JValue`
- Параметр называется `member`, а не `property` → `args.member is required`
- `scene.save` вызван в Play Mode → `This cannot be used during play mode`
- `scene.batch.addComponent` с широким query попал на 32 объекта вместо одного

**Решение:** перед работой читать `ORCHESTRATOR_COMMANDS.md`. Для деструктивных операций использовать `dryRun: true`. Для точного выбора объекта комбинировать фильтры: `{name:"Player", hasComponent:"ArenaGame.PlayerController"}`.

---

## 10. Play Mode не подхватывал изменения скриптов

**Симптом:** код исправлен, компиляция успешна, но поведение в игре не изменилось.

**Причина:** Unity применяет изменения сборки только при следующем входе в Play Mode. Если игра уже запущена — исполняется старый код.

**Решение:** всегда выходить из Play Mode (`editor.play.exit`) после изменения скриптов, дождаться компиляции, затем снова запускать.

---

## 11. Враги не поворачивались в сторону движения

**Симптом:** RectEnemy и TriangleEnemy всегда смотрят в одну сторону независимо от направления движения.

**Причина:** оба префаба имели `m_Constraints: 112` = FreezeRotationX + FreezeRotationY + FreezeRotationZ. Физический движок блокировал поворот по Y-оси и перезаписывал `transform.rotation` обратно на каждом physics step. Код `transform.rotation = Quaternion.LookRotation(dir)` выполнялся, но сразу же отменялся физикой.

**Решение:**
1. В префабах изменить `m_Constraints: 112 → 80` (80 = FreezeRotationX(16) + FreezeRotationZ(64) — только X и Z заморожены, Y свободен).
2. В `EnemyBase.MoveTowardsPlayer()` заменить `transform.rotation =` на `_rb.MoveRotation()` — physics-aware API, не конфликтует с Rigidbody.

```csharp
_rb.MoveRotation(Quaternion.LookRotation(dir.normalized));
```

**Почему 80, а не 112:** FreezeRotationY(32) запрещает физике менять Y-угол. Убрав его, позволяем врагу поворачиваться горизонтально; X и Z по-прежнему заморожены — враг не наклоняется и не переворачивается.

---

## 12. Игрок не поворачивался при движении

**Симптом:** WASD двигает игрока, но его rotation всегда остаётся (0,0,0).

**Причина:** `PlayerController.Move()` вычислял `dir` и вызывал `_controller.Move(dir * speed * dt)`, но ни разу не писал в `transform.rotation`. `CharacterController.Move()` управляет только позицией — поворот он не трогает.

**Решение:** добавить в `Move()` плавный поворот через `Quaternion.RotateTowards`:

```csharp
[SerializeField] private float rotationSpeed = 540f;

// в Move():
if (dir.sqrMagnitude > 0.001f)
{
    Quaternion targetRotation = Quaternion.LookRotation(dir);
    transform.rotation = Quaternion.RotateTowards(
        transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
}
```

`RotateTowards` поворачивает не больше `rotationSpeed * dt` градусов за кадр. При 540°/сек полный разворот занимает ~0.33 сек.

---

## 13. Поворот игрока визуально незаметен

**Симптом:** код поворота работает (rotation меняется), но в игре не видно что игрок смотрит в разные стороны.

**Причина:** игрок — симметричный куб одного цвета. У него нет различимого «переда» — все четыре боковые грани выглядят одинаково.

**Решение:** в `PlayerController.Awake()` создать дочерний маркер-куб на передней (+Z) грани:

```csharp
var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
marker.transform.SetParent(transform);
marker.transform.localPosition = new Vector3(0f, 0f, 0.6f);
marker.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
Destroy(marker.GetComponent<Collider>());
if (marker.TryGetComponent<Renderer>(out var mr))
    mr.material.color = Color.black;
```

Маркер вращается вместе с игроком и показывает куда тот смотрит. Новых файлов не требует.
