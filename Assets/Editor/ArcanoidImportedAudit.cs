using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArcanoidImportedAudit
{
    [Serializable] class Audit
    {
        public bool passed;
        public List<string> findings = new List<string>();
    }

    public static void Run()
    {
        var audit = new Audit();
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
        var data = AssetDatabase.LoadAssetAtPath<GameDataScript>("Assets/Data/GameData.asset");
        audit.findings.Add("GameData imports as GameDataScript: " + (data != null));
        if (data != null)
        {
            string assetGuid;
            long localId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data, out assetGuid, out localId);
            audit.findings.Add("GameData asset identifier: " + assetGuid + " / " + localId);
        }
        var player = UnityEngine.Object.FindObjectOfType<PlayerScript>();
        audit.findings.Add("PlayerScript present: " + (player != null));
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var names = new List<string>();
            foreach (var component in root.GetComponents<Component>())
                names.Add(component == null ? "MISSING SCRIPT" : component.GetType().FullName);
            audit.findings.Add("Root " + root.name + ": " + string.Join(", ", names));
        }
        string error = "PlayerScript missing";
        audit.passed = data != null && player != null && player.ValidateConfiguration(out error);
        audit.findings.Add("Scene configuration: " + (audit.passed ? "PASS" : error));
        if (player != null)
        {
            audit.findings.Add("Player.GameData reference resolves: " + (player.gameData != null));
            audit.findings.Add("Player Rigidbody2D: " + (player.GetComponent<Rigidbody2D>() != null));
            audit.findings.Add("MainCamera tag: " + (Camera.main != null));
            audit.findings.Add("Game UI reference: " + (player.gameUi != null));
        }
        Directory.CreateDirectory("Tests");
        File.WriteAllText("Tests/imported-project-report.json", JsonUtility.ToJson(audit, true));
        if (!audit.passed) throw new Exception("Imported Unity scene failed configuration audit: " + error);
        ArcanoidProjectBuilder.ValidateProject();
        Debug.Log("ARCANOID: Imported scene and asset references passed.");
    }
}
