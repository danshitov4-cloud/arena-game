# PROGRESS.md — Майн Фарм

Обновляется после каждой сессии. Самая свежая запись — сверху.

---

## 2026-06-27 — Claude Code (Canvas UI + физика врагов)

### Сделано

**Canvas UI для ArenaGame (через `UICreator.cs` в Edit Mode):**
- Canvas — ScreenSpaceOverlay, CanvasScaler ScaleWithScreenSize 1920×1080
- EventSystem с `InputSystemUIInputModule` (вместо устаревшего StandaloneInputModule)
- 4 текста HUD: HP (верхний левый), Timer (верхний центр), Wave (верхний правый), Ammo (нижний левый)
- UpgradePanel — по центру, 3 кнопки FireRate/Damage/HP, скрыта по умолчанию
- UIManager поля (`hpText`, `ammoText`, `timerText`, `waveText`, `upgradePanel`) — подключены через reflection и сохранены в сцене
- UpgradeSystem.`upgradePanel` и `upgradeButtons` — подключены

**Исправлена физика врагов (треугольники накладывались):**
- `EnemyBase.cs` — переключен с `transform.position = MoveTowards(...)` на `Rigidbody.linearVelocity` в `FixedUpdate()`
- Коллайдеры врагов — `isTrigger: false` (физическое разделение)
- `TriangleEnemy/RectEnemy.cs` — `OnTriggerEnter` → `OnCollisionEnter`, цвет ставится в `Awake()`

**Исправлена ошибка Input System:**
- `StandaloneInputModule` → `InputSystemUIInputModule` (совместимость с New Input System)
- Ошибка `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class` устранена

**Исправлены stale fileID ссылки на префабы:**
- После пересохранения префаба fileID внутри меняются → WaveManager терял ссылки
- Перепривязаны `triangleEnemyPrefab` и `rectEnemyPrefab` через `asset.ref.setFieldByQuery`

**Компиляция:** 0 ошибок, 0 предупреждений

### Важные выводы

- `editor.screenshot` (`cam.Render()`) **не захватывает** ScreenSpaceOverlay Canvas — UI виден только в Game View Unity
- Команды Оркестратора: `editor.log.read` (не `editor.log.get`); `scene.batch.setComponentProperties` требует ключ `set` (не `properties`); `asset.ref.setFieldByQuery` требует `member` (не `fieldName`)

### Осталось

- [ ] Панели GameOver и Victory (UIManager имеет поля `gameOverPanel`, `victoryPanel` — объектов нет в сцене)
- [ ] Удалить `Assets/Scripts/UICreator.cs` (временный скрипт-помощник)
- [ ] `WaveManager.player` и `WaveManager.upgradeSystem` — {fileID: 0}, работают через FindFirstObjectByType как fallback
- [ ] Улучшить скриншот в Orchestrator: использовать `ScreenCapture` вместо `cam.Render()` чтобы захватывать UI

---

## 2026-06-27 — Claude Code (префабы врагов и Bullet)

### Сделано

**Создано 3 префаба через Оркестр (`scene.createPrimitive` → компоненты → `scene.savePrefab`):**

| Префаб | Геометрия | Компоненты |
|---|---|---|
| `Assets/Prefabs/TriangleEnemy.prefab` | Capsule, scale (0.5,1.5,0.5), красный | TriangleEnemy, Rigidbody (no gravity, freeze rot), CapsuleCollider (trigger) |
| `Assets/Prefabs/RectEnemy.prefab` | Cube, scale (1.5,1,0.5), красный | RectEnemy, Rigidbody (no gravity, freeze rot), BoxCollider (trigger) |
| `Assets/Prefabs/Bullet.prefab` | Sphere, scale (0.2,0.2,0.2), жёлтый | Bullet, Rigidbody (no gravity), SphereCollider (trigger) |

**Подключены ссылки через `asset.ref.setFieldByQuery`:**
- `WaveManager.triangleEnemyPrefab` → TriangleEnemy.prefab ✅
- `WaveManager.rectEnemyPrefab` → RectEnemy.prefab ✅
- `PlayerController.bulletPrefab` → Bullet.prefab ✅

