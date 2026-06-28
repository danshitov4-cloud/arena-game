# Orchestrator Commands Reference

Endpoint: `POST http://127.0.0.1:5137/command`  
Body: `{ "command": "...", "args": {}, "id": "optional", "dryRun": false }`

**Query object** (used in most commands as `args.query`):
```json
{ "nameContains": "", "caseSensitive": false, "tag": null, "layer": null,
  "hasComponent": null, "includeInactive": true, "max": 200 }
```

---

## ping

```
ping
```
Нет аргументов. Возвращает `unityVersion`, `project`, `scene`.

---

## scene — файлы сцены

```
scene.open        { path: string, additive?: false }
scene.save        { all?: false }
scene.saveAs      { path: string }
scene.getActive   (нет аргументов)
```

---

## scene — поиск и выборки

```
scene.scan              (нет аргументов) → полный снапшот сцены
scene.query             { nameContains?, caseSensitive?, tag?, layer?, hasComponent?, includeInactive?=true, max?=200 }
scene.selectByQuery     (те же аргументы что scene.query) → выделяет в Editor
scene.find              { nameContains?, caseSensitive?, max?=50 }
scene.select            { instanceId: int, ping?=false }
scene.getComponents     { instanceId: int }
```

---

## scene — мутации иерархии

```
scene.createEmpty       { name?, parentInstanceId?, position?={x,y,z} }
scene.addPrefab         { assetPath|guid: string, parentInstanceId?, nameOverride?,
                          position?={x,y,z}, rotationEuler?={x,y,z}, scale?={x,y,z} }
scene.setTransform      { instanceId: int, position?={x,y,z}, rotationEuler?={x,y,z}, scale?={x,y,z} }
scene.deleteByName      { name: string, includeInactive?=true, max?=100, dryRun?=false }
scene.deleteByQuery     { query: {...}, max?=100, dryRun?=false }
scene.setActiveByQuery  { query: {...}, active: bool, max?=100, dryRun?=false }
scene.destroyChildrenOf { parentInstanceId|parentQuery: ..., recursive?=false,
                          maxParents?=100, maxChildren?=1000, dryRun?=false }
scene.batchSetEnabledByType  { typeName: string, enabled: bool, max?=1000 }
```

---

## scene.snapshot — снапшоты сцены (in-memory + диск)

```
scene.snapshot.take          { name?: string, includeTypes?: string[] }
scene.snapshot.restore       { id: string, dryRun?=false }
scene.snapshot.restoreLatest { dryRun?=false }
scene.snapshot.list          (нет аргументов)
scene.snapshot.latest        (нет аргументов)
scene.snapshot.delete        { id: string }
scene.snapshot.save          (нет аргументов) → сохранить на диск
scene.snapshot.load          (нет аргументов) → загрузить с диска
```

---

## scene.batch — массовые операции с компонентами

```
scene.batch.addComponent        { query, componentType: string, dryRun?=false }
scene.batch.removeComponent     { query, componentType: string, dryRun?=false }
scene.batch.setComponentEnabled { query, componentType: string, enabled: bool, dryRun?=false }

scene.batch.getComponentProperty       { query, componentType: string, member: string }
scene.batch.setComponentProperty       { query, componentType: string, member: string, value: any, dryRun?=false }
scene.batch.setComponentProperties     { query, componentType: string, set: {member:value,...}, dryRun?=false }
scene.batch.diffComponentProperties    { query, componentType: string, set: {member:value,...},
                                         sampleLimit?=30, includeUnchanged?=false, dryRun?=false }
scene.batch.applyIfDiffComponentProperties  { (те же что diff) + apply?=true }
```

---

## scene.batch — массовые трансформы

