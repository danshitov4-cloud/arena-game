# Майн Фарм — Codex Context

## Цель проекта

Не игра — инструмент разработки. Проект тестирует совместную работу AI-агентов (Codex, Claude Code, Orchestrator, claude.ai) над Unity-проектом. Pacman и ферма — полигон, а не продукт.

Unity 2D, URP. Основная сцена: `Assets/ИГРА.unity`.

## Архитектура

| Система | Файлы | Статус |
|---|---|---|
| Building System | `prefabs/Scripts/View/`, `prefabs/bullding/` | 🔄 нет сетки и UI |
| GridWorld | — | ❌ не начато |
| Pacman (прототип) | `Scripts/Pacman/` | ✅ работает |
| Snake | `Snake/SnakeGame.cs` | ❌ заглушка |
| AI Orchestrator | `Orchestrator/Editor/` | ✅ HTTP :5137 |

**Building:** `GameInit` → `BuildingService.Build(pos)` → `BuildingView` (таймер 30с, событие `onBuilt`)  
**Типы ячеек:** `CellType { Res, Supp, Spec, Deco }`, `CellState { Locked, Empty, Building, Active }`

## Соглашения

- Namespace фермы: `FarmGame` (Pacman: `PacmanGame`, Orchestrator: `Orchestrator.Editor`)
- Папки: `Assets/Scripts/[система]/`, префабы: `Assets/prefabs/[система]/`
- Один скрипт — одна ответственность
- Комментарии: русский или английский

## Оркестратор (AI → Unity)

`POST http://127.0.0.1:5137/command` — `{ "command": "...", "args": {} }`  
Всегда `dryRun: true` перед деструктивными командами. Снапшот до → работа → снапшот после.  
Полный список команд: `ORCHESTRATOR_SPEC.md` или команда `help.commands`.

## Приоритеты

1. GridWorld — сетка мира
2. Building System — выбор и размещение
3. Экономика — ресурсы
4. Сохранение

## Правила работы

- Читать `PROGRESS.md` перед началом, обновлять после
- Проверять компиляцию: `project.compilation.errors`
- Не трогать `_Recovery/` и дублирующийся `Scripts/SnakeGame.cs`
