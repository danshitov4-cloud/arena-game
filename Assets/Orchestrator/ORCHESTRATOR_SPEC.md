# Unity Orchestrator Spec

- SchemaVersion: `0.2.0`
- GeneratedAtUtc: `2026-03-30T10:58:26.8317474Z`

## Endpoint
- POST `http://127.0.0.1:5137/command`
- JSON body: `{ id, command, args, dryRun }`

## Notes
- `dryRun=true` where possible to preview changes.
- Use snapshots/workflows for rollback.

## Coverage report
- Dispatcher commands: **18**
- Described commands: **18**
- Missing in help: **0**
- Extra in help: **0**

## help.command

### `help.command`

Returns schema for a single command.

- **Risk:** `info`
- **Owner:** `HelpSchema`

#### Args
- `command`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"scene.batch.offsetTransform"}`


## help.commands

### `help.commands`

Lists all commands available in dispatcher.

- **Risk:** `info`
- **Owner:** `HelpSchema`

#### Args
- (none)


## help.schema

### `help.schema`

Compares dispatcher commands vs described commands. Can return skeleton for missing.

- **Risk:** `info`
- **Owner:** `HelpSchema`

#### Args
- `includeSkeleton`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`


## materials.snapshot

### `materials.snapshot.restore`

Restores material properties from material snapshot by id.

- **Risk:** `risky`
- **Owner:** `MaterialSnapshotCommands`

#### Args
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `id`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"8db038d7e9e744359088d87345f7b7c7"}`

### `materials.snapshot.take`

Captures material properties for top-used materials in scoped renderers (by nameContains).

- **Risk:** `risky`
- **Owner:** `MaterialSnapshotCommands`

#### Args
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `includeAllColorAliases`: `{"type":"bool","required":false,"optional":true,"default":true,"example":null}`
- `name`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"mat-before-red"}`
- `nameContains`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"Building"}`
- `properties`: `{"type":"object","required":false,"optional":true,"example":"[\"_Color\",\"_BaseColor\",\"_EmissionColor\",\"_ZWrite\"]"}`
- `top`: `{"type":"int","required":false,"optional":true,"default":3,"min":1,"max":200,"example":null}`
- `useShared`: `{"type":"bool","required":false,"optional":true,"default":true,"example":null}`


## ping

### `ping`

Health check. Returns unity version, project name, active scene.

- **Risk:** `info`
- **Owner:** `CommandDispatcher`

#### Args
- (none)


## scene.batch

### `scene.batch.applyIfDiffComponentProperties`

Runs diff; if differences exist, applies set:{...} (optionally only when apply=true).

- **Risk:** `risky`
- **Owner:** `SceneBatchProps`

#### Args
- `apply`: `{"type":"bool","required":false,"optional":true,"default":true,"example":null}`
- `componentType`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"Rigidbody"}`
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `query`: `{"type":"object","required":true,"optional":false,"example":"{nameContains:\"Building\", max:500, includeInactive:true}"}`
- `sampleLimit`: `{"type":"int","required":false,"optional":true,"default":20,"min":0,"max":200,"example":null}`
- `set`: `{"type":"object","required":true,"optional":false,"example":"{isKinematic:true, useGravity:false}"}`

### `scene.batch.diffComponentProperties`

Checks which objects differ from desired values (set:{...}). Returns diff stats + samples.

- **Risk:** `info`
- **Owner:** `SceneBatchProps`

#### Args
- `componentType`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"Rigidbody"}`
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `query`: `{"type":"object","required":true,"optional":false,"example":"{nameContains:\"Building\", max:500, includeInactive:true}"}`
- `sampleLimit`: `{"type":"int","required":false,"optional":true,"default":20,"min":0,"max":200,"example":null}`
- `set`: `{"type":"object","required":true,"optional":false,"example":"{isKinematic:true, useGravity:false}"}`

### `scene.batch.offsetTransform`

Offsets position/rotation/scale for objects selected by query.

- **Risk:** `risky`
- **Owner:** `SceneBatchTransforms`

#### Args
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `max`: `{"type":"int","required":false,"optional":true,"default":2000,"min":1,"max":20000,"example":null}`
- `positionDelta`: `{"type":"vec3","required":false,"optional":true,"default":{"x":0,"y":0,"z":0},"example":null}`
- `query`: `{"type":"object","required":true,"optional":false,"example":"scene.query args object"}`
- `rotationDeltaEuler`: `{"type":"vec3","required":false,"optional":true,"default":{"x":0,"y":0,"z":0},"example":null}`
- `scaleDelta`: `{"type":"vec3","required":false,"optional":true,"default":{"x":0,"y":0,"z":0},"example":null}`
- `scaleMul`: `{"type":"vec3","required":false,"optional":true,"default":{"x":1,"y":1,"z":1},"example":null}`
- `space`: `{"type":"string","required":false,"optional":true,"default":"world","allowed":["world","local"],"example":null}`

### `scene.batch.placePrefabGrid`

Creates a grid of prefab instances (optionally under a parent).

- **Risk:** `risky`
- **Owner:** `SceneBatchTransforms`

#### Args
- `assetPath`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"Assets/Prefabs/Tree.prefab"}`
- `centered`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `cols`: `{"type":"int","required":true,"optional":false,"default":null,"min":1,"max":null,"example":null}`
- `createParentIfMissing`: `{"type":"bool","required":false,"optional":true,"default":true,"example":null}`
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `makeParentNameUnique`: `{"type":"bool","required":false,"optional":true,"default":true,"example":null}`
- `maxTotal`: `{"type":"int","required":false,"optional":true,"default":5000,"min":1,"max":50000,"example":null}`
- `nameTemplate`: `{"type":"string","required":false,"optional":true,"default":"Grid_{r}_{c}","allowed":null,"example":null}`
- `origin`: `{"type":"vec3","required":false,"optional":true,"default":{"x":0,"y":0,"z":0},"example":null}`
- `parentInstanceId`: `{"type":"int","required":false,"optional":true,"default":0,"min":null,"max":null,"example":null}`
- `parentName`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"GridRoot"}`
- `plane`: `{"type":"string","required":false,"optional":true,"default":"xz","allowed":["xz","xy","yz"],"example":null}`
- `rotationEuler`: `{"type":"vec3","required":false,"optional":true,"default":{"x":0,"y":0,"z":0},"example":null}`
- `rows`: `{"type":"int","required":true,"optional":false,"default":null,"min":1,"max":null,"example":null}`
- `scale`: `{"type":"vec3","required":false,"optional":true,"default":{"x":1,"y":1,"z":1},"example":null}`
- `spacing`: `{"type":"vec3","required":false,"optional":true,"default":{"x":1,"y":0,"z":1},"example":null}`

### `scene.batch.setComponentProperties`

Sets multiple members at once (set:{...}) for all objects matching query.

- **Risk:** `risky`
- **Owner:** `SceneBatchProps`

#### Args
- `componentType`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"UnityEngine.Rendering.Universal.Light2D"}`
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `query`: `{"type":"object","required":true,"optional":false,"example":"{hasComponent:\"Light2D\", max:2000, includeInactive:true}"}`
- `set`: `{"type":"object","required":true,"optional":false,"example":"{intensity:3.0, color:{r:1,g:0.8,b:0.6,a:1}}"}`