**Компиляция:** 0 ошибок, 0 предупреждений

### Осталось

- [ ] Настроить Canvas / UI (HUD тексты: HP, Ammo, Timer, Wave; кнопки апгрейда) для UIManager
- [ ] Добавить `MaxHealth` property в PlayerController для HUD
- [ ] Подключить `WaveManager.player` → Player.transform
- [ ] Подключить `WaveManager.upgradeSystem` → UpgradeSystem

---

## 2026-06-27 — Claude Code (сборка сцены через Оркестр)

### Сделано

**Сцена `Assets/3D game.unity` — полностью собрана через Оркестр:**

| Объект | Что | Детали |
|---|---|---|
| `Arena_Floor` | Plane 30×30 | localScale (3,1,3), серый материал |
| `Wall_North/South/East/West` | Cube | 4 стены по периметру |
| `Player` | Cube | tag=Player, BoxCollider(trigger), CharacterController, PlayerController |
| `Main Camera` | Camera | позиция (10,10,-10), угол (35,45,0), CameraController → target=Player |
| `GameManager` | Empty | + ArenaGame.GameManager |
| `WaveManager` | Empty | + ArenaGame.WaveManager |
| `UIManager` | Empty | + ArenaGame.UIManager |
| `Directional Light` | Light | существовал ранее |

- Использован временный `ArenaSetup.cs` (MonoBehaviour + ExecuteInEditMode) для создания примитивов через CreatePrimitive(); удалён после сборки
- dryRun=true перед каждой операцией
- CameraController.target подключён к Player через `scene.ref.setFieldByQuerySelf`
- Компиляция: 0 ошибок, 0 предупреждений

### Осталось

- [ ] Создать префабы врагов (TriangleEnemy, RectEnemy) и Bullet для WaveManager
- [ ] Назначить префабы в WaveManager.triangleEnemyPrefab / rectEnemyPrefab
- [ ] Настроить Canvas / UI (HUD тексты, кнопки апгрейда) для UIManager
- [ ] Добавить `MaxHealth` property в PlayerController для полного HUD

---

## 2026-06-27 — Claude Code (UpgradeSystem + документация)

### Сделано

- `UpgradeSystem.cs` — полная логика: Fisher-Yates shuffle из 3 типов, пауза (timeScale=0), кнопки с текстовыми метками, ApplyUpgrade вызывает UpgradeFireRate/SetBulletDamage/Heal, после выбора → HidePanel + StartWave следующей волны
- Исправлено форматирование PROGRESS.md (слипшийся `---##`)
- Обновлён CLAUDE.md — статус всех файлов ArenaGame

**Компиляция:** 0 ошибок, 0 предупреждений

### Осталось для ArenaGame

- [ ] Создать префабы: Player (куб + CharacterController), TriangleEnemy, RectEnemy, Bullet (куб + Rigidbody + Trigger)
- [ ] Настроить сцену `Assets/3D game.unity`: разместить объекты, назначить ссылки в инспекторе
- [ ] Назначить тег "Player" на объект игрока
- [ ] Настроить Canvas с текстами HUD и кнопками апгрейда
- [ ] Добавить `MaxHealth` property в PlayerController (нужен для Heal-логики UI)

---

## 2026-06-27 — Codex (ArenaGame: продолжение логики, прервано)

### Сделано

- `PlayerController.cs` — добавлены задержка стрельбы, урон создаваемой пули, свойства HUD и методы для апгрейдов FireRate/Damage/HP
- `UIManager.cs` — HUD обновляется каждый кадр по реальным данным игрока и волны; добавлены панели поражения и победы
- `GameManager.cs` — ссылки на системы, запуск игры, GameOver, Victory и рестарт по R
- `WaveManager.cs` — волны по 180 секунд, спавн каждые 5 секунд в радиусе 15, состав 5/10/20 врагов, завершение через UpgradeSystem или Victory
- После восстановления Оркестратора проверена компиляция: **0 ошибок, 0 предупреждений**