```
scene.batch.offsetTransform  { query, space?="world", positionDelta?={x,y,z},
                                rotationDeltaEuler?={x,y,z}, scaleMul?={x,y,z}, scaleDelta?={x,y,z}, dryRun?=false }
scene.batch.setTransform     { query, space?="world", position?={x,y,z},
                                rotationEuler?={x,y,z}, scale?={x,y,z}, dryRun?=false }
scene.batch.snapToGrid       { query, gridStep: float, space?="world", mode?="floor",
                                snapX?=true, snapY?=true, snapZ?=true, offset?=0, dryRun?=false }
scene.batch.setAxis          { query, axis: "x"|"y"|"z", value: float, space?="world", dryRun?=false }
scene.batch.placePrefabGrid  { assetPath: string, rows: int, cols: int, plane?="XY",
                                origin?={x,y,z}, spacing?={x,y,z}, rotationEuler?={x,y,z}, scale?={x,y,z},
                                centered?=false, nameTemplate?, parentName|parentInstanceId?,
                                createParentIfMissing?=false, maxTotal?, dryRun?=false }
```

---

## scene.batch — иерархия и спрайты

```
scene.batch.setParentByQuery   { query, parentInstanceId|parentName: ...,
                                  createParentIfMissing?=false, worldPositionStays?=true, dryRun?=false }
scene.batch.renameByQuery      { query, template: string, startIndex?=0, zeroPad?=0, dryRun?=false }
scene.batch.duplicateByQuery   { query, copiesPerObject?=1, nameTemplate?, parentMode?="same"|"keepworld"|"set",
                                  parentName?, worldPositionOffset?={x,y,z}, maxCreated?, dryRun?=false }
scene.batch.setSpriteRendererSprite  { query, sprite?="white", pixelsPerUnit?, dryRun?=false }
```

---

## scene.ref — ссылки между компонентами

```
scene.ref.setFieldByQuery       { query, componentType: string, member: string,
                                   assetPath: string, assetType?="auto", allComponents?=false,
                                   max?=5000, dryRun?=false }
scene.ref.setFieldByQuerySelf   { query, componentType: string, member: string,
                                   selfComponentType: string, allComponents?=false,
                                   max?=5000, dryRun?=false }
scene.ref.setArrayFieldByQuery  { query, componentType: string, fieldName: string,
                                   targets: { by: "query"|"paths"|"instanceIds",
                                     query?:{...}, paths?:string[], instanceIds?:int[] },
                                   allowDuplicates?=false, dryRun?=false }
scene.ref.describe              { query, componentType: string, member: string }
```

---

## scene.workflow — workflow с автоматическим откатом

```
scene.workflow.run           { (комплексные аргументы workflow) }
scene.workflow.last          (нет аргументов) → последний результат workflow
scene.workflow.cancelRestore (нет аргументов) → отменить запланированный откат
```

---

## scene.optimize — анализ и оптимизация сцены

```
scene.optimize.plan    (нет аргументов) → план оптимизации
scene.optimize.apply   { stepIds?: string[] }
scene.optimize.restore (нет аргументов)
```

---

## scene.report — отчёты по сцене

```
scene.report.updates          (нет аргументов) → объекты с dirty/update flags
scene.report.renderers        (нет аргументов) → все Renderer-ы
scene.report.instancesByType  { typeName?: string, max?: int }
```

---

## materials — материалы

```
materials.report          (нет аргументов) → топ-20 материалов по кол-ву использований
materials.reportRich      { nameContains?, top?=10, useShared?=true }
materials.inspect         { nameContains?: string, top?=3, useShared?=true, includeAllColorAliases?=false }
materials.findUsage       { materialName?: string, assetPath?: string }
materials.setProperty     { nameContains?: string, property: string, value: any, dryRun?=false }
materials.setColorAuto    { nameContains?: string, color: {r,g,b,a}, dryRun?=false }
materials.setColorByObjectNameContains    { nameContains: string, color: {r,g,b,a}, dryRun?=false }
materials.setColorByMaterialNameContains  { nameContains: string, color: {r,g,b,a}, dryRun?=false }
materials.replaceByObjectNameContains     { nameContains: string, replacementMaterialPath: string, dryRun?=false }
materials.replaceByMaterialNameContains   { nameContains: string, replacementMaterialPath: string, dryRun?=false }
```

### Пресеты материалов: `highlight` | `red` | `ghost` | `reset`

```
materials.applyPreset            { preset: string, nameContains: string, dryRun?=false }
materials.applyPresetAutoTarget  { preset: string, target: string, top?=3,
                                    useShared?=true, dryRun?=false, autoRestore?=false }
materials.presets.list           (нет аргументов)
materials.presets.applyAutoTarget  { preset: string, target: string, top?=3,
                                      useShared?=true, dryRun?=false, autoRestore?=false,
                                      snapshotName? }
```