### `scene.batch.setComponentProperty`

Sets one component member/property for all objects matching query.

- **Risk:** `risky`
- **Owner:** `SceneBatchProps`

#### Args
- `componentType`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"UnityEngine.Camera / Rigidbody / UnityEngine.Rendering.Universal.Light2D"}`
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `member`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"orthographicSize / intensity / isKinematic"}`
- `query`: `{"type":"object","required":true,"optional":false,"example":"{nameContains:\"Building\", max:500, includeInactive:true}"}`
- `value`: `{"type":"object","required":true,"optional":false,"example":"8  (or true/false or {x:0,y:1,z:0})"}`

### `scene.batch.snapToGrid`

Snaps objects (from query) to grid (position rounding).

- **Risk:** `risky`
- **Owner:** `SceneBatchTransforms`

#### Args
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `gridSize`: `{"type":"vec3","required":false,"optional":true,"default":{"x":1,"y":1,"z":1},"example":"{x:1,y:1,z:1}"}`
- `origin`: `{"type":"vec3","required":false,"optional":true,"default":{"x":0,"y":0,"z":0},"example":null}`
- `query`: `{"type":"object","required":true,"optional":false,"example":"{nameContains:\"Building\", max:500, includeInactive:true}"}`
- `space`: `{"type":"string","required":false,"optional":true,"default":"world","allowed":["world","local"],"example":null}`


## scene.query

### `scene.query`

Finds GameObjects by query filters (name/tag/layer/component). Returns list (truncated by max).

- **Risk:** `info`
- **Owner:** `SceneQuery`

#### Args
- `caseSensitive`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `hasComponent`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"Rigidbody2D / UnityEngine.Camera / Light2D"}`
- `includeInactive`: `{"type":"bool","required":false,"optional":true,"default":true,"example":null}`
- `layer`: `{"type":"int","required":false,"optional":true,"default":null,"min":null,"max":null,"example":"0"}`
- `max`: `{"type":"int","required":false,"optional":true,"default":200,"min":1,"max":5000,"example":null}`
- `nameContains`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"Building"}`
- `tag`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"Player"}`


## scene.selectByQuery

### `scene.selectByQuery`

Selects objects found by scene.query (Editor selection).

- **Risk:** `risky`
- **Owner:** `SceneQuery`

#### Args
- `caseSensitive`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `hasComponent`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"Rigidbody2D"}`
- `includeInactive`: `{"type":"bool","required":false,"optional":true,"default":true,"example":null}`
- `layer`: `{"type":"int","required":false,"optional":true,"default":null,"min":null,"max":null,"example":"0"}`
- `max`: `{"type":"int","required":false,"optional":true,"default":200,"min":1,"max":5000,"example":null}`
- `nameContains`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"Building"}`
- `ping`: `{"type":"bool","required":false,"optional":true,"default":true,"example":"true"}`
- `tag`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"Player"}`


## scene.snapshot

### `scene.snapshot.list`

Lists saved scene snapshots (most recent first).

- **Risk:** `info`
- **Owner:** `SnapshotCommands`

#### Args
- `max`: `{"type":"int","required":false,"optional":true,"default":50,"min":1,"max":5000,"example":null}`

### `scene.snapshot.restore`

Restores scene snapshot by id.

- **Risk:** `risky`
- **Owner:** `SnapshotCommands`

#### Args
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `id`: `{"type":"string","required":true,"optional":false,"default":null,"allowed":null,"example":"f579d549714c49a6b28c1a17ce4fcefb"}`

### `scene.snapshot.take`

Takes a scene snapshot (stores enabled state / tracked stuff inside orchestrator snapshot system).

- **Risk:** `risky`
- **Owner:** `SnapshotCommands`

#### Args
- `dryRun`: `{"type":"bool","required":false,"optional":true,"default":false,"example":null}`
- `includeTypes`: `{"type":"object","required":false,"optional":true,"example":"[\"SortByY\",\"BuildingView\"]"}`
- `name`: `{"type":"string","required":false,"optional":true,"default":null,"allowed":null,"example":"before-opt"}`


