using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class CommandDispatcher
    {
        // ������� ������ switch � ������ HelpSchema.GetRegisteredCommands() ������ ��������
        private static readonly Dictionary<string, Func<JToken, object>> _handlers =
            new Dictionary<string, Func<JToken, object>>(StringComparer.Ordinal)
            {
                ["ping"] = _ => new
                {
                    unityVersion = Application.unityVersion,
                    project = Application.productName,
                    scene = SceneManager.GetActiveScene().name,
                    dispatcherVersion = "v3-helpregistry-fix"
                },

                ["scene.new"]       = args => SceneFileCommands.New(args),
                ["scene.open"]      = args => SceneFileCommands.Open(args),
                ["scene.save"]      = args => SceneFileCommands.Save(args),
                ["scene.saveAs"]    = args => SceneFileCommands.SaveAs(args),
                ["scene.getActive"] = args => SceneFileCommands.GetActive(args),

                ["scene.scan"] = _ => SceneScanner.ScanActiveScene(),
                ["scene.query"] = args => SceneQuery.Query(args),
                ["scene.selectByQuery"] = args => SceneQuery.SelectByQuery(args),
                ["scene.find"] = args => SceneCommands.Find(args),
                ["scene.select"] = args => SceneCommands.Select(args),
                ["scene.createEmpty"] = args => SceneMutations.CreateEmpty(args),
                ["scene.createPrimitive"] = args => ScenePrimitiveCommands.CreatePrimitive(args),
                ["scene.savePrefab"] = args => ScenePrimitiveCommands.SavePrefab(args),
                ["scene.addPrefab"] = args => SceneMutations.AddPrefab(args),
                ["scene.getComponents"] = args => SceneCommands.GetComponents(args),
                ["scene.setTransform"] = args => SceneMutations.SetTransform(args),
                ["scene.deleteByName"] = args => SceneDeleteCommands.DeleteByName(args),
                ["scene.deleteByQuery"] = args => SceneGameObjectCommands.DeleteByQuery(args),
                ["scene.setActiveByQuery"] = args => SceneGameObjectCommands.SetActiveByQuery(args),
                ["scene.destroyChildrenOf"] = args => SceneGameObjectCommands.DestroyChildrenOf(args),
                ["scene.batchSetEnabledByType"] = args => BatchCommands.BatchSetEnabledByType(args),

                ["scene.snapshot.take"] = args => SnapshotCommands.Take(args),
                ["scene.snapshot.restore"] = args => SnapshotCommands.Restore(args),
                ["scene.snapshot.restoreLatest"] = args => SnapshotCommands.RestoreLatest(args),
                ["scene.snapshot.list"] = args => SnapshotCommands.List(args),
                ["scene.snapshot.latest"] = args => SnapshotCommands.Latest(args),
                ["scene.snapshot.delete"] = args => SnapshotCommands.Delete(args),
                ["scene.snapshot.save"] = args => SnapshotCommands.Save(args),
                ["scene.snapshot.load"] = args => SnapshotCommands.Load(args),

                ["scene.batch.addComponent"] = args => SceneBatch.AddComponent(args),
                ["scene.batch.removeComponent"] = args => SceneBatchProps.RemoveComponent(args),
                ["scene.batch.setComponentEnabled"] = args => SceneBatch.SetComponentEnabled(args),
                ["scene.batch.setComponentProperty"] = args => SceneBatchProps.SetComponentProperty(args),
                ["scene.batch.setComponentProperties"] = args => SceneBatchProps.SetComponentProperties(args),
                ["scene.batch.getComponentProperty"] = args => SceneBatchProps.GetComponentProperty(args),
                ["scene.batch.diffComponentProperties"] = args => SceneBatchProps.DiffComponentProperties(args),
                ["scene.batch.applyIfDiffComponentProperties"] = args => SceneBatchProps.ApplyIfDiffComponentProperties(args),
                ["scene.batch.offsetTransform"] = args => SceneBatchTransforms.OffsetTransform(args),
                ["scene.batch.setTransform"] = args => SceneBatchTransforms.SetTransform(args),
                ["scene.batch.snapToGrid"] = args => SceneBatchTransforms.SnapToGrid(args),
                ["scene.batch.placePrefabGrid"] = args => SceneBatchTransforms.PlacePrefabGrid(args),
                ["scene.batch.setAxis"] = args => SceneBatchTransforms.SetAxis(args),
                ["scene.batch.setParentByQuery"] = args => SceneHierarchyCommands.SetParentByQuery(args),
                ["scene.batch.renameByQuery"] = args => SceneHierarchyCommands.RenameByQuery(args),
                ["scene.batch.duplicateByQuery"] = args => SceneHierarchyCommands.DuplicateByQuery(args),
                ["scene.batch.setSpriteRendererSprite"] = args => SceneBatchSprites.SetSpriteRendererSprite(args),

                ["scene.ref.setFieldByQuery"] = args => SceneReferenceCommands.SetFieldByQuery(args),
                ["scene.ref.setFieldByQuerySelf"] = args => SceneReferenceCommands.SetFieldByQuerySelf(args),
                ["scene.ref.setArrayFieldByQuery"] = args => SceneRefArrayCommands.SetArrayFieldByQuery(args),
                ["scene.ref.describe"] = args => SceneReferenceCommands.Describe(args),

                ["scene.workflow.run"] = args => WorkflowCommands.Run(args),
                ["scene.workflow.last"] = args => WorkflowCommands.Last(args),
                ["scene.workflow.cancelRestore"] = args => WorkflowCommands.CancelScheduledRestore(args),

                ["scene.optimize.plan"] = _ => OptimizerPlan.BuildPlan(),
                ["scene.optimize.apply"] = args => OptimizerApply.Apply(args),
                ["scene.optimize.restore"] = args => OptimizerRestore.Restore(args),

                ["scene.report.updates"] = _ => SceneReports.Updates(),
                ["scene.report.renderers"] = _ => RenderReports.Renderers(),
                ["scene.report.instancesByType"] = args => InstanceReports.InstancesByType(args),
               

                ["materials.report"] = args => MaterialsCommands.Report(args),
                ["materials.reportRich"] = args => MaterialsToolkit.ReportRich(args),
                ["materials.inspect"] = args => MaterialsToolkit.Inspect(args),
                ["materials.findUsage"] = args => MaterialsCommands.FindUsage(args),
                ["materials.setProperty"] = args => MaterialsToolkit.SetProperty(args),
                ["materials.setColorAuto"] = args => MaterialsToolkit.SetColorAuto(args),
                ["materials.setColorByObjectNameContains"] = args => MaterialsCommands.SetColorByObjectNameContains(args),
                ["materials.setColorByMaterialNameContains"] = args => MaterialsToolkit.SetColorByMaterialNameContains(args),
                ["materials.replaceByObjectNameContains"] = args => MaterialsCommands.ReplaceByObjectNameContains(args),
                ["materials.replaceByMaterialNameContains"] = args => MaterialsToolkit.ReplaceByMaterialNameContains(args),
                ["materials.applyPreset"] = args => MaterialsToolkit.ApplyPreset(args),
                ["materials.applyPresetAutoTarget"] = args => MaterialsToolkit.ApplyPresetAutoTarget(args),
                ["materials.presets.list"] = args => MaterialsToolkit.ListPresets(args),
                ["materials.presets.applyAutoTarget"] = args => MaterialsToolkit.PresetsApplyAutoTarget(args),
                ["materials.workflow.applyPresetAutoTargetDelay"] = args => MaterialsWorkflow.ApplyPresetAutoTargetDelay(args),
                ["materials.workflow.testPresetAutoTarget"] = args => MaterialsToolkit.WorkflowTestPresetAutoTarget(args),
                ["materials.workflow.testPresetAutoTargetV2"] = args => MaterialsToolkit.WorkflowTestPresetAutoTargetV2(args),
                ["materials.workflow.cancelRestore"] = args => MaterialsWorkflow.CancelScheduledRestore(args),

                ["materials.snapshot.take"] = args => MaterialSnapshotCommands.Take(args),
                ["materials.snapshot.restore"] = args => MaterialSnapshotCommands.Restore(args),
                ["materials.snapshot.list"] = args => MaterialSnapshotCommands.List(args),
                ["materials.snapshot.latest"] = args => MaterialSnapshotCommands.Latest(args),
                ["materials.snapshot.save"] = args => MaterialSnapshotCommands.Save(args),
                ["materials.snapshot.load"] = args => MaterialSnapshotCommands.Load(args),

                ["prefab.replaceByQuery"] = args => PrefabReplaceCommands.ReplaceByQuery(args),
                ["prefab.instantiate"] = args => PrefabInstantiateCommands.Instantiate(args),
                ["prefab.applyOverridesByQuery"] = args => PrefabCommands.ApplyOverridesByQuery(args),
                ["prefab.revertOverridesByQuery"] = args => PrefabCommands.RevertOverridesByQuery(args),
                ["prefab.unpackByQuery"] = args => PrefabCommands.UnpackByQuery(args),

                ["project.assets.createFolder"] = args => ProjectAssetsFolderCommands.CreateFolder(args),
                ["project.assets.importFile"]   = args => ProjectAssetsFolderCommands.ImportFile(args),
                ["project.assets.refresh"]      = args => ProjectAssetsFolderCommands.Refresh(args),

                ["project.sprites.configure"] = args => SpritesheetCommands.Configure(args),
                ["project.sprites.slice"]     = args => SpritesheetCommands.Slice(args),
                ["project.sprites.list"]      = args => SpritesheetCommands.List(args),

                ["anim.clip.create"]           = args => AnimClipCommands.Create(args),
                ["anim.controller.addState"]   = args => AnimClipCommands.AddState(args),

                ["project.assets.find"] = args => ProjectAssetsCommands.Find(args),
                ["project.assets.inspect"] = args => ProjectAssetsInspectCommands.Inspect(args),
                ["project.scripts.read"] = args => ProjectScriptsPatchCommands.Read(args),
                ["project.scripts.patch"] = args => ProjectScriptsPatchCommands.Patch(args),
                ["project.scripts.create"] = args => ProjectScriptsCommands.Create(args),
                ["project.compilation.wait"] = args => ProjectCompilationCommands.Wait(args),
                ["project.compilation.status"] = args => ProjectCompilationCommands.Status(args),
                ["project.compilation.errors"] = args => ProjectCompilationCommands.Errors(args),
                ["project.exportContext"] = args => ExportContextCommands.Export(args),

                ["component.setEnabled"] = args => ComponentCommands.SetEnabled(args),

                ["editor.play.status"] = args => EditorPlayModeCommands.Status(args),
                ["editor.play.enter"] = args => EditorPlayModeCommands.Enter(args),
                ["editor.play.exit"] = args => EditorPlayModeCommands.Exit(args),
                ["editor.play.wait"] = args => EditorPlayModeCommands.Wait(args),

                ["editor.log.read"]    = args => EditorLogCommands.Read(args),
                ["editor.screenshot"]  = args => EditorScreenshotCommands.Capture(args),

                ["anim.controller.create"] = args => AnimatorCommands.CreateController(args),
                ["anim.controller.addParameters"] = args => AnimatorCommands.AddParameters(args),
                ["anim.assignControllerByQuery"] = args => AnimatorCommands.AssignControllerByQuery(args),
                ["anim.report"] = args => AnimatorCommands.Report(args),

                ["asset.ref.setFieldByQuery"] = args => AssetReferenceCommands.SetFieldByQuery(args),

                ["help.command"] = args => HelpCommands.Command(args),
                ["help.commands"] = args => HelpCommands.Commands(args),
                ["help.schema"] = args => HelpCommands.Schema(args),
                ["help.dump"] = args => HelpSchema.Dump(args),
                ["help.save"] = args => HelpSchema.Save(args),
                ["help.load"] = args => HelpSchema.Load(args),
                ["help.open"] = args => HelpSchema.Open(args),
                ["help.exportMarkdown"] = args => HelpSchema.ExportMarkdown(args),
            };

        // ��������� ����� ��� HelpSchema � ������ ������ ������ �������� �������������
        public static string[] GetRegisteredCommands() =>
            System.Linq.Enumerable.ToArray(_handlers.Keys);

        public static CommandResponse Execute(CommandRequest req)
        {
            req.id ??= Guid.NewGuid().ToString("N");

            if (!_handlers.TryGetValue(req.command ?? "", out var handler))
            {
                return new CommandResponse
                {
                    id = req.id,
                    ok = false,
                    error = $"Unknown command: {req.command}"
                };
            }

            try
            {
                var result = handler(req.args);
                return new CommandResponse { id = req.id, ok = true, result = result };
            }
            catch (Exception ex)
            {
                return new CommandResponse
                {
                    id = req.id,
                    ok = false,
                    error = ex.Message
                };
            }
        }
    }
}