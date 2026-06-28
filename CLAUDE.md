# Project: Майн Фарм

## Цель проекта

**Главная цель — не игра, а инструмент разработки.**

Проект создаёт и тестирует рабочий процесс, в котором команда AI-агентов (Codex, Claude Code, Unity Orchestrator, claude.ai) совместно разрабатывает Unity-проекты. Игровой код — Pacman, ферма — это только полигон для проверки инструмента.

**Что проверяется:**
- Может ли Codex писать C# скрипты по задаче без ручного вмешательства
- Может ли Orchestrator управлять сценой Unity через HTTP-команды
- Может ли Claude Code читать, рефакторить и связывать скрипты от других агентов
- Как агенты передают контекст друг другу через CLAUDE.md / PROGRESS.md

Unity 2D, URP. Основная сцена — `Assets/ИГРА.unity`.

---

## Оркестратор (AI-управление Unity)

HTTP-сервер внутри Unity Editor. Запускается автоматически при открытии проекта.

- **Endpoint:** `POST http://127.0.0.1:5137/command`
- **Тело запроса:** `{ "command": "...", "args": {}, "id": "optional", "dryRun": false }`
- **Окно в редакторе:** `Tools → AI Orchestrator`
- **Спецификация:** `Assets/Orchestrator/ORCHESTRATOR_SPEC.md`

### Группы команд

| Prefix | Что делает |
|---|---|
| `ping` | Проверка соединения — версия Unity, сцена, проект |
| `scene.*` | Поиск, создание, удаление объектов на сцене |
| `scene.open/save/saveAs/getActive` | Открыть/сохранить файл сцены |
| `scene.batch.*` | Массовые операции: компоненты, трансформы, спрайты |
| `scene.snapshot.*` | Снапшоты сцены и откат |
| `scene.optimize.*` | Анализ и оптимизация сцены |
| `materials.*` | Материалы: инспект, цвета, пресеты, снапшоты |
| `prefab.*` | Инстанцирование, замена, применение overrides |
| `project.assets.*` | Поиск, создание папок, копирование файлов, refresh |
| `project.sprites.*` | Настройка импорта PNG, нарезка спрайтлиста, список спрайтов |
| `project.scripts.*` | Чтение и патч C# файлов |
| `project.compilation.*` | Ожидание компиляции, статус, ошибки |
| `editor.play.*` | Вход/выход из Play Mode |
| `anim.*` | Контроллеры, параметры, клипы, состояния |
| `help.*` | Документация команд (тянет из диспетчера, всегда актуально) |

Перед деструктивными операциями использовать `dryRun: true`. Для отката — `scene.snapshot.take` до и `scene.snapshot.restore` после.

---

## Архитектура игры

### Система зданий (основная механика фермы)

```
Assets/prefabs/
  Scripts/
    core/GameTypes.cs          — перечисления CellType, CellState
    View/GameInit.cs           — точка входа, привязывает buildingPrefab
    View/BuildingService.cs    — статический сервис, Build(Vector3)
    View/BuildingView.cs       — визуал: таймер строительства (30 сек), событие onBuilt
  bullding/
    Building_Base.prefab       — базовый префаб здания (с Body/SpriteRenderer, UI таймером)
```

`GameInit` → `BuildingService.buildingPrefab` → `BuildingService.Build()` → `BuildingView`

**Типы ячеек** (`CellType`): `Res` (ресурс), `Supp` (поддержка), `Spec` (спецздание), `Deco` (декор)  
**Состояния** (`CellState`): `Locked → Empty → Building → Active`

### Пакман-прототип

```
Assets/Scripts/Pacman/
  MazeGenerator.cs    — генерирует лабиринт 21×21 из хардкоженого Layout[]
  PacmanController.cs — движение игрока
  GhostAI.cs          — 4 призрака, случайное движение без разворота
  GameManager.cs      — счёт, победа/поражение, рестарт (R)
  Dot.cs              — точка для сбора
  MazeCell.cs         — ячейка лабиринта (Wall / Path)
```

Namespace: `PacmanGame`. Спрайты генерируются программно из цветных Texture2D.

### Змейка

`Assets/Snake/SnakeGame.cs` — пустая заглушка, механика не реализована.

---

## Соглашения

- **Namespace Pacman:** `PacmanGame`
- **Namespace Building:** глобальный (нет namespace — нужно добавить, например `FarmGame`)
- **Namespace Orchestrator:** `Orchestrator.Editor`
- **Папки:** `Assets/Scripts/[система]/`, `Assets/prefabs/Scripts/[слой]/`
- **Префабы:** `Assets/prefabs/[система]/`
- **Рендер:** URP 2D (`Assets/Settings/UniversalRP.asset`)
- **Input:** Unity Input System (`Assets/InputSystem_Actions.inputactions`)

