using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Local opt-in bridge for checking this project in an already open editor.
// Requests live under Tests; a request is consumed once and leaves a result file.
[InitializeOnLoad]
public static class ArcanoidEditorCheckRunner
{
    const string RequestPath = "Tests/editor-check-request.json";
    const string ResultPath = "Tests/editor-check-result.json";
    [Serializable] class Request { public string action; }
    [Serializable] class Result { public bool passed; public string action; public string details; }
    static Request pending;

    static ArcanoidEditorCheckRunner() { EditorApplication.update += Update; }

    static void Update()
    {
        if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (pending == null)
        {
            if (!File.Exists(RequestPath)) return;
            pending = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
            File.Delete(RequestPath);
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            return;
        }
        var request = pending;
        pending = null;
        try
        {
            var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty)
            {
                Directory.CreateDirectory("Tests/SceneBackups");
                EditorSceneManager.SaveScene(current, "Tests/SceneBackups/BeforeCheck.unity", true);
            }
            if (request.action == "audit") ArcanoidImportedAudit.Run();
            else if (request.action == "refresh")
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ArcanoidImportedAudit.Run();
            }
            else if (request.action == "repair")
            {
                ArcanoidProjectBuilder.CreateProject();
                ArcanoidImportedAudit.Run();
            }
            else if (request.action == "build")
            {
                ArcanoidImportedAudit.Run();
                ArcanoidProjectBuilder.BuildWindows();
            }
            else if (request.action == "play")
            {
                ArcanoidImportedAudit.Run();
                SessionState.SetBool("Arcanoid.EditorSmokeCheck", true);
                EditorApplication.isPlaying = true;
            }
            else throw new InvalidOperationException("Unknown check action: " + request.action);
            File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result { passed = true, action = request.action,
                details = request.action == "play" ? "Play Mode check requested; see editor-runtime-report.json" : "Completed" }, true));
        }
        catch (Exception error)
        {
            File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result { passed = false, action = request.action, details = error.ToString() }, true));
            Debug.LogError("Arcanoid editor check: " + error.Message);
        }
    }
}