### Не завершено

- [ ] `UpgradeSystem.cs` — всё ещё скелет; выбор и применение FireRate/Damage/HP не реализованы
- [ ] Префабы и ссылки на объекты в сцене `Assets/3D game.unity` не настраивались

---

## 2026-06-27 — Claude Code (сессия ArenaGame скелеты + логика)

### Сделано

**Новая сцена 3D игры:**
- Создана `Assets/3D game.unity` (scene.new, DefaultGameObjects)
- Добавлен Directional Light (50°, -30°, 0°)
- Main Camera переведена в перспективный режим (FOV 60), позиция (0, 5, -10)

**Скелеты — Часть 1 (только структура, без логики):**

| Файл | Описание |
|---|---|
| `Assets/Scripts/Core/GameManager.cs` | Singleton, GameState enum, score/wave/lives |
| `Assets/Scripts/Player/PlayerController.cs` | CharacterController, поля движения/боя |
| `Assets/Scripts/Player/CameraController.cs` | Изометрическая камера, offset/smooth |
| `Assets/Scripts/Enemy/EnemyBase.cs` | Абстрактный базовый класс, hp/speed/TakeDamage |
| `Assets/Scripts/Enemy/TriangleEnemy.cs` | Наследник, hp=2 |
| `Assets/Scripts/Enemy/RectEnemy.cs` | Наследник, hp=6 |
| `Assets/Scripts/Bullet/Bullet.cs` | damage=1, speed=10, OnTriggerEnter |
| `Assets/Scripts/Wave/WaveManager.cs` | currentWave, waveTimer=180, волны 5/10/20 врагов |
| `Assets/Scripts/Wave/UpgradeSystem.cs` | UpgradeType enum, ShowUpgradeChoice/ApplyUpgrade |
| `Assets/Scripts/UI/UIManager.cs` | Singleton, UpdateAmmo/HP/Timer/Wave/ShowUpgradePanel |

**Логика — Часть 2 (наполнение скелетов):**

| Файл | Что реализовано |
|---|---|
| `PlayerController.cs` | WASD + CharacterController, гравитация, стрельба на клик мыши (raycast → ground plane), 60 патронов, 3 HP, Die → GameManager.GameOver() |
| `CameraController.cs` | Изометрия как в Hades: offset (10,10,-10), Lerp следование, LookAt игрока |
| `Bullet.cs` | Rigidbody.linearVelocity = transform.forward * speed, OnTriggerEnter → EnemyBase.TakeDamage, Expire через 3 сек |
| `EnemyBase.cs` | TakeDamage уменьшает hp → Die → Destroy, MoveTowardsPlayer() на XZ плоскости |
| `TriangleEnemy.cs` | hp=2, speed=4, наследует движение, OnTriggerEnter → player.TakeDamage |
| `RectEnemy.cs` | hp=6, speed=2, то же самое |

**Исправлено попутно:**
- `WaveManager.cs` и `UpgradeSystem.cs` — CS0161 (методы с return типом без return)

**Компиляция:** 0 ошибок, 0 предупреждений (после domain reload ~10 мин)

### Осталось для ArenaGame

- [ ] `GameManager.cs` — логика: старт/конец игры, счёт, волны
- [ ] `WaveManager.cs` — логика: спавн врагов по таймеру, смена волн
- [ ] `UpgradeSystem.cs` — логика: UI выбора апгрейда, применение к PlayerController
- [ ] `UIManager.cs` — логика: обновление текстов HUD
- [ ] Создать префабы: Player (куб + CharacterController), TriangleEnemy, RectEnemy, Bullet
- [ ] Настроить сцену `3D game.unity`: добавить объекты, привязать компоненты
- [ ] Назначить тег "Player" на объект игрока (нужен для FindWithTag в EnemyBase)

### Осталось (технический долг Майн Фарм)

- [ ] Починить `BuildingService.cs` — namespace FarmGame, убрать mojibake
- [ ] Удалить дубликат `Assets/Scripts/SnakeGame.cs`
- [ ] Убрать тестовые скрипты `AssetRefTest.cs`, `TestRunner.cs`
- [ ] Очистить `_Recovery/` (15 старых бэкап-сцен)