---

## Текущий статус

- ✅ Оркестратор — HTTP-сервер, 155+ команд, полный пайплайн PNG→спрайт→анимация
- ✅ Pacman-прототип — генератор лабиринта, ИИ призраков, сбор точек, победа/поражение
- ✅ BuildingView — таймер строительства, событие onBuilt
- ✅ BuildingService — создание здания по позиции
- ✅ GameTypes — типы и состояния ячеек
- 🔄 Building System — нет сетки клеток, нет UI выбора здания, нет игровой петли
- 🔄 ArenaGame (3D) — скрипты ✅, сцена собрана ✅; нужны префабы врагов и Canvas UI
- ❌ Snake — пустая заглушка
- ❌ Сетка мира (GridWorld) — типы клеток есть, сама сетка не реализована
- ❌ Ресурсы / экономика фермы
- ❌ Сохранение / загрузка

### ArenaGame — namespace `ArenaGame`, сцена `Assets/3D game.unity`

```
Assets/Scripts/
  Core/GameManager.cs          — запуск, GameOver, Victory, рестарт по R ✅
  Player/PlayerController.cs   — WASD, стрельба, 60 патронов, 3 HP, точки интеграции апгрейдов ✅
  Player/CameraController.cs   — Hades-изометрия, offset (10,10,-10) ✅
  Enemy/EnemyBase.cs           — TakeDamage, Die, MoveTowardsPlayer ✅
  Enemy/TriangleEnemy.cs       — hp=2, speed=4 ✅
  Enemy/RectEnemy.cs           — hp=6, speed=2 ✅
  Bullet/Bullet.cs             — Rigidbody, trigger, Expire ✅
  Wave/WaveManager.cs          — таймер 180 сек, спавн 5 сек, волны 5/10/20 ✅
  Wave/UpgradeSystem.cs        — пауза, shuffle, кнопки, ApplyUpgrade → UpgradeFireRate/SetBulletDamage/Heal ✅
  UI/UIManager.cs              — живой HUD и панели GameOver/Victory ✅
```

---

## Известные проблемы

- `Assets/Snake/SnakeGame.cs` и `Assets/Scripts/SnakeGame.cs` — дубликат, нужно удалить один
- `BuildingService.cs` — битые комментарии (mojibake), глобальный namespace
- `_Recovery/` — 15 резервных сцен (`0.unity` … `0 (14).unity`), можно удалить если не нужны
- `Assets/AssetRefTest.cs` — тестовый скрипт в корне, нужно убрать или переместить
- `Assets/Scripts/TestRunner.cs` — тестовый скрипт в корне Scripts, нужно убрать
- В Pacman лабиринт хардкоженный (не процедурный), 4 призрака всегда красные

---

## Правила для AI агентов

### Обязательно перед началом работы
1. Прочитать этот файл полностью
2. Прочитать PROGRESS.md если существует
3. Проверить компиляцию: `project.compilation.errors`

### Обязательно после работы
1. Обновить PROGRESS.md — что сделано, что осталось
2. Проверить компиляцию
3. Обновить статус в этом файле (раздел «Текущий статус»)

### Правила кода
- Namespace фермы: `FarmGame`
- Всегда `dryRun: true` перед деструктивными операциями оркестратора
- Один скрипт = одна ответственность
- Комментарии на русском или английском

### Приоритеты разработки
1. **GridWorld** — сетка мира (основа всего)
2. **Building System** — выбор и размещение зданий
3. **Экономика** — ресурсы
4. **Сохранение**

---

## Файловая структура

```
Assets/
  ИГРА.unity                          — главная сцена
  Anim/Player.controller              — анимации игрока
  Orchestrator/Editor/                — 52 файла AI-оркестратора
    SceneFileCommands.cs              — scene.open/save/saveAs/getActive
    ProjectAssetsFolderCommands.cs    — createFolder/importFile/refresh
    SpritesheetCommands.cs            — sprites.configure/slice/list
    AnimClipCommands.cs               — anim.clip.create, anim.controller.addState
  prefabs/
    bullding/Building_Base.prefab
    Scripts/core/GameTypes.cs
    Scripts/View/{BuildingService,BuildingView,GameInit}.cs
  Scripts/
    Pacman/{GameManager,GhostAI,MazeGenerator,MazeCell,PacmanController,Dot}.cs
  Snake/SnakeGame.cs
  Settings/                           — URP конфиги
  _Recovery/                          — 15 бэкап-сцен
```
