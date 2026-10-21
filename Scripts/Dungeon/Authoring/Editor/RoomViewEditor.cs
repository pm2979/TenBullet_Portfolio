using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Room Prefab 자식 구성에서 RoomView가 요구하는 참조를 자동으로 찾아 연결한다.
/// </summary>
[CustomEditor(typeof(RoomView))]
public sealed class RoomViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("참조 자동 연결", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "자식 Hierarchy에서 Tilemap·RoomSpawnPointView·RoomAccessController와 "
            + "EntryTrigger(Is Trigger O)·RoomCollider(Is Trigger X)를 찾아 비어 있는 참조에 채웁니다. "
            + "이미 연결된 참조는 덮어쓰지 않습니다.",
            MessageType.Info);

        if (GUILayout.Button("자식에서 참조 자동 연결"))
        {
            AutoWireReferences();
        }
    }

    private void AutoWireReferences()
    {
        var view = (RoomView)target;

        var allColliders2D = view.GetComponentsInChildren<Collider2D>(true);
        Collider2D[] triggerColliders = allColliders2D.Where(c => c.isTrigger).ToArray();
        Collider2D[] solidColliders = allColliders2D.Where(c => !c.isTrigger).ToArray();

        var tilemaps = view.GetComponentsInChildren<Tilemap>(true);
        var spawnPoints = view.GetComponentsInChildren<RoomSpawnPointView>(true);
        var accessController = view.GetComponentInChildren<RoomAccessController>(true);

        serializedObject.Update();

        int filledCount = 0;
        var warnings = new List<string>();

        filledCount += TryFillObjectReference(
            "_entryTrigger", ResolveSingle(triggerColliders, "EntryTrigger(Is Trigger O)", warnings));
        filledCount += TryFillArray("_tilemaps", tilemaps);
        filledCount += TryFillArray("_roomColliders", solidColliders);
        filledCount += TryFillArray("_spawnPoints", spawnPoints);
        filledCount += TryFillObjectReference("_accessController", accessController);

        serializedObject.ApplyModifiedProperties();

        string message = filledCount > 0
            ? $"빈 참조 {filledCount}개를 채웠습니다."
            : "채울 빈 참조가 없습니다(이미 모두 연결되어 있음).";

        if (warnings.Count > 0)
        {
            message += "\n\n" + string.Join("\n", warnings);
        }

        EditorUtility.DisplayDialog("참조 자동 연결", message, "확인");
    }

    // 여러 개가 발견되면 연결하지 않고 경고만 남긴다(어떤 것이 맞는지 자동으로 판단할 수 없음).
    private static Object ResolveSingle(Collider2D[] candidates, string label, List<string> warnings)
    {
        if (candidates.Length == 0)
        {
            return null;
        }

        if (candidates.Length > 1)
        {
            warnings.Add($"{label} 후보가 {candidates.Length}개 발견되어 자동 연결을 건너뛰었습니다. 직접 지정해 주세요.");
            return null;
        }

        return candidates[0];
    }

    private int TryFillObjectReference(string propertyName, Object value)
    {
        if (value == null)
        {
            return 0;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property.objectReferenceValue != null)
        {
            return 0;
        }

        property.objectReferenceValue = value;
        return 1;
    }

    private int TryFillArray<T>(string propertyName, T[] values) where T : Object
    {
        if (values == null || values.Length == 0)
        {
            return 0;
        }

        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property.arraySize > 0)
        {
            return 0;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        return 1;
    }
}