### Workflow материалов

```
materials.workflow.applyPresetAutoTargetDelay  { preset, target, delaySeconds, top?=3, dryRun?=false }
materials.workflow.testPresetAutoTarget        { preset, target, top?=3, dryRun?=false }
materials.workflow.testPresetAutoTargetV2      { preset, nameContains, top?=3, useShared?=true,
                                                  dryRun?=false, autoRestore?=false, snapshotName? }
materials.workflow.cancelRestore               (нет аргументов)
```

---

## materials.snapshot — снапшоты материалов (автосохранение)

```
materials.snapshot.take     { name?: string, nameContains?: string, top?=3,
                               useShared?=true, includeAllColorAliases?=false }
materials.snapshot.restore  { id: string, dryRun?=false }
materials.snapshot.list     (нет аргументов)
materials.snapshot.latest   (нет аргументов)
materials.snapshot.save     (нет аргументов) → сохранить в ProjectSettings
materials.snapshot.load     { merge?=true }
```

---

## prefab — префабы

```
prefab.replaceByQuery       { query, prefabAssetPath: string, keepTransform?=true,
                               keepName?=true, keepParent?=true, deleteOriginal?=true,
                               max?=2000, dryRun?=false }
prefab.instantiate          { assetPath: string, name?, position?={x,y,z},
                               rotationEuler?={x,y,z}, scale?={x,y,z},
                               parentInstanceId|parentName?, createParentIfMissing?=false,
                               worldPositionStays?=true, select?=false, dryRun?=false }
prefab.applyOverridesByQuery   { query, max?=2000, dryRun?=false }
prefab.revertOverridesByQuery  { query, max?=2000, dryRun?=false }
prefab.unpackByQuery           { query, max?=2000, dryRun?=false }
```

---

## project.assets — файловая система проекта

```
project.assets.createFolder  { path: string, dryRun?=false }
project.assets.importFile    { source: string, destination: string, overwrite?=false, dryRun?=false }
project.assets.refresh       { path?: string }   — пусто = AssetDatabase.Refresh()
project.assets.find          { nameContains?, type?, folder?, max?=50 }
project.assets.inspect       { assetPath: string, includeDependencies?=false,
                                includeSubAssets?=false, depsRecursive?=false,
                                depsMax?=20, subAssetsMax?=20 }
```

---

## project.sprites — импорт и нарезка спрайтлистов

```
project.sprites.configure  { path: string, pixelsPerUnit?=100,
                              filterMode?="Point"|"Bilinear"|"Trilinear",
                              spriteMode?="Multiple"|"Single", dryRun?=false }

project.sprites.slice      по сетке: { path, cellWidth: int, cellHeight: int, dryRun?=false }
                           по рэктам: { path, rects: [{x,y,w,h,name?},...], dryRun?=false }

project.sprites.list       { path: string }  → список sub-спрайтов с rect и path
```

---

## project.scripts — чтение и патч C# файлов

```
project.scripts.read    { path: string, maxChars?: int }
project.scripts.patch   { path: string, operations: [{type:"insert"|"replace"|"delete",...}],
                           overwrite?=false, dryRun?=false }
project.scripts.create  { path: string, template?, className?, namespace?,
                           overwrite?=false, dryRun?=false }
```

---

## project.compilation — компиляция

```
project.compilation.wait    { timeoutSeconds?=30 }
project.compilation.status  (нет аргументов)
project.compilation.errors  (нет аргументов)
```

---

## project.exportContext

```
project.exportContext  { zipPath?="ProjectSettings/OrchestratorContext.zip",
                          include?: string[], add?: string[], exclude?: string[],
                          includeProjectSettingsSchema?=true }
```

---

## anim — анимации