---

## 2026-06-25 — Claude Code (сессия 2)

### Сделано

**Восстановление сцены Pacman:**
- Удалены артефакты с `Assets/ИГРА.unity`: `GridRoot` (6 дочерних объектов — Building_Base, Orch_Building_01/02, TestPrefab_01) и `Orch_Empty1`
- Сцена сохранена через `scene.save`
- На сцене остались только нужные объекты: `GameManager`, `Maze`, `Player`, `Ghosts`, `Main Camera`, `Global Light 2D`, `Canvas`, `EventSystem`

**Edit Mode генерация лабиринта (`MazeGenerator.cs`):**
- Добавлен `[ContextMenu("Generate Maze")]` — правый клик → Generate Maze прямо в редакторе
- `ClearGeneratedChildren` и `OnDestroy` теперь используют `SafeDestroy` — `DestroyImmediate` в Edit Mode, `Destroy` в Play Mode
- Создан `Assets/Scripts/Pacman/Editor/MazeGeneratorEditor.cs` — кастомный Inspector с кнопками **Generate Maze (Edit Mode)** и **Clear Maze**

**`GameManager.cs`** — был уже исправлен другим агентом до этой сессии:
- Убран `SceneManager.LoadScene(buildIndex)`, рестарт через `MazeGenerator.GenerateMaze()`
- `Awake` сбрасывает `Time.timeScale = 1f`, `OnDestroy` чистит `Instance`

---

## 2026-06-25 — Claude Code

### Сделано

**Оркестратор — 12 новых команд в 4 файлах:**

| Файл | Команды |
|---|---|
| `SceneFileCommands.cs` | `scene.open`, `scene.save`, `scene.saveAs`, `scene.getActive` |
| `ProjectAssetsFolderCommands.cs` | `project.assets.createFolder`, `project.assets.importFile`, `project.assets.refresh` |
| `SpritesheetCommands.cs` | `project.sprites.configure`, `project.sprites.slice`, `project.sprites.list` |
| `AnimClipCommands.cs` | `anim.clip.create`, `anim.controller.addState` |

**Исправлено:**
- `help.commands` теперь тянет список из `CommandDispatcher` — всегда актуален, не нужно регистрировать вручную

**Документация:**
- Создан `CLAUDE.md` с полным описанием проекта, архитектурой, правилами
- Создан `CODEX_CONTEXT.md` — компактная версия ≤50 строк для Codex
- Создан `AGENT_EVAL.md` — журнал оценки работы агентов
- Создан `PROGRESS.md` (этот файл)
- Добавлен раздел «Цель проекта» — инструмент AI-разработки, не игра

### Что теперь умеет агент (полный пайплайн PNG→анимация)

```
project.assets.importFile   ← скопировать PNG с диска в Assets
project.sprites.configure   ← настроить TextureImporter (Multiple, Point, PPU)
project.sprites.slice       ← нарезать по сетке cellWidth/cellHeight
project.sprites.list        ← получить имена всех кадров
anim.clip.create            ← создать .anim с кадрами и FPS
anim.controller.addState    ← добавить состояние в Animator
anim.assignControllerByQuery← назначить контроллер на объект
scene.save                  ← сохранить сцену
```

### Осталось (технический долг)

- [ ] Починить `BuildingService.cs` — добавить namespace `FarmGame`, убрать mojibake
- [ ] Удалить дубликат `Assets/Scripts/SnakeGame.cs`
- [ ] Убрать тестовые скрипты `AssetRefTest.cs`, `TestRunner.cs`
- [ ] Очистить `_Recovery/` (15 старых бэкап-сцен)

### Следующий приоритет (по CLAUDE.md)

**GridWorld** — сетка мира. Типы/состояния ячеек уже есть в `GameTypes.cs`.
Нужен: `GridWorld.cs` (MonoBehaviour, 2D массив Cell), `GridCell.cs` (данные ячейки), визуализация через тайлы или SpriteRenderer.