```
anim.clip.create              { assetPath: string, fps?=8, loop?=true,
                                 sprites: string[], overwrite?=false, dryRun?=false }
                              sprites — пути к sub-спрайтам: "Assets/Sprites/X.png/frame_0"

anim.controller.addState      { assetPath: string, stateName: string, clipPath: string,
                                 isDefault?=false, dryRun?=false }

anim.controller.create        { assetPath: string, overwrite?=false, dryRun?=false }

anim.controller.addParameters { assetPath: string,
                                 parameters: [{name:string, type:"float"|"int"|"bool"|"trigger", default?:any}],
                                 dryRun?=false }

anim.assignControllerByQuery  { query, controllerAssetPath: string,
                                 addAnimatorIfMissing?=false, dryRun?=false }

anim.report                   { assetPath: string }
```

---

## component

```
component.setEnabled  { componentInstanceId: int, enabled: bool, ping?=false }
```

---

## editor.play — Play Mode

```
editor.play.status  (нет аргументов)
editor.play.enter   { dryRun?=false }
editor.play.exit    { dryRun?=false }
editor.play.wait    { targetState: "playing"|"stopped", timeoutSeconds?=15 }
```

---

## editor.screenshot — скриншот игры / сцены

```
editor.screenshot  { source?="game"|"scene", width?=640, height?=480, superSampling?=1..4 }
```
Рендерит камеру в PNG, сохраняет в `ProjectSettings/OrchestratorScreenshot.png`.  
Возвращает абсолютный путь — читать через `Read` инструмент для визуального просмотра.  
`source="game"` — Camera.main (работает в Play Mode и Edit Mode).  
`source="scene"` — камера последнего активного SceneView.

---

## editor.log — лог Unity

```
editor.log.read  { max?=50, type?="all"|"error"|"warning"|"log"|"exception",
                    contains?="", last?=true, clear?=false }
```
Кольцевой буфер 2000 записей. Содержит: `type`, `message`, `stackTrace`, `timeUtc`.

---

## asset.ref

```
asset.ref.setFieldByQuery  { query, componentType: string, member: string,
                              assetPath: string,
                              assetType?="auto"|"Material"|"Sprite"|"AudioClip"|"GameObject"|"ScriptableObject",
                              allComponents?=false, max?=5000, dryRun?=false }
```

---

## help

```
help.command         { name: string }         → документация одной команды
help.commands        (нет аргументов)         → список всех 122 зарегистрированных команд
help.schema          (нет аргументов)         → полная схема
help.dump            (нет аргументов)         → только задокументированные команды
help.save            (нет аргументов)
help.load            (нет аргументов)
help.open            (нет аргументов)
help.exportMarkdown  (нет аргументов)
```

---

## Типичные паттерны

### Читать поле компонента
```json
{ "command": "scene.batch.getComponentProperty",
  "args": { "query": {"nameContains":"Player"}, "componentType": "Animator", "member": "enabled" } }
```

### Изменить поле (с проверкой)
```json
{ "command": "scene.batch.setComponentProperty", "args": {
    "query": {"nameContains":"Player"}, "componentType": "Animator",
    "member": "enabled", "value": false, "dryRun": true } }
```

### Назначить ассет на поле (Sprite, Material, Prefab)
```json
{ "command": "asset.ref.setFieldByQuery", "args": {
    "query": {"nameContains":"Player"}, "componentType": "SpriteRenderer",
    "member": "sprite", "assetPath": "Assets/Sprites/player.png", "dryRun": true } }
```

### Полный пайплайн PNG → анимация
```
project.assets.importFile    { source: "C:/...", destination: "Assets/Sprites/X.png" }
project.sprites.configure    { path: "Assets/Sprites/X.png", pixelsPerUnit: 16, spriteMode: "Multiple" }
project.sprites.slice        { path: "...", cellWidth: 32, cellHeight: 32 }
project.sprites.list         { path: "..." }  → получить имена sub-спрайтов
anim.clip.create             { assetPath: "Assets/Anim/Walk.anim", fps: 8, sprites: [...] }
anim.controller.addState     { assetPath: "Assets/Anim/Player.controller", stateName: "Walk",
                                clipPath: "Assets/Anim/Walk.anim", isDefault: true }
anim.assignControllerByQuery { query: {...}, controllerAssetPath: "Assets/Anim/Player.controller" }
scene.save                   {}
```

### Безопасный откат
```
scene.snapshot.take    { name: "before-experiment" }
... деструктивные операции ...
scene.snapshot.restore { id: "before-experiment" }
```
